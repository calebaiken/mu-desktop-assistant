using System.Diagnostics;
using System.Security.Cryptography;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Lazy one-operation capture supervision. Only CaptureAsync admits an explicit microphone operation.</summary>
public sealed class CaptureAudioInput : IAudioCapture
{
    private readonly Func<ICaptureWorkerConnection> startWorker;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? active;
    private Task? disposal;
    private bool disposed;
    private bool poisoned;
    private int? workerId;
    public int? ActiveWorkerId { get { lock (sync) return workerId; } }

    public CaptureAudioInput(string executablePath)
    {
        VoiceWorkerProcess.ValidateLaunch(executablePath, "--capture-worker", 1024);
        startWorker = () => new CaptureWorkerConnection(VoiceWorkerProcess.Start(executablePath, "--capture-worker"));
    }

    internal CaptureAudioInput(Func<ICaptureWorkerConnection> startWorker) =>
        this.startWorker = startWorker ?? throw new ArgumentNullException(nameof(startWorker));

    public async Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        (await RunAsync(new(1, Guid.NewGuid(), "devices", null), null, CancellationToken.None, cancellationToken).ConfigureAwait(false)).Devices!;

    public async Task<Pcm16Audio> CaptureAsync(AudioCaptureRequest request, IProgress<AudioCaptureState>? progress,
        CancellationToken stopToken, CancellationToken cancellationToken = default)
    {
        AudioInputLimits.Validate(request);
        return (await RunAsync(new(1, request.SessionId, "capture", request), progress, stopToken, cancellationToken).ConfigureAwait(false)).Audio!;
    }

    private async Task<OperationResult> RunAsync(CaptureCommand command, IProgress<AudioCaptureState>? progress,
        CancellationToken stopToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource completion;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (poisoned) throw new AudioInputException(AudioInputFailure.CleanupFailed);
            if (active is not null) throw new AudioInputException(AudioInputFailure.Busy);
            active = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            if (command.Kind == "capture" && stopToken.IsCancellationRequested)
                return new(null, Pcm16Audio.TakeOwnership([], 0));
            return await Task.Run(() => ExecuteAsync(command, progress, stopToken, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            lock (sync) { active = null; workerId = null; }
            completion.TrySetResult();
        }
    }

    private async Task<OperationResult> ExecuteAsync(CaptureCommand command, IProgress<AudioCaptureState>? progress,
        CancellationToken stopToken, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        deadline.CancelAfter(AudioInputLimits.InitializationDeadline);
        var token = deadline.Token;
        ICaptureWorkerConnection? worker = null;
        Task? errorDrain = null;
        Task? termination = null;
        Task? gracefulStop = null;
        var controlSync = new object();
        var stopAcknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admissionCompleted = false;
        var stopBeforeAdmission = false;
        var stopTimedOut = false;
        var started = false;
        var stoppedReported = false;
        var completed = false;
        var resultValidated = false;
        var sequence = 0;
        var partialFrame = false;
        var length = 0;
        var limited = false;
        byte[]? audio = command.Kind == "capture" ? new byte[AudioInputLimits.MaximumBytes] : null;
        IReadOnlyList<AudioInputDevice>? devices = null;

        Task TerminateAsync()
        {
            lock (controlSync) return termination ??= Task.Run(async () =>
            {
                ICaptureWorkerConnection? current;
                lock (controlSync) current = worker;
                if (current is not null) await current.StopAsync(AudioInputLimits.CleanupDeadline).ConfigureAwait(false);
            });
        }
        void RequestStop()
        {
            lock (controlSync)
            {
                if (gracefulStop is not null || stopBeforeAdmission) return;
                if (!admissionCompleted)
                {
                    stopBeforeAdmission = true;
                    deadline.Cancel();
                    return;
                }
                var elapsed = Stopwatch.StartNew();
                gracefulStop = Task.Run(async () =>
                {
                    try
                    {
                        TimeSpan Remaining() => AudioInputLimits.StopDeadline - elapsed.Elapsed;
                        var remaining = Remaining();
                        if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                        using var stopDeadline = new CancellationTokenSource(remaining);
                        var write = worker!.Input.WriteAsync(new byte[] { CaptureProtocol.StopCommand }, stopDeadline.Token).AsTask();
                        ObserveFault(write);
                        await write.WaitAsync(stopDeadline.Token).ConfigureAwait(false);
                        await worker.Input.FlushAsync(stopDeadline.Token).WaitAsync(stopDeadline.Token).ConfigureAwait(false);
                        await stopAcknowledged.Task.WaitAsync(stopDeadline.Token).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is TimeoutException or OperationCanceledException or IOException or ObjectDisposedException)
                    {
                        lock (controlSync) stopTimedOut = true;
                        await deadline.CancelAsync().ConfigureAwait(false);
                        await TerminateAsync().ConfigureAwait(false);
                    }
                });
                ObserveFault(gracefulStop);
            }
        }

        using var stopRegistration = command.Kind == "capture" ? stopToken.Register(RequestStop) : default;
        using var cancelRegistration = token.Register(() => { _ = TerminateAsync(); });
        try
        {
            token.ThrowIfCancellationRequested();
            var launched = startWorker();
            lock (controlSync) worker = launched;
            lock (sync) workerId = worker.Id;
            token.ThrowIfCancellationRequested();
            errorDrain = DrainErrorsAsync(worker.Error, token);
            ObserveFault(errorDrain);
            await CaptureProtocol.WriteCommandAsync(worker.Input, command, token).WaitAsync(token).ConfigureAwait(false);
            lock (controlSync) admissionCompleted = true;
            token.ThrowIfCancellationRequested();
            while (!completed)
            {
                var read = CaptureProtocol.ReadEventAsync(worker.Output, token);
                ObserveFault(read);
                CaptureEvent message;
                try { message = await read.WaitAsync(token).ConfigureAwait(false); }
                catch
                {
                    // A synchronous pipe read may finish after the deadline wins. Clear its unclaimed payload.
                    _ = read.ContinueWith(late => CryptographicOperations.ZeroMemory(late.Result.Payload), CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    throw;
                }
                try
                {
                    if (message.OperationId != command.OperationId) throw CaptureProtocol.Invalid();
                    switch (message.Kind)
                    {
                        case CaptureEventKind.Started when command.Kind == "capture" && !started && sequence == 0:
                            started = true;
                            deadline.CancelAfter(AudioInputLimits.CaptureDuration + AudioInputLimits.StopDeadline);
                            if (!stopToken.IsCancellationRequested && !token.IsCancellationRequested) progress?.Report(AudioCaptureState.Started);
                            break;
                        case CaptureEventKind.Pcm when command.Kind == "capture" && started && !partialFrame && message.Sequence == sequence + 1:
                            if (message.Payload.Length > AudioInputLimits.MaximumBytes - length) throw new AudioInputException(AudioInputFailure.Overflow);
                            message.Payload.CopyTo(audio!, length);
                            length += message.Payload.Length;
                            sequence++;
                            partialFrame = message.Payload.Length != AudioInputLimits.FrameBytes;
                            break;
                        case CaptureEventKind.Stopped when command.Kind == "capture" && message.Sequence == sequence && (started || stopToken.IsCancellationRequested):
                            limited = message.Payload[0] == 1;
                            if ((!started && (length != 0 || limited)) || (length == AudioInputLimits.MaximumBytes && !limited)) throw CaptureProtocol.Invalid();
                            completed = true;
                            stopAcknowledged.TrySetResult();
                            if (started) { progress?.Report(AudioCaptureState.Stopped); stoppedReported = true; }
                            deadline.CancelAfter(AudioInputLimits.CleanupDeadline);
                            break;
                        case CaptureEventKind.Devices when command.Kind == "devices" && !started && sequence == 0:
                            devices = CaptureProtocol.ParseDevices(message.Payload);
                            completed = true;
                            deadline.CancelAfter(AudioInputLimits.CleanupDeadline);
                            break;
                        case CaptureEventKind.Failed when message.Sequence == sequence:
                            throw new AudioInputException(CaptureProtocol.ParseFailure(message.Payload));
                        default: throw CaptureProtocol.Invalid();
                    }
                }
                finally { CryptographicOperations.ZeroMemory(message.Payload); }
            }
            worker.Input.Close();
            var tail = new byte[1];
            var tailRead = worker.Output.ReadAsync(tail, token).AsTask();
            ObserveFault(tailRead);
            if (await tailRead.WaitAsync(token).ConfigureAwait(false) != 0) throw CaptureProtocol.Invalid();
            await worker.WaitForExitAsync(token).WaitAsync(token).ConfigureAwait(false);
            if (errorDrain is not null) await errorDrain.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (worker.ExitCode != 0) throw new AudioInputException(AudioInputFailure.Unavailable);
            resultValidated = true;
        }
        catch (Exception error) when (stopBeforeAdmission && !cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested &&
            error is OperationCanceledException or IOException or AudioInputException { Failure: not AudioInputFailure.CleanupFailed })
        {
            resultValidated = true;
            length = 0;
        }
        catch (Exception error) when (token.IsCancellationRequested && error is not OutOfMemoryException and not StackOverflowException)
        {
            if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : lifetime.Token);
            throw new AudioInputException(AudioInputFailure.Timeout);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { throw new AudioInputException(AudioInputFailure.Unavailable); }
        finally
        {
            var cleanupSucceeded = false;
            try
            {
                // A failure is not a native Stop acknowledgment; its bounded stop path still has to finish.
                if (gracefulStop is not null) await gracefulStop.ConfigureAwait(false);
                if (worker is not null)
                {
                    // An early cancellation may have observed no worker before Start returned.
                    await worker.StopAsync(AudioInputLimits.CleanupDeadline).ConfigureAwait(false);
                    if (!worker.HasExited) throw new AudioInputException(AudioInputFailure.CleanupFailed);
                    await worker.DisposeAsync().ConfigureAwait(false);
                }
                if (termination is not null) await termination.ConfigureAwait(false);
                cleanupSucceeded = true;
                if (started && !stoppedReported) progress?.Report(AudioCaptureState.Stopped);
            }
            catch (AudioInputException error) when (error.Failure == AudioInputFailure.CleanupFailed)
            {
                lock (sync) poisoned = true;
                throw;
            }
            finally
            {
                if (audio is not null && (!resultValidated || !cleanupSucceeded || token.IsCancellationRequested || stopTimedOut))
                    CryptographicOperations.ZeroMemory(audio);
            }
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            lifetime.Token.ThrowIfCancellationRequested();
            if (stopTimedOut) throw new AudioInputException(AudioInputFailure.Timeout);
            return new(devices, audio is null ? null : Pcm16Audio.TakeOwnership(audio, length, limited));
        }
        catch { if (audio is not null) CryptographicOperations.ZeroMemory(audio); throw; }
    }

    private static async Task DrainErrorsAsync(Stream stream, CancellationToken token)
    {
        var bytes = new byte[1024];
        var total = 0;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(bytes, token).ConfigureAwait(false);
                if (read == 0) return;
                total += read;
                CryptographicOperations.ZeroMemory(bytes);
                if (total > 64 * 1024) throw CaptureProtocol.Invalid();
            }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void ObserveFault(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
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
            catch (TimeoutException) { lock (sync) poisoned = true; throw new AudioInputException(AudioInputFailure.CleanupFailed); }
        }
        lifetime.Dispose();
    }

    private sealed record OperationResult(IReadOnlyList<AudioInputDevice>? Devices, Pcm16Audio? Audio);
}
