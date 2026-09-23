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
        var manualContentChecks = new[] { new { stage = "C", status = "REQUIRES_MANUAL_REVIEW",
            criterion = "Three distinct, relevant short tips for organizing a desk; inspect retained response." } };
        var ownedProcesses = new List<Process>();
        int? workerId = null;
        int? replacementWorkerId = null;
        Process? currentProcess = null;
        LocalModelHost? startupHost = null;
        long peakPrivate = 0;
        LocalModelHost? host = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        try
        {
            host = new LocalModelHost(modelDirectory);
            var model = await host.GetModelAsync(deadline.Token);
            workerId = host.WorkerProcessId;
            Process HoldOwnedProcess(int id)
            {
                var held = Process.GetProcessById(id);
                // Hold the actual process handle throughout the attempt rather than relying on later PID lookup.
                try
                {
                    _ = held.SafeHandle;
                    ownedProcesses.Add(held);
                    return held;
                }
                catch { held.Dispose(); throw; }
            }
            currentProcess = HoldOwnedProcess(workerId ?? throw new InvalidOperationException("Worker PID absent."));
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
                        if (item is ChatTextDelta delta)
                        {
                            if (completions != 0) failures.Add(id + ": text followed terminal completion");
                            text.Append(delta.Text);
                            if (delta.Text.Length != 0) deltas++;
                        }
                        if (item is ChatCompleted completion) { completions++; finishReason = completion.Reason.ToString(); }
                        if (item is not (ChatTextDelta or ChatCompleted)) failures.Add(id + ": unknown stream event");
                        var observedProcess = currentProcess ?? throw new InvalidOperationException("No current owned worker handle.");
                        observedProcess.Refresh();
                        peakPrivate = Math.Max(peakPrivate, observedProcess.PrivateMemorySize64);
                    }
                }
                catch (Exception exception) { error = exception.GetType().Name; throw; }
                finally
                {
                    observations.Add(new { stage = id, messages, response = text.ToString(), deltas, completions, finishReason, error,
                        elapsedSeconds = timer.Elapsed.TotalSeconds, sampledPeakPrivateBytes = peakPrivate });
                }
                if (completions != 1 || deltas == 0 || string.IsNullOrWhiteSpace(text.ToString()))
                    failures.Add(id + ": missing nonempty output or single streamed completion");
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

            var oversized = new ChatMessage(ChatRole.User, string.Concat(Enumerable.Repeat("x ", 3000)));
            var rejectedDeltas = 0;
            var rejectedCompletions = 0;
            var rejectedText = new StringBuilder();
            ChatFailure? rejection = null;
            string? rejectionError = null;
            var rejectionTimer = Stopwatch.StartNew();
            try
            {
                await foreach (var item in model.StreamAsync([oversized], deadline.Token))
                {
                    if (item is ChatTextDelta delta) { rejectedDeltas++; rejectedText.Append(delta.Text); }
                    if (item is ChatCompleted) rejectedCompletions++;
                }
            }
            catch (ChatModelException exception) when (exception.Failure == ChatFailure.InvalidRequest)
            { rejection = exception.Failure; }
            catch (Exception exception) { rejectionError = exception.GetType().Name; throw; }
            finally
            {
                observations.Add(new { stage = "D-oversized-latest", messages = new[] { oversized },
                    inputCharacters = oversized.Content.Length, inputUtf8Bytes = Encoding.UTF8.GetByteCount(oversized.Content),
                    designedPromptBudgetTokens = 1792, actualPromptTokens = (int?)null,
                    rejection = rejection?.ToString(), rejectionError, deltas = rejectedDeltas, completions = rejectedCompletions,
                    response = rejectedText.ToString(), elapsedSeconds = rejectionTimer.Elapsed.TotalSeconds });
            }
            if (rejection != ChatFailure.InvalidRequest || rejectedDeltas != 0 || rejectedCompletions != 0)
                failures.Add("D-oversized-latest: InvalidRequest with zero stream events was not observed");
            var afterReject = await Turn("after-reject-A", new ChatMessage(ChatRole.User, "Reply with exactly this word: LOCAL_READY"));
            if (afterReject.Trim() != "LOCAL_READY") contentFailures.Add("after-reject-A: output differs");

            var longHistory = new List<ChatMessage>();
            var historicalFiller = string.Concat(Enumerable.Repeat("x ", 300));
            for (var index = 1; index <= 4; index++)
            {
                longHistory.Add(new(ChatRole.User, $"Public history fixture {index}. " + historicalFiller));
                longHistory.Add(new(ChatRole.Assistant, $"Public archived answer {index}. " + historicalFiller));
            }
            longHistory.Add(new(ChatRole.User, "Ignore repeated filler. Reply with one short sentence describing a tidy desk."));
            observations.Add(new { stage = "E-fixture-bounds", historicalPairs = 4, fillerRepetitions = 2400,
                inputCharacters = longHistory.Sum(message => message.Content.Length),
                inputUtf8Bytes = longHistory.Sum(message => Encoding.UTF8.GetByteCount(message.Content)),
                designedPromptBudgetTokens = 1792, actualPromptTokens = (int?)null, removedPairs = (int?)null });
            await Turn("E-long-history", longHistory.ToArray());

            var originalProcess = currentProcess;
            var originalModel = model;
            var workerExitTimer = Stopwatch.StartNew();
            if (host.WorkerProcessId != originalProcess.Id || originalProcess.HasExited)
                throw new InvalidOperationException("The original owned worker was not alive before the exit fixture.");
            observations.Add(new { stage = "F-owned-worker-exit-requested", workerId = originalProcess.Id,
                liveWorkerMatchesHost = true, entireProcessTree = false });
            originalProcess.Kill(entireProcessTree: false);
            await originalProcess.WaitForExitAsync(deadline.Token).WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);
            observations.Add(new { stage = "F-owned-worker-exit", workerId = originalProcess.Id,
                exited = originalProcess.HasExited, hostReportsNoWorker = host.WorkerProcessId is null,
                elapsedSeconds = workerExitTimer.Elapsed.TotalSeconds });
            if (!originalProcess.HasExited || host.WorkerProcessId is not null)
                failures.Add("F-owned-worker-exit: known owned worker exit was not observed");
            model = await host.GetModelAsync(deadline.Token);
            replacementWorkerId = host.WorkerProcessId;
            currentProcess = HoldOwnedProcess(replacementWorkerId ?? throw new InvalidOperationException("Replacement worker PID absent."));
            var replaced = replacementWorkerId != workerId && !ReferenceEquals(originalModel, model) && !currentProcess.HasExited;
            observations.Add(new { stage = "F-worker-restarted", workerId, replacementWorkerId, replaced,
                elapsedSeconds = workerExitTimer.Elapsed.TotalSeconds });
            if (!replaced) failures.Add("F-worker-restarted: a new live owned worker and model were not observed");
            var afterExit = await Turn("after-worker-exit-A", new ChatMessage(ChatRole.User, "Reply with exactly this word: LOCAL_READY"));
            if (afterExit.Trim() != "LOCAL_READY") contentFailures.Add("after-worker-exit-A: output differs");

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var sawDelta = false;
            var cancelled = false;
            var partial = new StringBuilder();
            var cancelFixture = new ChatMessage(ChatRole.User, "Write a long list of simple ideas for a weekend at home.");
            string? cancelError = null;
            try
            {
                await foreach (var item in model.StreamAsync([cancelFixture], cancellation.Token))
                {
                    if (item is ChatTextDelta { Text.Length: > 0 } delta) { partial.Append(delta.Text); sawDelta = true; cancellation.Cancel(); }
                }
            }
            catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { cancelled = true; }
            catch (Exception exception) { cancelError = exception.GetType().Name; throw; }
            finally
            {
                observations.Add(new { stage = "cancel-stream", messages = new[] { cancelFixture }, sawDelta, cancelled,
                    partialText = partial.ToString(), error = cancelError });
            }
            if (!sawDelta || !cancelled) failures.Add("cancel-stream: cancellation was not observed after a delta");
            var afterCancel = await Turn("after-cancel-A", new ChatMessage(ChatRole.User, "Reply with exactly this word: LOCAL_READY"));
            if (afterCancel.Trim() != "LOCAL_READY") contentFailures.Add("after-cancel-A: output differs");
            await host.DisposeAsync();
            currentProcess.Refresh();
            if (!currentProcess.HasExited || !originalProcess.HasExited) failures.Add("worker remained alive after disposal");
            observations.Add(new { stage = "worker-disposed", workerId, originalExited = originalProcess.HasExited,
                replacementWorkerId, exited = currentProcess.HasExited });
            await host.DisposeAsync();
            startupHost = new LocalModelHost(modelDirectory);
            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var startup = startupHost.GetModelAsync(startupCancellation.Token);
            int? startupPid = null;
            while (!startup.IsCompleted && (startupPid = startupHost.WorkerProcessId) is null)
                await Task.Delay(10, deadline.Token);
            if (startup.IsCompleted && startupPid is null) startupPid = startupHost.WorkerProcessId;
            var startupProcess = startupPid.HasValue ? HoldOwnedProcess(startupPid.Value) : null;
            var cancelledWhilePending = !startup.IsCompleted;
            startupCancellation.Cancel();
            var startupCancelled = false;
            try { await startup; }
            catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { startupCancelled = true; }
            await startupHost.DisposeAsync();
            var startupWorkerExited = true;
            if (startupProcess is not null)
            {
                startupProcess.Refresh();
                startupWorkerExited = startupProcess.HasExited;
            }
            observations.Add(new { stage = "cancel-startup", startupPid, cancelledWhilePending, startupCancelled, startupWorkerExited });
            if (startupPid is null || !cancelledWhilePending || !startupCancelled || !startupWorkerExited)
                failures.Add("cancel-startup: spawned, pending cancellation and cleanup not all observed");
        }
        catch (Exception exception)
        {
            failures.Add("execution: " + exception.GetType().Name);
            throw;
        }
        finally
        {
            try { if (host is not null) await host.DisposeAsync(); }
            catch (Exception exception) { failures.Add("host cleanup: " + exception.GetType().Name); }
            try { if (startupHost is not null) await startupHost.DisposeAsync(); }
            catch (Exception exception) { failures.Add("startup cleanup: " + exception.GetType().Name); }
            foreach (var ownedProcess in ownedProcesses)
            {
                try
                {
                    ownedProcess.Refresh();
                    var exited = ownedProcess.HasExited;
                    observations.Add(new { stage = "owned-handle-cleanup", workerId = ownedProcess.Id, exited });
                    if (!exited) failures.Add("a known owned worker remained alive after final cleanup");
                }
                catch (Exception exception) { failures.Add("owned worker cleanup verification: " + exception.GetType().Name); }
                finally { ownedProcess.Dispose(); }
            }
            status = failures.Count == 0 ? "PASS" : "FAIL";
            try
            {
                Directory.CreateDirectory(evidenceDirectory);
                await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "local-model-smoke.json"),
                    JsonSerializer.Serialize(new { schemaVersion = 2, status, failures, contentFailures, manualContentChecks,
                        workerId, replacementWorkerId,
                        totalSeconds = watch.Elapsed.TotalSeconds, sampledPeakPrivateBytes = peakPrivate, observations },
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { throw new IOException("Local model evidence could not be saved."); }
        }
        if (failures.Count != 0) throw new InvalidOperationException("Local model checks failed; all observations retained.");
    }
}
