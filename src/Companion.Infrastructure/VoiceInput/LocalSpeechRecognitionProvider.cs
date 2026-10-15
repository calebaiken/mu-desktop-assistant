using System.Security.Cryptography;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Lazy, offline recognition. Each operation owns one child and leases the exact pinned assets.</summary>
public sealed class LocalSpeechRecognitionProvider : ISpeechToTextProvider
{
    private readonly string executablePath;
    private readonly string assetDirectory;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? active;
    private Task? disposal;
    private bool disposed;
    private bool cleanupFailed;
    private int? activeWorkerId;

    public LocalSpeechRecognitionProvider(string executablePath, string assetDirectory)
    {
        VoiceWorkerProcess.ValidateLaunch(executablePath, "--recognition-worker", 1024);
        this.executablePath = Path.GetFullPath(executablePath);
        this.assetDirectory = RecognitionAssets.ValidateDirectory(assetDirectory);
    }

    /// <summary>Content-free diagnostic identity; termination always uses the original held handle.</summary>
    public int? ActiveWorkerId { get { lock (sync) return activeWorkerId; } }

    public async Task<SpeechRecognitionResult> TranscribeAsync(Pcm16Audio audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (audio is null || audio.ByteLength is <= 0 or > AudioInputLimits.MaximumBytes || (audio.ByteLength & 1) != 0)
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
        TaskCompletionSource completion;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (cleanupFailed) throw new AudioInputException(AudioInputFailure.CleanupFailed);
            if (active is not null) throw new AudioInputException(AudioInputFailure.Busy);
            active = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try { return await Task.Run(() => ExecuteAsync(audio, cancellationToken)).ConfigureAwait(false); }
        finally
        {
            lock (sync) { active = null; activeWorkerId = null; }
            completion.TrySetResult();
        }
    }

    private async Task<SpeechRecognitionResult> ExecuteAsync(Pcm16Audio audio, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(AudioInputLimits.RecognitionDeadline);
        var token = deadline.Token;
        VoiceWorkerProcess? worker = null;
        RecognitionAssets? assets = null;
        byte[]? pcm = null;
        Task? drain = null;
        CancellationTokenRegistration stopRegistration = default;
        try
        {
            token.ThrowIfCancellationRequested();
            pcm = audio.Bytes.ToArray();
            assets = await RecognitionAssets.OpenAsync(assetDirectory, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            worker = VoiceWorkerProcess.Start(executablePath, "--recognition-worker", 1024);
            lock (sync) activeWorkerId = worker.Id;
            var heldWorker = worker;
            stopRegistration = token.Register(() => ObserveFault(heldWorker.StopAsync(AudioInputLimits.CleanupDeadline)));
            token.ThrowIfCancellationRequested();
            drain = DrainErrorsAsync(worker.Error, token);
            ObserveFault(drain);
            var operationId = Guid.NewGuid();
            var command = RecognitionProtocol.SerializeCommand(new(1, operationId, assetDirectory,
                AudioInputLimits.SampleRate, 1, 16, pcm.Length / 2));
            try { await RecognitionProtocol.WriteFrameAsync(worker.Input, command, RecognitionProtocol.MaximumJsonBytes, token).ConfigureAwait(false); }
            finally { CryptographicOperations.ZeroMemory(command); }
            await RecognitionProtocol.WriteFrameAsync(worker.Input, pcm, AudioInputLimits.MaximumBytes, token).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(pcm);
            var first = await ReadEventAsync(worker.Output, token).ConfigureAwait(false);
            if (first.OperationId != operationId || first.Kind is not ("ready" or "failed")) throw RecognitionProtocol.Invalid();
            var terminal = first.Kind == "ready" ? await ReadEventAsync(worker.Output, token).ConfigureAwait(false) : first;
            if (terminal.OperationId != operationId || terminal.Kind is not ("completed" or "failed")) throw RecognitionProtocol.Invalid();
            worker.Input.Close();
            var extra = new byte[1];
            try
            {
                if (await worker.Output.ReadAsync(extra, token).ConfigureAwait(false) != 0) throw RecognitionProtocol.Invalid();
            }
            finally { CryptographicOperations.ZeroMemory(extra); }
            await worker.WaitForExitAsync(token).ConfigureAwait(false);
            await drain.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (terminal.Kind == "failed") throw new AudioInputException(terminal.Failure!.Value);
            if (worker.ExitCode != 0) throw new AudioInputException(AudioInputFailure.Unavailable);
            return terminal.Result!;
        }
        catch (Exception error) when (token.IsCancellationRequested && error is not OutOfMemoryException and not StackOverflowException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lifetime.Token.ThrowIfCancellationRequested();
            throw new AudioInputException(AudioInputFailure.Timeout);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException or InvalidOperationException)
        { throw new AudioInputException(AudioInputFailure.Unavailable); }
        finally
        {
            stopRegistration.Dispose();
            if (pcm is not null) CryptographicOperations.ZeroMemory(pcm);
            try
            {
                if (worker is not null) await worker.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                lock (sync) cleanupFailed = true;
                throw new AudioInputException(AudioInputFailure.CleanupFailed);
            }
            finally
            {
                assets?.Dispose();
                if (drain is not null) ObserveFault(drain);
            }
        }
    }

    private static async Task<RecognitionEvent> ReadEventAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = await RecognitionProtocol.ReadFrameAsync(stream, RecognitionProtocol.MaximumJsonBytes, cancellationToken).ConfigureAwait(false);
        try { return RecognitionProtocol.ParseEvent(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static async Task DrainErrorsAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var total = 0;
        try
        {
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                total += count;
                CryptographicOperations.ZeroMemory(buffer);
                if (total > 16 * 1024) throw RecognitionProtocol.Invalid();
            }
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static void ObserveFault(Task task) => _ = task.ContinueWith(value => _ = value.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            disposed = true;
            return new(disposal ??= DisposeCoreAsync(active?.Task));
        }
    }

    private async Task DisposeCoreAsync(Task? operation)
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        if (operation is not null)
        {
            try { await operation.WaitAsync(AudioInputLimits.CleanupDeadline).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                lock (sync) cleanupFailed = true;
                throw new AudioInputException(AudioInputFailure.CleanupFailed);
            }
        }
        // The source remains alive for any late cancellation continuation after failed cleanup.
    }
}
