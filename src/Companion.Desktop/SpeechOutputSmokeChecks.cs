using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Companion.Application.Voice;
using Companion.Infrastructure.Voice;

namespace Companion.Desktop;

/// <summary>Public, explicitly selected native speech diagnostics. No audio bytes or user text are persisted.</summary>
internal static class SpeechOutputSmokeChecks
{
    private const string First = "This is the local voice playback check. Press Stop to end it.";
    private const string Second = "My fictional pet is named Maple.";

    internal static async Task RunAsync(string dataDirectory, bool playback)
    {
        var observations = new List<object>();
        var failures = new List<string>();
        var heldWorkers = new List<Process>();
        var elapsed = Stopwatch.StartNew();
        await using var output = new WindowsSpeechOutput(Path.Combine(AppContext.BaseDirectory, "Companion.Desktop.exe"));
        try
        {
            var voices = await output.GetVoicesAsync();
            observations.Add(new { stage = "voices", voices, elapsedSeconds = elapsed.Elapsed.TotalSeconds });
            if (voices.Count == 0) throw new InvalidOperationException("No enabled installed Microsoft voice was available.");
            var voice = voices[0];
            foreach (var (name, text) in new[] { ("S01", First), ("S02", Second) })
            {
                var watch = Stopwatch.StartNew();
                var probe = await output.SynthesizeProbeAsync(new(text, voice.Id));
                var expectedAlignment = checked(probe.Channels * probe.BitsPerSample / 8);
                var byteRate = checked(probe.SampleRate * expectedAlignment);
                if (expectedAlignment <= 0 || byteRate <= 0 || probe.DataBytes <= 0 ||
                    probe.DataBytes % expectedAlignment != 0 || probe.TotalBytes > 4 * 1024 * 1024 ||
                    !probe.HasNonZeroSamples || probe.DurationSeconds != (double)probe.DataBytes / byteRate)
                    throw new InvalidOperationException("Native audio metadata did not satisfy the frozen PCM bounds.");
                observations.Add(new { stage = "memory-" + name, probe, elapsedSeconds = watch.Elapsed.TotalSeconds,
                    observation = "Native bounded memory synthesis metadata; no audible output or persisted WAV." });
            }
            using (var alreadyCancelled = new CancellationTokenSource())
            {
                alreadyCancelled.Cancel();
                var cancelled = false;
                try { await output.SpeakAsync(new(First, voice.Id), null, alreadyCancelled.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                if (!cancelled) throw new InvalidOperationException("Pre-cancelled native speech was admitted.");
                observations.Add(new { stage = "pre-cancelled", cancelled });
            }

            if (playback)
            {
                using var stop = new CancellationTokenSource();
                var starts = 0;
                var stops = 0;
                var cancelWatch = new Stopwatch();
                Process? firstWorker = null;
                var progress = new CallbackProgress(state =>
                {
                    if (state == SpeechOutputState.Speaking)
                    {
                        Interlocked.Increment(ref starts);
                        firstWorker = HoldWorker(output, heldWorkers);
                        cancelWatch.Start();
                        stop.Cancel();
                    }
                    else Interlocked.Increment(ref stops);
                });
                var cancelled = false;
                try { await output.SpeakAsync(new(First, voice.Id), progress, stop.Token); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { cancelled = true; }
                cancelWatch.Stop();
                var firstWorkerId = firstWorker?.Id;
                var firstExited = firstWorker?.HasExited == true;
                if (!cancelled || starts != 1 || stops != 1 || !firstExited || cancelWatch.Elapsed > TimeSpan.FromSeconds(3))
                    throw new InvalidOperationException("Native playback cancellation or owned exit did not meet the frozen bound.");
                observations.Add(new { stage = "default-output-S01-stop", starts, stops, cancelled,
                    stopAcknowledgmentSeconds = cancelWatch.Elapsed.TotalSeconds, voice, rate = 0,
                    workerId = firstWorkerId, ownedWorkerExited = firstExited,
                    observation = "Engine events only; physical hearing was not observed by this diagnostic." });

                var events = new List<string>();
                var gate = new object();
                var watch = Stopwatch.StartNew();
                Process? secondWorker = null;
                await output.SpeakAsync(new(Second, voice.Id), new CallbackProgress(state =>
                {
                    if (state == SpeechOutputState.Speaking) secondWorker = HoldWorker(output, heldWorkers);
                    lock (gate) events.Add(state.ToString());
                }));
                var secondWorkerId = secondWorker?.Id;
                var secondExited = secondWorker?.HasExited == true;
                if (!events.SequenceEqual(new[] { nameof(SpeechOutputState.Speaking), nameof(SpeechOutputState.Stopped) }) || !secondExited)
                    throw new InvalidOperationException("Native playback reuse did not produce the expected event order and owned exit.");
                observations.Add(new { stage = "default-output-S02-reuse", voice, rate = 0, events,
                    workerId = secondWorkerId, ownedWorkerExited = secondExited,
                    elapsedSeconds = watch.Elapsed.TotalSeconds });

                Process? deadlineWorker = null;
                var deadlineEvents = new List<string>();
                var deadlineWatch = Stopwatch.StartNew();
                var timedOut = false;
                try
                {
                    await output.SpeakAsync(new(string.Concat(Enumerable.Repeat("Maple ", 330)), voice.Id, -5),
                        new CallbackProgress(state =>
                        {
                            if (state == SpeechOutputState.Speaking) deadlineWorker = HoldWorker(output, heldWorkers);
                            lock (gate) deadlineEvents.Add(state.ToString());
                        }));
                }
                catch (SpeechException error) when (error.Failure == SpeechFailure.Timeout) { timedOut = true; }
                deadlineWatch.Stop();
                var deadlineWorkerId = deadlineWorker?.Id;
                var deadlineExited = deadlineWorker?.HasExited == true;
                observations.Add(new { stage = "default-output-S04-deadline", voice, rate = -5,
                    inputCharacters = 1980, timedOut, elapsedSeconds = deadlineWatch.Elapsed.TotalSeconds,
                    events = deadlineEvents, workerId = deadlineWorkerId, ownedWorkerExited = deadlineExited });
                if (!timedOut || !deadlineExited || deadlineWatch.Elapsed < TimeSpan.FromSeconds(30) ||
                    deadlineWatch.Elapsed > TimeSpan.FromSeconds(33) ||
                    !deadlineEvents.SequenceEqual(new[] { nameof(SpeechOutputState.Speaking), nameof(SpeechOutputState.Stopped) }))
                    throw new InvalidOperationException("Native deadline did not stop the owned worker within its declared bound.");
            }
            else observations.Add(new { stage = "default-output-playback", status = "NOT_REQUESTED", reason = "Separate explicit diagnostic option required." });
        }
        catch (Exception error)
        {
            // Only public fixture stage and fixed typed error classification enter this artifact.
            failures.Add(error is SpeechException speech ? speech.Failure.ToString() : error.GetType().Name);
            throw;
        }
        finally
        {
            Exception? cleanupFailure = null;
            try { await output.DisposeAsync(); }
            catch (Exception error) { cleanupFailure = error; failures.Add("CleanupFailed"); }
            foreach (var worker in heldWorkers) worker.Dispose();
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "speech-output-smoke.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1, status = failures.Count == 0 ? "PASS" : "FAIL", failures, playbackRequested = playback,
                observations, totalSeconds = elapsed.Elapsed.TotalSeconds,
                runtime = Environment.Version.ToString(), operatingSystem = Environment.OSVersion.VersionString,
                limits = new { textCharacters = 2000, deadlineSeconds = 30, cleanupSeconds = 3, memoryBytes = 4 * 1024 * 1024 },
                physicalHearing = "UNOBSERVED", capture = "NOT_IMPLEMENTED"
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (cleanupFailure is not null) throw new InvalidOperationException("Owned speech-worker cleanup failed.", cleanupFailure);
        }
    }

    private sealed class CallbackProgress(Action<SpeechOutputState> callback) : IProgress<SpeechOutputState>
    {
        public void Report(SpeechOutputState value) => callback(value);
    }

    private static Process HoldWorker(WindowsSpeechOutput output, List<Process> heldWorkers)
    {
        var id = output.ActiveWorkerId ?? throw new InvalidOperationException("No owned speech worker identity was available.");
        var process = Process.GetProcessById(id);
        _ = process.SafeHandle;
        heldWorkers.Add(process);
        return process;
    }
}
