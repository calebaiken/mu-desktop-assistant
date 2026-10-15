using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Companion.Application.MachineLearning;

namespace Companion.Infrastructure.MachineLearning;

/// <summary>Optional local CPU decision model; one held, resource-limited process per request.</summary>
public sealed class LocalLayaDecisionService : ILayaDecisionService
{
    private readonly string pythonPath;
    private readonly string workerPath;
    private readonly string modelDirectory;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private Task<LayaAdvice>? active;
    private Task? disposal;
    private bool disposed;
    private bool poisoned;

    public LocalLayaDecisionService(string pythonPath, string workerPath, string modelDirectory)
    {
        this.pythonPath = LocalMlLabService.ValidatePath(pythonPath);
        this.workerPath = LocalMlLabService.ValidatePath(workerPath);
        this.modelDirectory = LocalMlLabService.ValidatePath(modelDirectory);
        if (!string.Equals(Path.GetFileName(this.pythonPath), "python.exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(this.workerPath) != "worker.py") throw new ArgumentException("Use the configured Mu LAYA runtime and worker.");
    }

    public Task<LayaAdvice> AdviseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2000 || prompt.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Describe your ML task in up to 2,000 characters.");
        _ = new UTF8Encoding(false, true).GetByteCount(prompt);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (poisoned) throw new LayaException("cleanup", "Restart Mu before using LAYA; its previous worker did not stop cleanly.");
            if (active is { IsCompleted: false }) throw new LayaException("busy", "LAYA is already analyzing a task. Stop it or wait for its result.");
            return active = RunAsync(prompt, cancellationToken);
        }
    }

    private async Task<LayaAdvice> RunAsync(string prompt, CancellationToken caller)
    {
        await Task.Yield();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, lifetime.Token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (!File.Exists(pythonPath) || !File.Exists(workerPath) || !Directory.Exists(modelDirectory))
                throw new LayaException("missing_runtime", "The optional local LAYA model is not installed. Run scripts/setup-laya.ps1, then try again. Mu's existing chat and ML controls remain available.");
            LocalMlLabService.ValidatePath(pythonPath); LocalMlLabService.ValidatePath(workerPath); LocalMlLabService.ValidatePath(modelDirectory);
            return await RunWorkerAsync(prompt, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new LayaException("timeout", "LAYA exceeded its two-minute limit. Try a shorter task description or check available memory."); }
        catch (LayaException) { throw; }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or DecoderFallbackException or FormatException or OverflowException or MlException)
        { throw new LayaException("worker_failure", "LAYA could not analyze the task. Check its local installation and available memory. No ML action ran."); }
    }

    private async Task<LayaAdvice> RunWorkerAsync(string prompt, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new { protocol = 1, id, prompt, modelDir = modelDirectory });
        using var job = MlWorkerJob.Create(3072);
        using var process = new Process();
        var start = process.StartInfo;
        start.FileName = LocalMlLabService.ResolveInterpreter(pythonPath);
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardInputEncoding = new UTF8Encoding(false);
        start.StandardOutputEncoding = start.StandardErrorEncoding = new UTF8Encoding(false, true);
        start.WorkingDirectory = Path.GetDirectoryName(workerPath)!;
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-u"); start.ArgumentList.Add(workerPath);
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
        foreach (var name in new[] { "OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS" }) start.Environment[name] = "2";
        foreach (var name in new[] { "HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY", "DO_NOT_TRACK" }) start.Environment[name] = "1";
        start.Environment["PYTHONUTF8"] = "1";
        if (!string.Equals(start.FileName, pythonPath, StringComparison.OrdinalIgnoreCase)) start.Environment["__PYVENV_LAUNCHER__"] = pythonPath;
        var started = false;
        try
        {
            token.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started) throw new LayaException("start", "The local LAYA worker could not start.");
            job.Assign(process);
            async Task<string> DrainAsync(StreamReader reader, int limit)
            {
                try { return await ReadBoundedAsync(reader, limit, token).ConfigureAwait(false); }
                catch { job.Terminate(); throw; }
            }
            var stdout = DrainAsync(process.StandardOutput, 65536);
            var stderr = DrainAsync(process.StandardError, 32768);
            await process.StandardInput.WriteLineAsync(request.AsMemory(), token).ConfigureAwait(false);
            process.StandardInput.Close();
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(token)).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(await stdout.ConfigureAwait(false), new() { MaxDepth = 8 });
            var response = document.RootElement;
            var ok = response.GetProperty("ok").GetBoolean();
            RequireFields(response, "protocol", "id", "ok", ok ? "result" : "error");
            if (response.GetProperty("protocol").GetInt32() != 1 || response.GetProperty("id").GetString() != id)
                throw new JsonException("Mismatched LAYA response.");
            if (!ok)
            {
                var error = response.GetProperty("error"); RequireFields(error, "code", "message");
                var code = BoundedText(error, "code", 80); var message = BoundedText(error, "message", 1000);
                throw new LayaException(code, message);
            }
            if (process.ExitCode != 0) throw new JsonException("Unsuccessful LAYA worker exit.");
            return ParseAdvice(response.GetProperty("result"));
        }
        finally
        {
            if (started)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        job.Terminate(); if (!process.HasExited) process.Kill();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    lock (sync) poisoned = true;
                    throw new LayaException("cleanup", "LAYA did not stop cleanly. Restart Mu before retrying.");
                }
            }
        }
    }

    internal static LayaAdvice ParseAdvice(JsonElement result)
    {
        RequireFields(result, "category", "confidence", "probabilities", "truncated", "modelRevision", "elapsedSeconds", "warnings");
        var category = BoundedText(result, "category", 32);
        if (!LayaAdvice.Categories.Contains(category, StringComparer.Ordinal)) throw new JsonException("Unknown task category.");
        var confidence = result.GetProperty("confidence").GetDouble();
        var elapsed = result.GetProperty("elapsedSeconds").GetDouble();
        var scores = result.GetProperty("probabilities"); RequireFields(scores, LayaAdvice.Categories.ToArray());
        var probabilities = scores.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetDouble(), StringComparer.Ordinal);
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1 || !double.IsFinite(elapsed) || elapsed is < 0 or > 120 ||
            probabilities.Values.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            Math.Abs(probabilities.Values.Sum() - 1) > .001 || Math.Abs(probabilities[category] - confidence) > .001 ||
            probabilities.Values.Any(value => value > confidence + .001)) throw new JsonException("Invalid LAYA category scores.");
        var revision = BoundedText(result, "modelRevision", 80);
        if (revision != "55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851") throw new JsonException("Unexpected LAYA checkpoint.");
        var warnings = result.GetProperty("warnings");
        if (warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() > 16) throw new JsonException();
        var messages = warnings.EnumerateArray().Select(item => item.GetString() ?? throw new JsonException()).ToArray();
        if (messages.Any(item => item.Length > 500 || item.Any(char.IsControl))) throw new JsonException();
        return new(category, confidence, new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(probabilities),
            result.GetProperty("truncated").GetBoolean(), revision, elapsed, Array.AsReadOnly(messages));
    }

    private static void RequireFields(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = value.EnumerateObject().Select(item => item.Name).ToArray();
        if (names.Length != expected.Length || names.Distinct(StringComparer.Ordinal).Count() != expected.Length ||
            names.Any(name => !expected.Contains(name, StringComparer.Ordinal))) throw new JsonException("Unexpected LAYA response fields.");
    }

    private static string BoundedText(JsonElement value, string key, int limit)
    {
        var text = value.GetProperty(key).GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit || text.Any(char.IsControl)) throw new JsonException();
        return text;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var text = new StringBuilder(); var buffer = new char[2048];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0) return text.ToString();
            if (text.Length + count > limit) throw new LayaException("output_limit", "LAYA exceeded its response limit. Check the local runtime.");
            text.Append(buffer, 0, count);
        }
    }

    public ValueTask DisposeAsync() { lock (sync) return new(disposal ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        Task? remaining;
        lock (sync) { if (disposed) return; disposed = true; lifetime.Cancel(); remaining = active; }
        if (remaining is not null) try { await remaining.ConfigureAwait(false); } catch (Exception) { }
        lifetime.Dispose();
    }
}
