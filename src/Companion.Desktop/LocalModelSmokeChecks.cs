using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;

namespace Companion.Desktop;

internal static class LocalModelSmokeChecks
{
    internal static async Task RunAsync(string modelDirectory, string evidenceDirectory)
    {
        var observations = new List<object>();
        var watch = Stopwatch.StartNew();
        var status = "FAIL";
        var failures = new List<string>();
        var contentFailures = new List<string>();
        int? workerId = null;
        long peakPrivate = 0;
        var host = new LocalModelHost(modelDirectory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        try
        {
            var model = await host.GetModelAsync(deadline.Token);
            workerId = host.WorkerProcessId;
            using var process = Process.GetProcessById(workerId ?? throw new InvalidOperationException("Worker PID absent."));
            observations.Add(new { stage = "ready", elapsedSeconds = watch.Elapsed.TotalSeconds, workerId });
            async Task<string> Turn(string id, params ChatMessage[] messages)
            {
                var text = new StringBuilder();
                var deltas = 0;
                var completions = 0;
                string? finishReason = null;
                var timer = Stopwatch.StartNew();
                string? error = null;
                try
                {
                    await foreach (var item in model.StreamAsync(messages, deadline.Token))
                    {
                        if (item is ChatTextDelta delta) { text.Append(delta.Text); deltas++; }
                        if (item is ChatCompleted completion) { completions++; finishReason = completion.Reason.ToString(); }
                        process.Refresh();
                        peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
                    }
                }
                catch (Exception exception) { error = exception.GetType().Name; throw; }
                finally
                {
                    observations.Add(new { stage = id, messages, response = text.ToString(), deltas, completions, finishReason, error,
                        elapsedSeconds = timer.Elapsed.TotalSeconds, sampledPeakPrivateBytes = peakPrivate });
                }
                if (completions != 1 || deltas == 0) failures.Add(id + ": missing or duplicate streamed completion");
                return text.ToString();
            }
            var exact = await Turn("A", new ChatMessage(ChatRole.User, "Reply with exactly this word: LOCAL_READY"));
            if (exact.Trim() != "LOCAL_READY") contentFailures.Add("A: output differs");
            var remember = new ChatMessage(ChatRole.User, "For this conversation, remember that my fictional pet is named Maple. Reply with only the pet's name.");
            var acknowledgment = await Turn("B1", remember);
            if (acknowledgment.Trim() != "Maple") contentFailures.Add("B1: output differs");
            var recall = await Turn("B2", remember, new(ChatRole.Assistant, acknowledgment),
                new(ChatRole.User, "What is my fictional pet's name? Reply with only the name."));
            if (recall.Trim() != "Maple") contentFailures.Add("B2: output differs");
            await Turn("C", new ChatMessage(ChatRole.User, "Give exactly three short tips for organizing a desk."));

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var sawDelta = false;
            var cancelled = false;
            var partial = new StringBuilder();
            try
            {
                await foreach (var item in model.StreamAsync([new(ChatRole.User, "Write a long list of simple ideas for a weekend at home.")], cancellation.Token))
                {
                    if (item is ChatTextDelta { Text.Length: > 0 } delta) { partial.Append(delta.Text); sawDelta = true; cancellation.Cancel(); }
                }
            }
            catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { cancelled = true; }
            observations.Add(new { stage = "cancel-stream", sawDelta, cancelled, partialText = partial.ToString() });
            if (!sawDelta || !cancelled) failures.Add("cancel-stream: cancellation was not observed after a delta");
            await Turn("after-cancel-A", new ChatMessage(ChatRole.User, "Reply with exactly this word: LOCAL_READY"));
            await host.DisposeAsync();
            process.Refresh();
            if (!process.HasExited) failures.Add("worker remained alive after disposal");
            observations.Add(new { stage = "worker-disposed", exited = process.HasExited });
            await host.DisposeAsync();
            await using var startupHost = new LocalModelHost(modelDirectory);
            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var startup = startupHost.GetModelAsync(startupCancellation.Token);
            int? startupPid = null;
            while (!startup.IsCompleted && (startupPid = startupHost.WorkerProcessId) is null)
                await Task.Delay(10, deadline.Token);
            var cancelledWhilePending = !startup.IsCompleted;
            startupCancellation.Cancel();
            var startupCancelled = false;
            try { await startup; }
            catch (OperationCanceledException) { startupCancelled = true; }
            await startupHost.DisposeAsync();
            var startupWorkerExited = true;
            if (startupPid.HasValue)
            {
                try { using var startedProcess = Process.GetProcessById(startupPid.Value); startupWorkerExited = startedProcess.HasExited; }
                catch (ArgumentException) { }
            }
            observations.Add(new { stage = "cancel-startup", startupPid, cancelledWhilePending, startupCancelled, startupWorkerExited });
            if (startupPid is null || !cancelledWhilePending || !startupCancelled || !startupWorkerExited)
                failures.Add("cancel-startup: spawned, pending cancellation and cleanup not all observed");
            status = failures.Count == 0 ? "PASS" : "FAIL";
        }
        catch (Exception exception)
        {
            failures.Add("execution: " + exception.GetType().Name);
            throw;
        }
        finally
        {
            try { await host.DisposeAsync(); }
            finally
            {
                Directory.CreateDirectory(evidenceDirectory);
                await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "local-model-smoke.json"),
                    JsonSerializer.Serialize(new { schemaVersion = 1, status, failures, contentFailures, workerId,
                        totalSeconds = watch.Elapsed.TotalSeconds, sampledPeakPrivateBytes = peakPrivate, observations },
                        new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        if (failures.Count != 0) throw new InvalidOperationException("Local model checks failed; all observations retained.");
    }
}
