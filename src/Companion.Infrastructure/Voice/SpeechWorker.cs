using System.Speech.Synthesis;
using System.Runtime.InteropServices;
using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

/// <summary>Early entry point for the exact private --speech-worker desktop mode.</summary>
public static class SpeechWorker
{
    public static async Task<int> RunAsync()
    {
        // Never fall back to a terminal or file. The parent supplies private redirected pipes.
        if (!Console.IsInputRedirected || !Console.IsOutputRedirected || !HasPrivatePipes()) return 2;
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        using var deadline = new CancellationTokenSource(SpeechLimits.OperationDeadline);
        SpeechCommand? command = null;
        try
        {
            var readCommand = SpeechProtocol.ReadFrameAsync(input, SpeechProtocol.MaximumCommandBytes, deadline.Token);
            ObserveFault(readCommand);
            var bytes = await readCommand.WaitAsync(deadline.Token).ConfigureAwait(false);
            try { command = SpeechProtocol.ParseCommand(bytes); }
            finally { Array.Clear(bytes); }
            var writer = new WorkerWriter(output, command.OperationId);
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { writer.Send(RunOperation(command, writer, deadline.Token)); completion.TrySetResult(0); }
                catch (OperationCanceledException) { completion.TrySetResult(1); }
                catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
                {
                    try { writer.Send(new SpeechEvent(1, command.OperationId, "failed", Failure:
                        error is SpeechException speech ? speech.Failure : SpeechFailure.Unavailable)); }
                    catch (Exception failure) when (failure is IOException or ObjectDisposedException or SpeechException) { }
                    completion.TrySetResult(1);
                }
            }) { IsBackground = true, Name = "Local speech operation" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Parent retains stdin until cleanup. EOF or any second command cancels the operation.
            // If native COM is stuck, returning ends this one-operation worker process at the host.
            var eof = ObserveInputEndAsync(input, writer, deadline);
            try { return await completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false); }
            finally
            {
                await deadline.CancelAsync().ConfigureAwait(false);
                ObserveFault(eof);
                try { await eof.WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        catch (Exception error) when (error is OperationCanceledException or IOException or SpeechException or ObjectDisposedException)
        { return 2; }
    }

    private static async Task ObserveInputEndAsync(Stream input, WorkerWriter writer, CancellationTokenSource cancellation)
    {
        try
        {
            var next = new byte[1];
            _ = await input.ReadAsync(next, cancellation.Token).ConfigureAwait(false);
            if (!writer.TerminalSent) await cancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (IOException) { if (!writer.TerminalSent) await cancellation.CancelAsync().ConfigureAwait(false); }
    }

    private static SpeechEvent RunOperation(SpeechCommand command, WorkerWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var synthesizer = new SpeechSynthesizer();
        // Constructor/enumeration collect registrations; they do not select a TTS proxy engine.
        var tokens = synthesizer.GetInstalledVoices();
        if (tokens.Count > SpeechVoicePolicy.MaximumRegistrations) throw new SpeechException(SpeechFailure.Unavailable);
        var registrations = tokens.Select(item => new SpeechVoiceRegistration(item.VoiceInfo.Id,
            item.VoiceInfo.Name, item.VoiceInfo.Culture?.Name, item.VoiceInfo.AdditionalInfo
                .FirstOrDefault(pair => string.Equals(pair.Key, "Vendor", StringComparison.OrdinalIgnoreCase)).Value,
            item.Enabled)).ToArray();
        var installed = SpeechVoicePolicy.GetEligible(registrations);
        if (command.Kind == "voices")
        {
            return new SpeechEvent(1, command.OperationId, "voices", installed);
        }
        var request = command.Request!;
        var voice = SpeechVoicePolicy.AdmitSelection(registrations, request.VoiceId);
        // This changes only this synthesizer's in-memory selection flags. It writes no registry
        // settings. All enabled engines passed admission before these setters can load a default.
        foreach (var item in tokens)
            if (!string.Equals(item.VoiceInfo.Id, voice.Id, StringComparison.Ordinal) && item.Enabled) item.Enabled = false;
        synthesizer.SelectVoice(voice.Name);
        if (!string.Equals(synthesizer.Voice.Id, voice.Id, StringComparison.Ordinal))
            throw new SpeechException(SpeechFailure.Unavailable);
        synthesizer.Rate = request.Rate;
        using var audio = command.Kind == "probe" ? new CappedAudioStream() : null;
        if (audio is not null) synthesizer.SetOutputToWaveStream(audio);
        else synthesizer.SetOutputToDefaultAudioDevice();

        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var completed = 0;
        var failed = 0;
        EventHandler<SpeakStartedEventArgs> onStarted = (_, _) =>
        {
            if (audio is not null || cancellationToken.IsCancellationRequested || Interlocked.Exchange(ref started, 1) != 0) return;
            try { writer.Send(new SpeechEvent(1, command.OperationId, "speaking")); }
            catch (Exception error) when (error is IOException or ObjectDisposedException or SpeechException)
            { Interlocked.Exchange(ref failed, 1); finished.TrySetResult(); }
        };
        EventHandler<SpeakCompletedEventArgs> onCompleted = (_, args) =>
        {
            if (Interlocked.Exchange(ref completed, 1) != 0) return;
            if (args.Cancelled || args.Error is not null) Interlocked.Exchange(ref failed, 1);
            finished.TrySetResult();
        };
        synthesizer.SpeakStarted += onStarted;
        synthesizer.SpeakCompleted += onCompleted;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            synthesizer.SpeakAsync(request.Text); // Plain text only. Never SpeakSsml or an external lexicon.
            finished.Task.Wait(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref failed) != 0 || Volatile.Read(ref completed) == 0)
                throw new SpeechException(SpeechFailure.Unavailable);
            // Detach the destination before reading its finalized RIFF header; the stream stays ours.
            synthesizer.SetOutputToNull();
            if (audio is not null)
                return new SpeechEvent(1, command.OperationId, "probe", Probe: SpeechWave.Inspect(audio.WrittenSpan, voice));
            else
            {
                if (Volatile.Read(ref started) == 0) throw new SpeechException(SpeechFailure.Unavailable);
                return new SpeechEvent(1, command.OperationId, "completed");
            }
        }
        finally
        {
            synthesizer.SpeakStarted -= onStarted;
            synthesizer.SpeakCompleted -= onCompleted;
            // Native cleanup may block. This is a background STA in an owned, bounded-lifetime child.
            if (cancellationToken.IsCancellationRequested) synthesizer.SpeakAsyncCancelAll();
        }
    }

    private sealed class WorkerWriter(Stream output, Guid operationId)
    {
        private readonly object sync = new();
        private bool terminalSent;
        internal bool TerminalSent { get { lock (sync) return terminalSent; } }
        internal void Send(SpeechEvent value)
        {
            lock (sync)
            {
                if (value.OperationId != operationId) throw SpeechProtocol.Invalid();
                SpeechProtocol.WriteFrameAsync(output, SpeechProtocol.SerializeEvent(value),
                    SpeechProtocol.MaximumEventBytes, CancellationToken.None).GetAwaiter().GetResult();
                if (value.Kind != "speaking") terminalSent = true;
            }
        }
    }

    private static bool HasPrivatePipes() => new[] { -10, -11, -12 }.All(id => GetFileType(GetStdHandle(id)) == 3);

    private static void ObserveFault(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr file);
}
