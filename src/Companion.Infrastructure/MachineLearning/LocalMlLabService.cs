using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Companion.Application.MachineLearning;

namespace Companion.Infrastructure.MachineLearning;

/// <summary>App-scoped bounded FIFO job queue. One disposable Python worker per action.</summary>
public sealed class LocalMlLabService : IMlLabService
{
    private readonly string pythonPath;
    private readonly string workerPath;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private readonly Dictionary<Guid, CancellationTokenSource> cancellations = [];
    private readonly List<MlJobUpdate> jobs = [];
    private readonly HashSet<Task> pending = [];
    private Task queueTail = Task.CompletedTask;
    private Task? disposal;
    private bool poisoned;
    private bool disposed;
    public string ProjectDirectory { get; }
    public event Action<MlJobUpdate>? JobChanged;
    public IReadOnlyList<MlJobUpdate> Jobs { get { lock (sync) return jobs.ToArray(); } }

    public LocalMlLabService(string pythonPath, string workerPath, string projectDirectory)
    {
        this.pythonPath = ValidatePath(pythonPath);
        this.workerPath = ValidatePath(workerPath);
        ProjectDirectory = ValidatePath(projectDirectory);
        if (!string.Equals(Path.GetFileName(this.pythonPath), "python.exe", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(this.workerPath), "worker.py", StringComparison.Ordinal))
            throw new ArgumentException("Use the configured Mu ML runtime and worker.");
    }

    public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
    {
        limits ??= new();
        MlValidation.Validate(action, limits);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (poisoned) throw new MlException("cleanup", "Restart Mu before running another ML job; previous worker cleanup was not confirmed.");
            if (pending.Count >= 16) throw new MlException("queue_full", "The job queue is full. Cancel a queued job or wait for it to finish.");
            var predecessor = queueTail;
            var task = RunAsync(action, limits, cancellationToken, predecessor);
            // A cancelled queued request must not allow its successor to overtake the active job.
            queueTail = Task.WhenAll(predecessor, task).ContinueWith(_ => { }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            pending.Add(task);
            _ = task.ContinueWith(done => { lock (sync) pending.Remove(done); }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return task;
        }
    }

    public void Cancel(Guid jobId)
    {
        lock (sync) if (cancellations.TryGetValue(jobId, out var source)) source.Cancel();
    }

    private async Task<MlResult> RunAsync(MlAction action, MlLimits limits, CancellationToken caller, Task predecessor)
    {
        // Yield ensures ExecuteAsync registers this task before it can finish.
        await Task.Yield();
        var id = Guid.NewGuid();
        var watch = Stopwatch.StartNew();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, lifetime.Token);
        lock (sync) cancellations.Add(id, cancellation);
        Publish(new(id, action.Operation, MlJobState.Queued, watch.Elapsed));
        try
        {
            await predecessor.WaitAsync(cancellation.Token).ConfigureAwait(false);
            lock (sync) if (poisoned) throw new MlException("cleanup", "Restart Mu before running another ML job.");
            Publish(new(id, action.Operation, MlJobState.Preparing, watch.Elapsed));
            if (!File.Exists(pythonPath) || !File.Exists(workerPath))
                throw new MlException("missing_runtime", "The optional ML runtime is not installed. Run scripts/setup-ml-lab.ps1 from the Mu project, then reopen the Lab.");
            ValidatePath(pythonPath); ValidatePath(workerPath); ValidatePath(ProjectDirectory);
            var request = JsonSerializer.Serialize(new { protocol = 1, id, action = action.Operation,
                projectDir = ProjectDirectory, parameters = MlActionCodec.Parameters(action), limits }, MlActionCodec.Json);
            if (Encoding.UTF8.GetByteCount(request) > 128 * 1024) throw new MlException("request_limit", "Reduce the ML configuration size.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            var result = await RunWorkerAsync(id, action.Operation, request, limits, watch, deadline.Token).ConfigureAwait(false);
            Publish(new(id, action.Operation, MlJobState.Completed, watch.Elapsed));
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { Publish(new(id, action.Operation, MlJobState.Cancelled, watch.Elapsed)); throw; }
        catch (OperationCanceledException)
        { Publish(new(id, action.Operation, MlJobState.Failed, watch.Elapsed)); throw new MlException("timeout", "The ML job exceeded ten minutes. Reduce rows or model complexity, then retry."); }
        catch (MlException)
        { Publish(new(id, action.Operation, MlJobState.Failed, watch.Elapsed)); throw; }
        catch (Exception)
        { Publish(new(id, action.Operation, MlJobState.Failed, watch.Elapsed)); throw new MlException("worker_failure", "The local ML worker could not finish. Check the runtime, available memory and project access, then retry."); }
        finally
        {
            lock (sync) cancellations.Remove(id);
        }
    }

    private async Task<MlResult> RunWorkerAsync(Guid id, string operation, string request, MlLimits limits, Stopwatch watch, CancellationToken token)
    {
        using var job = MlWorkerJob.Create(limits.MemoryMiB);
        using var process = new Process();
        var start = process.StartInfo;
        start.FileName = ResolveInterpreter(pythonPath);
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.RedirectStandardInput = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        start.StandardInputEncoding = new UTF8Encoding(false);
        start.StandardOutputEncoding = new UTF8Encoding(false, true);
        start.StandardErrorEncoding = new UTF8Encoding(false, true);
        start.WorkingDirectory = Path.GetDirectoryName(workerPath)!;
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-u"); start.ArgumentList.Add(workerPath);
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Path.Combine(systemRoot, "System32");
        foreach (var name in new[] { "OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS" })
            start.Environment[name] = limits.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["MPLBACKEND"] = "Agg";
        start.Environment["PYTHONUTF8"] = "1";
        // CPython's Windows venv redirector would create a second process. Launch the
        // configured base interpreter directly with the same venv identity instead,
        // keeping both hard ownership and resource measurements on one held process.
        if (!string.Equals(start.FileName, pythonPath, StringComparison.OrdinalIgnoreCase))
            start.Environment["__PYVENV_LAUNCHER__"] = pythonPath;
        start.Environment["MPLCONFIGDIR"] = Path.Combine(ProjectDirectory, "cache", "matplotlib");
        token.ThrowIfCancellationRequested();
        var started = false;
        using var monitoring = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task? monitor = null;
        try
        {
            started = process.Start();
            if (!started) throw new MlException("start", "The ML worker could not start.");
            // Worker bootstrap only reads stdin until this assignment has succeeded.
            job.Assign(process);
            Publish(new(id, operation, MlJobState.Running, watch.Elapsed));
            monitor = MonitorAsync(process, id, operation, watch, monitoring.Token);
            async Task<string> DrainAsync(StreamReader reader, int limit)
            {
                try { return await ReadBoundedAsync(reader, limit, token).ConfigureAwait(false); }
                catch { job.Terminate(); throw; }
            }
            var stdout = DrainAsync(process.StandardOutput, 2 * 1024 * 1024);
            var stderr = DrainAsync(process.StandardError, 64 * 1024);
            await process.StandardInput.WriteLineAsync(request.AsMemory(), token).ConfigureAwait(false);
            process.StandardInput.Close();
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(token)).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(await stdout.ConfigureAwait(false), new() { MaxDepth = 32 });
            var response = document.RootElement;
            var names = response.EnumerateObject().Select(field => field.Name).ToArray();
            if (names.Length != 4 || names.Distinct(StringComparer.Ordinal).Count() != 4 ||
                names.Any(name => name is not ("protocol" or "id" or "ok" or "result" or "error"))) throw new JsonException();
            if (response.GetProperty("protocol").GetInt32() != 1 || response.GetProperty("id").GetString() != id.ToString())
                throw new MlException("protocol", "The ML worker returned an incompatible response.");
            if (!response.GetProperty("ok").GetBoolean())
            {
                var error = response.GetProperty("error");
                var code = error.GetProperty("code").GetString() ?? "worker";
                var message = error.GetProperty("message").GetString() ?? "ML operation failed.";
                if (code.Length > 80 || message.Length > 1000) throw new JsonException();
                throw new MlException(code, message);
            }
            if (process.ExitCode != 0) throw new MlException("exit", "The ML worker exited before completion.");
            return new(id, operation, response.GetProperty("result").Clone());
        }
        finally
        {
            monitoring.Cancel();
            if (monitor is not null) { try { await monitor.ConfigureAwait(false); } catch (OperationCanceledException) { } }
            if (started)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        job.Terminate();
                        // Process retains its native handle; never reopen or terminate by process name.
                        if (!process.HasExited) process.Kill();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    lock (sync) poisoned = true;
                    throw new MlException("cleanup", "The ML worker did not stop cleanly. Restart Mu before running another job.");
                }
            }
        }
    }

    private async Task MonitorAsync(Process process, Guid id, string operation, Stopwatch watch, CancellationToken token)
    {
        var previousTime = watch.Elapsed;
        var previousCpu = TimeSpan.Zero;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            try
            {
                process.Refresh(); if (process.HasExited) return;
                var cpu = process.TotalProcessorTime; var now = watch.Elapsed;
                var percent = (cpu - previousCpu).TotalSeconds / Math.Max(.001, (now - previousTime).TotalSeconds) / Environment.ProcessorCount * 100;
                Publish(new(id, operation, MlJobState.Running, now, process.WorkingSet64, Math.Clamp(percent, 0, 100)));
                previousCpu = cpu; previousTime = now;
            }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception) { return; }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var text = new StringBuilder(); var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0) return text.ToString();
            if (text.Length + count > limit) throw new MlException("output_limit", "The ML result exceeded its display limit. Use fewer rows or features.");
            text.Append(buffer, 0, count);
        }
    }

    private void Publish(MlJobUpdate update)
    {
        lock (sync)
        {
            var index = jobs.FindIndex(item => item.Id == update.Id);
            if (index >= 0) jobs[index] = update; else jobs.Add(update);
            while (jobs.Count > 100 && jobs[0].State is MlJobState.Completed or MlJobState.Cancelled or MlJobState.Failed) jobs.RemoveAt(0);
        }
        // UI observers marshal to their Dispatcher. Their failures cannot orphan a worker.
        foreach (Action<MlJobUpdate> observer in JobChanged?.GetInvocationList() ?? [])
            try { observer(update); } catch (Exception) { }
    }

    internal static string ValidatePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal) ||
            value.Length > 2048 || value.Any(char.IsControl) || value.AsSpan(2).Contains(':'))
            throw new ArgumentException("Use an absolute local path.");
        var full = Path.GetFullPath(value);
        if (full.StartsWith("\\\\", StringComparison.Ordinal)) throw new ArgumentException("Use an absolute local path.");
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Linked ML paths are not supported.");
        return full;
    }

    internal static string ResolveInterpreter(string pythonPath)
    {
        var config = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(pythonPath))!, "pyvenv.cfg");
        if (!File.Exists(config)) return pythonPath;
        ValidatePath(config);
        if (new FileInfo(config).Length > 8192) throw new MlException("runtime", "The ML environment configuration is invalid. Recreate it with setup-ml-lab.ps1.");
        var homes = File.ReadAllLines(config).Where(line => line.StartsWith("home = ", StringComparison.Ordinal)).ToArray();
        if (homes.Length != 1) throw new MlException("runtime", "The ML environment has no unique base interpreter. Recreate it with setup-ml-lab.ps1.");
        var interpreter = ValidatePath(Path.Combine(homes[0][7..].Trim(), "python.exe"));
        if (!File.Exists(interpreter)) throw new MlException("runtime", "The ML environment's base Python is missing. Recreate it with setup-ml-lab.ps1.");
        return interpreter;
    }

    public ValueTask DisposeAsync()
    {
        lock (sync) return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Task[] remaining;
        lock (sync) { if (disposed) return; disposed = true; lifetime.Cancel(); remaining = pending.ToArray(); }
        try { await Task.WhenAll(remaining).ConfigureAwait(false); }
        catch (Exception) { /* All terminal states were reported by the individual job. */ }
        lifetime.Dispose();
        lock (sync) if (poisoned) throw new MlException("cleanup", "The ML worker did not stop cleanly. Restart Mu before opening another project.");
    }
}
