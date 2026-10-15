using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Companion.Application.MachineLearning;
using Companion.Application.Voice;
using Companion.Infrastructure.MachineLearning;

namespace Companion.Infrastructure.Voice;

/// <summary>Optional offline CPU speech, isolated in one owned, memory-limited Python worker.</summary>
public sealed class KokoroNeuralSpeechOutput : ISpeechOutput
{
    internal const int MaximumCommandBytes = 16 * 1024;
    internal const int MaximumSmallEventBytes = 64 * 1024;
    internal const int MaximumAudioEventBytes = 6 * 1024 * 1024;
    internal const int MaximumErrorBytes = 32 * 1024;
    internal static readonly TimeSpan GenerationDeadline = TimeSpan.FromSeconds(90);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly object TemporaryOwnershipSync = new();
    private static readonly HashSet<string> OwnedTemporaryPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyList<SpeechVoice> Catalog = Array.AsReadOnly(new[]
    {
        new SpeechVoice("kokoro:af_heart", "Heart — Neural (US)", "en-US"),
        new SpeechVoice("kokoro:af_bella", "Bella — Neural (US)", "en-US"),
        new SpeechVoice("kokoro:am_michael", "Michael — Neural (US)", "en-US"),
        new SpeechVoice("kokoro:bm_george", "George — Neural (UK)", "en-GB")
    });
    private readonly string pythonPath;
    private readonly string workerPath;
    private readonly string modelDirectory;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? active;
    private Task? disposal;
    private bool disposed;
    private bool cleanupFailed;
    private int? activeWorkerId;
    private string? activeWorkerTemporaryDirectory;
    private string? lastWorkerFailure;

    public KokoroNeuralSpeechOutput(string pythonPath, string workerPath, string modelDirectory)
    {
        this.pythonPath = LocalMlLabService.ValidatePath(pythonPath);
        this.workerPath = LocalMlLabService.ValidatePath(workerPath);
        this.modelDirectory = LocalMlLabService.ValidatePath(modelDirectory);
        if (!string.Equals(Path.GetFileName(this.pythonPath), "python.exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(this.workerPath) != "worker.py")
            throw new ArgumentException("Use the configured Mu neural speech runtime and worker.");
    }

    /// <summary>Held child identity for explicit lifecycle diagnostics; no process is selected by name.</summary>
    public int? ActiveWorkerId { get { lock (sync) return activeWorkerId; } }
    /// <summary>Last validated, closed worker failure code for content-free local diagnostics.</summary>
    public string? LastWorkerFailure { get { lock (sync) return lastWorkerFailure; } }
    internal string? ActiveWorkerTemporaryDirectory { get { lock (sync) return activeWorkerTemporaryDirectory; } }

    internal static bool IsNeuralVoice(string id) => Catalog.Any(voice => voice.Id == id);

    public async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default) =>
        (await RunAsync("voices", null, null, cancellationToken).ConfigureAwait(false)).Voices!;

    public async Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await RunAsync("speak", request, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicit inaudible diagnostic. Validates the returned PCM wave before exposing its bytes.</summary>
    public async Task<SpeechSynthesisAudio> SynthesizeAudioAsync(SpeechRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        return (await RunAsync("probe", request, null, cancellationToken).ConfigureAwait(false)).Audio!;
    }

    private static void ValidateRequest(SpeechRequest? request)
    {
        SpeechLimits.Validate(request);
        if (!IsNeuralVoice(request!.VoiceId)) throw new SpeechException(SpeechFailure.InvalidRequest);
    }

    private async Task<NeuralEvent> RunAsync(string kind, SpeechRequest? request,
        IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource completion;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (cleanupFailed) throw new SpeechException(SpeechFailure.CleanupFailed);
            if (active is not null) throw new SpeechException(SpeechFailure.Busy);
            lastWorkerFailure = null;
            active = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        try
        {
            // Startup, model-path validation and native teardown run away from the UI dispatcher.
            return await Task.Run(() => ExecuteAsync(kind, request, progress, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            lock (sync) { active = null; activeWorkerId = null; activeWorkerTemporaryDirectory = null; }
            completion.TrySetResult();
        }
    }

    private async Task<NeuralEvent> ExecuteAsync(string kind, SpeechRequest? request,
        IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(kind == "voices" ? SpeechLimits.OperationDeadline : GenerationDeadline);
        var token = deadline.Token;
        Process? worker = null;
        MlWorkerJob? job = null;
        string? temporaryDirectory = null;
        Task? errorDrain = null;
        var stopSync = new object();
        Task? stopping = null;
        CancellationTokenRegistration stopRegistration = default;
        Task BeginStop()
        {
            lock (stopSync) return stopping ??= Task.Run(async () =>
            {
                var cleanupWatch = Stopwatch.StartNew();
                await StopOwnedWorkerAsync(worker, job).ConfigureAwait(false);
                // phonemizer copies its packaged eSpeak DLL into TEMP. Cancellation skips Python
                // atexit, so only this operation's private directory is cleaned after child exit.
                if (temporaryDirectory is not null) DeleteOwnedTemporaryDirectory(temporaryDirectory, cleanupWatch);
            });
        }
        try
        {
            token.ThrowIfCancellationRequested();
            LocalMlLabService.ValidatePath(pythonPath);
            LocalMlLabService.ValidatePath(workerPath);
            LocalMlLabService.ValidatePath(modelDirectory);
            if (!File.Exists(pythonPath) || !File.Exists(workerPath) || !Directory.Exists(modelDirectory))
                throw new SpeechException(SpeechFailure.Unavailable);
            temporaryDirectory = CreateOwnedTemporaryDirectory();
            lock (sync) activeWorkerTemporaryDirectory = temporaryDirectory;
            job = MlWorkerJob.Create(1536);
            var start = BuildStartInfo(pythonPath, workerPath);
            start.Environment["TEMP"] = start.Environment["TMP"] = temporaryDirectory;
            worker = Process.Start(start) ?? throw new SpeechException(SpeechFailure.Unavailable);
            _ = worker.SafeHandle;
            // The worker only reads bounded stdin before admission. No text is sent until it is in
            // this private kill-on-close, one-process job. This is not a suspended-start guarantee.
            job.Assign(worker);
            lock (sync) activeWorkerId = worker.Id;
            stopRegistration = token.Register(() => { _ = BeginStop(); });
            token.ThrowIfCancellationRequested();
            errorDrain = DrainErrorsAsync(worker.StandardError.BaseStream, job, token);
            var id = Guid.NewGuid().ToString();
            var command = JsonSerializer.SerializeToUtf8Bytes(new
            {
                protocol = 1, id, kind, modelDir = modelDirectory,
                request = request is null ? null : new { text = request.Text, voiceId = request.VoiceId, rate = request.Rate }
            });
            try
            {
                if (command.Length > MaximumCommandBytes) throw new SpeechException(SpeechFailure.InvalidRequest);
                await worker.StandardInput.BaseStream.WriteAsync(command, token).ConfigureAwait(false);
                await worker.StandardInput.BaseStream.WriteAsync("\n"u8.ToArray(), token).ConfigureAwait(false);
                await worker.StandardInput.BaseStream.FlushAsync(token).ConfigureAwait(false);
            }
            finally { CryptographicOperations.ZeroMemory(command); worker.StandardInput.Close(); }

            var reader = new BoundedLineReader(worker.StandardOutput.BaseStream);
            var first = await ReadEventAsync(reader, kind == "probe" ? MaximumAudioEventBytes : MaximumSmallEventBytes,
                id, request, token).ConfigureAwait(false);
            NeuralEvent terminal;
            if (first.Kind == "speaking")
            {
                if (kind != "speak") throw SpeechProtocol.Invalid();
                token.ThrowIfCancellationRequested();
                deadline.CancelAfter(SpeechLimits.OperationDeadline);
                ReportProgress(progress, SpeechOutputState.Speaking);
                terminal = await ReadEventAsync(reader, MaximumSmallEventBytes, id, request, token).ConfigureAwait(false);
                if (terminal.Kind is not ("completed" or "failed")) throw SpeechProtocol.Invalid();
            }
            else
            {
                terminal = first;
                if (terminal.Kind != "failed" && terminal.Kind != (kind switch { "voices" => "voices", "probe" => "probe", _ => "" }))
                    throw SpeechProtocol.Invalid();
            }
            // Exactly one terminal event and EOF are permitted; extra messages, bytes or floods fail.
            await reader.ExpectEofAsync(token).ConfigureAwait(false);
            await worker.WaitForExitAsync(token).ConfigureAwait(false);
            await errorDrain.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (terminal.Failure is { } failure)
            {
                lock (sync) lastWorkerFailure = terminal.WorkerFailure;
                throw new SpeechException(failure);
            }
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
        catch (Exception error) when (error is JsonException or DecoderFallbackException or FormatException or OverflowException)
        { throw SpeechProtocol.Invalid(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or
            InvalidOperationException or NotSupportedException or ArgumentException or MlException)
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
                    _ = errorDrain.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                job?.Dispose();
                worker?.Dispose();
                if (kind == "speak") ReportProgress(progress, SpeechOutputState.Stopped);
            }
        }
    }

    internal static ProcessStartInfo BuildStartInfo(string pythonPath, string workerPath)
    {
        var executable = LocalMlLabService.ResolveInterpreter(pythonPath);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(workerPath)!
        };
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-u"); start.ArgumentList.Add(workerPath);
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Environment.SystemDirectory;
        foreach (var name in new[] { "OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS" })
            start.Environment[name] = "2";
        foreach (var name in new[] { "HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY", "DO_NOT_TRACK" })
            start.Environment[name] = "1";
        start.Environment["PYTHONUTF8"] = "1";
        if (!string.Equals(executable, pythonPath, StringComparison.OrdinalIgnoreCase)) start.Environment["__PYVENV_LAUNCHER__"] = pythonPath;
        return start;
    }

    private static async Task<NeuralEvent> ReadEventAsync(BoundedLineReader reader, int maximum, string id,
        SpeechRequest? request, CancellationToken token)
    {
        var bytes = await reader.ReadLineAsync(maximum, token).ConfigureAwait(false);
        try { return ParseEvent(bytes, id, request); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static NeuralEvent ParseEvent(ReadOnlySpan<byte> bytes, string id, SpeechRequest? request)
    {
        try
        {
            if (bytes.Length is <= 0 or > MaximumAudioEventBytes ||
                !Guid.TryParseExact(id, "D", out var operationId) || operationId == Guid.Empty || operationId.ToString() != id)
                throw SpeechProtocol.Invalid();
            using var document = JsonDocument.Parse(StrictUtf8.GetString(bytes), new() { MaxDepth = 8 });
            var root = document.RootElement;
            ValidateJson(root);
            if (root.GetProperty("protocol").GetInt32() != 1 || root.GetProperty("id").GetString() != id)
                throw SpeechProtocol.Invalid();
            var kind = root.GetProperty("kind").GetString();
            switch (kind)
            {
                case "speaking":
                case "completed":
                    RequireFields(root, "protocol", "id", "kind");
                    return new(kind);
                case "voices":
                    RequireFields(root, "protocol", "id", "kind", "voices");
                    var voices = root.GetProperty("voices");
                    if (voices.ValueKind != JsonValueKind.Array || voices.GetArrayLength() != Catalog.Count) throw SpeechProtocol.Invalid();
                    var result = new List<SpeechVoice>();
                    foreach (var item in voices.EnumerateArray())
                    {
                        RequireFields(item, "id", "name", "culture");
                        var voice = new SpeechVoice(item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!,
                            item.GetProperty("culture").GetString()!);
                        if (!Catalog.Contains(voice) || result.Any(existing => existing.Id == voice.Id)) throw SpeechProtocol.Invalid();
                        result.Add(voice);
                    }
                    return new(kind, Array.AsReadOnly(result.ToArray()));
                case "probe":
                    RequireFields(root, "protocol", "id", "kind", "waveBase64");
                    if (request is null || !IsNeuralVoice(request.VoiceId)) throw SpeechProtocol.Invalid();
                    var encoded = root.GetProperty("waveBase64").GetString() ?? throw SpeechProtocol.Invalid();
                    if (encoded.Length is < 60 or > MaximumAudioEventBytes - 1024) throw SpeechProtocol.Invalid();
                    var wave = Convert.FromBase64String(encoded);
                    try
                    {
                        if (Convert.ToBase64String(wave) != encoded) throw SpeechProtocol.Invalid();
                        var probe = SpeechWave.Inspect(wave, Catalog.Single(voice => voice.Id == request.VoiceId));
                        if (probe.SampleRate != 24000 || probe.Channels != 1 || probe.BitsPerSample != 16 || !probe.HasNonZeroSamples)
                            throw SpeechProtocol.Invalid();
                        return new(kind, Audio: new(probe, wave));
                    }
                    catch { CryptographicOperations.ZeroMemory(wave); throw; }
                case "failed":
                    RequireFields(root, "protocol", "id", "kind", "failure");
                    var code = root.GetProperty("failure").GetString();
                    var failure = code switch
                    {
                        "invalid_request" => SpeechFailure.InvalidRequest,
                        "missing_assets" or "asset_mismatch" or "memory_limit" or "runtime_missing" or
                        "inference_failed" or "audio_limit" or "unsupported_platform" => SpeechFailure.Unavailable,
                        _ => throw SpeechProtocol.Invalid()
                    };
                    return new(kind, Failure: failure, WorkerFailure: code);
                default: throw SpeechProtocol.Invalid();
            }
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or FormatException or
            ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        { throw SpeechProtocol.Invalid(); }
    }

    private static void ValidateJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in element.EnumerateObject())
            {
                if (!names.Add(item.Name) || !SpeechLimits.IsValidText(item.Name)) throw SpeechProtocol.Invalid();
                ValidateJson(item.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateJson(item);
        else if (element.ValueKind == JsonValueKind.String && !SpeechLimits.IsValidText(element.GetString()!))
            throw SpeechProtocol.Invalid();
    }

    private static void RequireFields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != fields.Length ||
            fields.Any(field => !value.TryGetProperty(field, out _))) throw SpeechProtocol.Invalid();
    }

    private static async Task DrainErrorsAsync(Stream stream, MlWorkerJob job, CancellationToken token)
    {
        var buffer = new byte[1024];
        var total = 0;
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0) return;
                total += count;
                CryptographicOperations.ZeroMemory(buffer);
                if (total > MaximumErrorBytes) throw SpeechProtocol.Invalid();
            }
        }
        catch { job.Terminate(); throw; }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    internal static async Task StopOwnedWorkerAsync(Process? worker, MlWorkerJob? job)
    {
        var watch = Stopwatch.StartNew();
        job?.Terminate();
        if (worker is null) return;
        try
        {
            try
            {
                if (!worker.HasExited) worker.Kill(entireProcessTree: false);
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // TerminateJobObject may already be completing this child's exit. A racing Kill
                // failure is harmless only if the held process below confirms exit within the
                // original deadline. Failed or uncertain exit still poisons this provider.
            }
            var remaining = SpeechLimits.CleanupDeadline - watch.Elapsed;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException();
            await worker.WaitForExitAsync().WaitAsync(remaining).ConfigureAwait(false);
            if (!worker.HasExited) throw new TimeoutException();
        }
        catch (Exception error) when (error is Win32Exception or TimeoutException or NotSupportedException or InvalidOperationException)
        { throw new SpeechException(SpeechFailure.CleanupFailed); }
    }

    internal static string CreateOwnedTemporaryDirectory()
    {
        var parent = LocalMlLabService.ValidatePath(Path.Combine(Path.GetTempPath(), "MuNeuralSpeech"));
        Directory.CreateDirectory(parent);
        LocalMlLabService.ValidatePath(parent);
        var path = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        if (Directory.Exists(path) || File.Exists(path)) throw new IOException("The private speech temporary path already exists.");
        Directory.CreateDirectory(path);
        var result = LocalMlLabService.ValidatePath(path);
        lock (TemporaryOwnershipSync) OwnedTemporaryPaths.Add(result);
        return result;
    }

    internal static void DeleteOwnedTemporaryDirectory(string path, Stopwatch? cleanupWatch = null)
    {
        try
        {
            var watch = cleanupWatch ?? Stopwatch.StartNew();
            var parent = LocalMlLabService.ValidatePath(Path.Combine(Path.GetTempPath(), "MuNeuralSpeech"));
            var root = LocalMlLabService.ValidatePath(path);
            lock (TemporaryOwnershipSync)
                if (!OwnedTemporaryPaths.Contains(root)) throw new IOException("The speech temporary path is not owned by this process.");
            if (!string.Equals(Path.GetDirectoryName(root), parent, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(Path.GetFileName(root), "N", out var id) || id == Guid.Empty)
                throw new IOException("The private speech temporary path is invalid.");
            if (!Directory.Exists(root))
            {
                if (File.Exists(root)) throw new IOException("The private speech temporary directory was replaced.");
                lock (TemporaryOwnershipSync) OwnedTemporaryPaths.Remove(root);
                return;
            }
            var descendants = new List<string>();
            var directories = new List<string> { root };
            var pending = new Stack<string>();
            pending.Push(root);
            var prefix = root + Path.DirectorySeparatorChar;
            while (pending.TryPop(out var directory))
            {
                LocalMlLabService.ValidatePath(directory);
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (watch.Elapsed >= SpeechLimits.CleanupDeadline || descendants.Count >= 64)
                        throw new IOException("Private speech temporary cleanup exceeded its bounds.");
                    var full = LocalMlLabService.ValidatePath(entry);
                    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException();
                    descendants.Add(full);
                    if ((File.GetAttributes(full) & FileAttributes.Directory) != 0)
                    {
                        directories.Add(full);
                        pending.Push(full);
                    }
                }
            }
            foreach (var file in descendants.Except(directories, StringComparer.OrdinalIgnoreCase))
            {
                if (watch.Elapsed >= SpeechLimits.CleanupDeadline) throw new IOException();
                LocalMlLabService.ValidatePath(file);
                File.Delete(file);
            }
            // Delete checked leaves one by one; never recursively delete generic TEMP or follow links.
            for (var index = directories.Count - 1; index >= 0; index--)
            {
                if (watch.Elapsed >= SpeechLimits.CleanupDeadline) throw new IOException();
                LocalMlLabService.ValidatePath(directories[index]);
                Directory.Delete(directories[index], recursive: false);
            }
            lock (TemporaryOwnershipSync) OwnedTemporaryPaths.Remove(root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw new SpeechException(SpeechFailure.CleanupFailed); }
    }

    private static void ReportProgress(IProgress<SpeechOutputState>? progress, SpeechOutputState state)
    {
        // UI observers never own the worker's lifetime and cannot skip native cleanup.
        try { progress?.Report(state); }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { }
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposal is not null) return new(disposal);
            disposed = true;
            return new(disposal = DisposeCoreAsync(active?.Task));
        }
    }

    private async Task DisposeCoreAsync(Task? operation)
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        if (operation is null) return;
        try { await operation.WaitAsync(SpeechLimits.CleanupDeadline).ConfigureAwait(false); }
        catch (TimeoutException) { throw new SpeechException(SpeechFailure.CleanupFailed); }
    }

    internal sealed record NeuralEvent(string Kind, IReadOnlyList<SpeechVoice>? Voices = null,
        SpeechSynthesisAudio? Audio = null, SpeechFailure? Failure = null, string? WorkerFailure = null);

    private sealed class BoundedLineReader(Stream stream)
    {
        private readonly byte[] buffer = new byte[4096];
        private int offset;
        private int length;

        internal async Task<byte[]> ReadLineAsync(int maximum, CancellationToken token)
        {
            using var line = new MemoryStream();
            while (true)
            {
                if (offset == length)
                {
                    offset = 0;
                    length = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (length == 0) throw SpeechProtocol.Invalid();
                }
                var newline = Array.IndexOf(buffer, (byte)'\n', offset, length - offset);
                var count = (newline < 0 ? length : newline) - offset;
                if (line.Length + count > maximum) throw SpeechProtocol.Invalid();
                line.Write(buffer, offset, count);
                Array.Clear(buffer, offset, count);
                offset += count;
                if (newline < 0) continue;
                offset++;
                if (line.Length == 0) throw SpeechProtocol.Invalid();
                return line.ToArray();
            }
        }

        internal async Task ExpectEofAsync(CancellationToken token)
        {
            if (offset != length || await stream.ReadAsync(buffer.AsMemory(0, 1), token).ConfigureAwait(false) != 0)
                throw SpeechProtocol.Invalid();
        }
    }
}
