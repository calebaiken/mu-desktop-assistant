using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Exact --recognition-worker early entry point. This child accepts one in-memory PCM operation.</summary>
public static class RecognitionWorker
{
    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows() || !PrivatePipes()) return 2;
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        using var deadline = new CancellationTokenSource(AudioInputLimits.RecognitionDeadline);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        startup.CancelAfter(AudioInputLimits.CommandDeadline);
        byte[]? pcm = null;
        try
        {
            var commandBytes = await ReadBoundedAsync(input, RecognitionProtocol.MaximumJsonBytes, startup.Token).ConfigureAwait(false);
            RecognitionCommand command;
            try { command = RecognitionProtocol.ParseCommand(commandBytes); }
            finally { CryptographicOperations.ZeroMemory(commandBytes); }
            pcm = await ReadBoundedAsync(input, AudioInputLimits.MaximumBytes, startup.Token,
                checked(command.SampleCount * 2)).ConfigureAwait(false);
            var writer = new WorkerWriter(output, command.OperationId, deadline.Token);
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownedPcm = pcm;
            pcm = null;
            var thread = new Thread(() =>
            {
                float[]? samples = null;
                try
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    using var assets = RecognitionAssets.OpenAsync(command.AssetDirectory, deadline.Token).GetAwaiter().GetResult();
                    samples = RecognitionAudio.ToFloat(ownedPcm);
                    CryptographicOperations.ZeroMemory(ownedPcm);
                    writer.Send(new(1, command.OperationId, "ready"));
                    var native = new WhisperNative(assets.RuntimeDirectory);
                    var result = native.Transcribe(assets, samples, deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    writer.Send(new(1, command.OperationId, "completed", result));
                    completion.TrySetResult(0);
                }
                catch (OperationCanceledException) { completion.TrySetResult(1); }
                catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
                {
                    try { writer.Send(new(1, command.OperationId, "failed", Failure:
                        error is AudioInputException audio ? audio.Failure : AudioInputFailure.Unavailable)); }
                    catch (Exception failure) when (failure is IOException or ObjectDisposedException or AudioInputException or OperationCanceledException) { }
                    completion.TrySetResult(1);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(ownedPcm);
                    if (samples is not null) Array.Clear(samples);
                }
            }) { IsBackground = true, Name = "Local speech recognition" };
            try { thread.Start(); }
            catch { CryptographicOperations.ZeroMemory(ownedPcm); throw; }
            var eof = ObserveInputEndAsync(input, writer, deadline);
            ObserveFault(eof);
            try { return await completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false); }
            finally { await deadline.CancelAsync().ConfigureAwait(false); }
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return 2; }
        finally { if (pcm is not null) CryptographicOperations.ZeroMemory(pcm); }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int maximum, CancellationToken cancellationToken, int? exactLength = null)
    {
        var pending = RecognitionProtocol.ReadFrameAsync(input, maximum, cancellationToken, exactLength);
        try { return await pending.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch
        {
            // Console pipes may not interrupt a synchronous OS read. If it later completes,
            // discard and clear the abandoned payload; the host exits this worker meanwhile.
            _ = pending.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion) CryptographicOperations.ZeroMemory(task.Result);
                else _ = task.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    private static async Task ObserveInputEndAsync(Stream input, WorkerWriter writer, CancellationTokenSource cancellation)
    {
        var next = new byte[1];
        try
        {
            _ = await input.ReadAsync(next, cancellation.Token).ConfigureAwait(false);
            if (!writer.TerminalSent) await cancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (IOException) { if (!writer.TerminalSent) await cancellation.CancelAsync().ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(next); }
    }

    private static void ObserveFault(Task task) => _ = task.ContinueWith(value => _ = value.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private sealed class WorkerWriter(Stream output, Guid operationId, CancellationToken cancellationToken)
    {
        private int terminal;
        internal bool TerminalSent => Volatile.Read(ref terminal) != 0;
        internal void Send(RecognitionEvent value)
        {
            if (value.OperationId != operationId || TerminalSent) throw RecognitionProtocol.Invalid();
            var bytes = RecognitionProtocol.SerializeEvent(value);
            try
            {
                if (value.Kind != "ready") Interlocked.Exchange(ref terminal, 1);
                RecognitionProtocol.WriteFrameAsync(output, bytes, RecognitionProtocol.MaximumJsonBytes,
                    cancellationToken).WaitAsync(cancellationToken).GetAwaiter().GetResult();
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    private static bool PrivatePipes() => GetFileType(GetStdHandle(-10)) == 3 &&
        GetFileType(GetStdHandle(-11)) == 3 && GetFileType(GetStdHandle(-12)) == 3;
    [DllImport("kernel32.dll")] private static extern nint GetStdHandle(int handle);
    [DllImport("kernel32.dll")] private static extern uint GetFileType(nint handle);
}
