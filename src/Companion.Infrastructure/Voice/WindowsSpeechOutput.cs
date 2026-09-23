using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Companion.Application.Voice;
using Companion.Infrastructure.Conversation;

namespace Companion.Infrastructure.Voice;

/// <summary>Lazy, one-operation speech workers using this desktop executable and private pipes.</summary>
public sealed class WindowsSpeechOutput : ISpeechOutput
{
    private readonly string executablePath;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? active;
    private Task? disposal;
    private bool disposed;
    private bool cleanupFailed;
    private int? activeWorkerId;

    public WindowsSpeechOutput(string executablePath)
    {
        var path = Path.GetFullPath(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new SpeechException(SpeechFailure.InvalidRequest);
        this.executablePath = path;
    }

    /// <summary>Content-free identity of the held child, for explicit lifecycle diagnostics.</summary>
    public int? ActiveWorkerId { get { lock (sync) return activeWorkerId; } }

    public async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("voices", null, null, cancellationToken).ConfigureAwait(false);
        return result.Voices!;
    }

    public async Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress,
        CancellationToken cancellationToken = default)
    {
        SpeechLimits.Validate(request);
        await RunAsync("speak", request, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicit inaudible diagnostic. Returns metadata, never synthesized bytes or text.</summary>
    public async Task<SpeechAudioProbe> SynthesizeProbeAsync(SpeechRequest request,
        CancellationToken cancellationToken = default)
    {
        SpeechLimits.Validate(request);
        return (await RunAsync("probe", request, null, cancellationToken).ConfigureAwait(false)).Probe!;
    }

    private async Task<SpeechEvent> RunAsync(string kind, SpeechRequest? request,
        IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource completion;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (cleanupFailed) throw new SpeechException(SpeechFailure.CleanupFailed);
            if (active is not null) throw new SpeechException(SpeechFailure.Busy);
            active = completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            // Process startup, pipe setup and native teardown never execute on the caller's dispatcher.
            return await Task.Run(() => ExecuteAsync(kind, request, progress, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            lock (sync) { active = null; activeWorkerId = null; }
            completion.TrySetResult();
        }
    }

    private async Task<SpeechEvent> ExecuteAsync(string kind, SpeechRequest? request,
        IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(SpeechLimits.OperationDeadline);
        var token = deadline.Token;
        Process? worker = null;
        LocalModelJob? job = null;
        Task? errorDrain = null;
        var started = false;
        var stopSync = new object();
        Task? stopping = null;
        CancellationTokenRegistration stopRegistration = default;
        Task BeginStop()
        {
            lock (stopSync) return stopping ??= Task.Run(() => StopOwnedWorkerAsync(worker, job));
        }
        try
        {
            token.ThrowIfCancellationRequested();
            job = LocalModelJob.Create();
            worker = Process.Start(BuildStartInfo(executablePath)) ?? throw new SpeechException(SpeechFailure.Unavailable);
            _ = worker.SafeHandle; // Hold this process identity; never reopen or kill a PID selected by name.
            // No content is sent until assignment succeeds. There is still a launch/assignment gap;
            // stdin EOF and the worker startup deadline mitigate it, not suspended-start guarantees.
            job.Assign(worker);
            lock (sync) activeWorkerId = worker.Id;
            // Cancellation actively terminates the owned worker, even if a pipe/native read does
            // not promptly honor its token. The token callback only queues cleanup off the caller.
            stopRegistration = token.Register(() => { _ = BeginStop(); });
            token.ThrowIfCancellationRequested();
            errorDrain = DrainErrorsAsync(worker.StandardError.BaseStream, token);
            var operationId = Guid.NewGuid();
            var command = SpeechProtocol.SerializeCommand(new SpeechCommand(1, operationId, kind, request));
            try { await SpeechProtocol.WriteFrameAsync(worker.StandardInput.BaseStream, command, SpeechProtocol.MaximumCommandBytes, token).ConfigureAwait(false); }
            finally { CryptographicOperations.ZeroMemory(command); }
            SpeechEvent? terminal = null;
            for (var count = 0; count < 2; count++)
            {
                var bytes = await SpeechProtocol.ReadFrameAsync(worker.StandardOutput.BaseStream,
                    SpeechProtocol.MaximumEventBytes, token).ConfigureAwait(false);
                var item = SpeechProtocol.ParseEvent(bytes);
                if (item.OperationId != operationId) throw SpeechProtocol.Invalid();
                if (item.Kind == "speaking")
                {
                    if (kind != "speak" || started || count != 0) throw SpeechProtocol.Invalid();
                    token.ThrowIfCancellationRequested();
                    started = true;
                    progress?.Report(SpeechOutputState.Speaking);
                    continue;
                }
                terminal = item;
                break;
            }
            if (terminal is null || (terminal.Kind != "failed" && terminal.Kind !=
                    (kind switch { "voices" => "voices", "probe" => "probe", _ => "completed" })) ||
                (kind == "speak" && terminal.Kind == "completed" && !started)) throw SpeechProtocol.Invalid();
            // EOF is the only permitted tail. Closing stdin also lets the worker's EOF watcher finish.
            worker.StandardInput.BaseStream.Close();
            var extra = new byte[1];
            if (await worker.StandardOutput.BaseStream.ReadAsync(extra, token).ConfigureAwait(false) != 0)
                throw SpeechProtocol.Invalid();
            await worker.WaitForExitAsync(token).ConfigureAwait(false);
            if (errorDrain is not null) await errorDrain.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (terminal.Kind == "failed") throw new SpeechException(terminal.Failure!.Value);
            if (worker.ExitCode != 0) throw new SpeechException(SpeechFailure.Unavailable);
            return terminal;
        }
        catch (Exception error) when (token.IsCancellationRequested && error is not OutOfMemoryException and not StackOverflowException)
        {
            if (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
                throw new SpeechException(SpeechFailure.Timeout);
            throw new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : lifetime.Token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or NotSupportedException)
        { throw new SpeechException(SpeechFailure.Unavailable); }
        finally
        {
            try { await BeginStop().ConfigureAwait(false); }
            catch (SpeechException error) when (error.Failure == SpeechFailure.CleanupFailed)
            {
                lock (sync) cleanupFailed = true;
                throw;
            }
            finally
            {
                stopRegistration.Dispose();
                if (errorDrain is not null)
                {
                    // A terminated owned child closes the pipe; observe disposal/cancellation faults.
                    _ = errorDrain.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                if (kind == "speak") progress?.Report(SpeechOutputState.Stopped);
            }
        }
    }

    internal static ProcessStartInfo BuildStartInfo(string path)
    {
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(path)!
        };
        start.ArgumentList.Add("--speech-worker");
        start.Environment.Clear();
        // These are OS/runtime locations only. Provider credentials, proxies and arbitrary runtime
        // startup hooks are not inherited. No user speech text is put in the environment or argv.
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "USERPROFILE", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Environment.SystemDirectory;
        return start;
    }

    private static async Task DrainErrorsAsync(Stream stream, CancellationToken token)
    {
        var discard = new byte[1024];
        var total = 0;
        while (true)
        {
            var length = await stream.ReadAsync(discard, token).ConfigureAwait(false);
            if (length == 0) return;
            total += length;
            Array.Clear(discard);
            if (total > 64 * 1024) throw SpeechProtocol.Invalid();
        }
    }

    private static async Task StopOwnedWorkerAsync(Process? worker, LocalModelJob? job)
    {
        var watch = Stopwatch.StartNew();
        var failure = false;
        try
        {
            job?.Dispose(); // Kill-on-close applies only to this private job.
            if (worker is not null)
            {
                try
                {
                    if (!worker.HasExited) worker.Kill(entireProcessTree: false);
                    var remaining = SpeechLimits.CleanupDeadline - watch.Elapsed;
                    if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                    await worker.WaitForExitAsync().WaitAsync(remaining).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
                catch (Exception error) when (error is Win32Exception or TimeoutException or NotSupportedException)
                { failure = true; }
            }
        }
        finally { worker?.Dispose(); }
        if (failure) throw new SpeechException(SpeechFailure.CleanupFailed);
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposal is not null) return new ValueTask(disposal);
            disposed = true;
            return new ValueTask(disposal = DisposeCoreAsync(active?.Task));
        }
    }

    private async Task DisposeCoreAsync(Task? operation)
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        if (operation is null) return;
        try { await operation.WaitAsync(SpeechLimits.CleanupDeadline).ConfigureAwait(false); }
        catch (TimeoutException) { throw new SpeechException(SpeechFailure.CleanupFailed); }
    }
}
