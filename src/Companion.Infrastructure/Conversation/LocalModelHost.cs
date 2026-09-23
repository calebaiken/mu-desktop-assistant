using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using Companion.Application.Conversation;

namespace Companion.Infrastructure.Conversation;

/// <summary>Lazy, offline, loopback-only model process. No user credentials or persistent server.</summary>
public sealed class LocalModelHost : IAsyncDisposable
{
    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object disposeLock = new();
    private Task? disposal;
    private Process? worker;
    private LocalModelJob? job;
    private LocalModelFiles? files;
    private LocalChatModel? model;
    private Task[] drains = [];
    public int? WorkerProcessId
    {
        get
        {
            try { return worker is { HasExited: false } current ? current.Id : null; }
            catch (InvalidOperationException) { return null; }
        }
    }

    public LocalModelHost(string directory) => this.directory = LocalModelFiles.ValidateDirectory(directory);

    public async Task<IChatModel> GetModelAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        linked.CancelAfter(TimeSpan.FromSeconds(90));
        var token = linked.Token;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            if (model is not null && worker is { HasExited: false }) return model;
            await StopWorkerAsync().ConfigureAwait(false);
            files = await LocalModelFiles.OpenAsync(directory, token).ConfigureAwait(false);
            var port = ReservePort();
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var start = BuildStartInfo(files.RuntimeDirectory, files.ModelPath, port, secret);
            job = LocalModelJob.Create();
            worker = Process.Start(start) ?? throw new IOException("Local worker could not start.");
            // Assign immediately. Failure closes only our child, never an unrelated process.
            job.Assign(worker);
            drains = [DrainAsync(worker.StandardOutput), DrainAsync(worker.StandardError)];
            using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            var health = new Uri($"http://127.0.0.1:{port}/v1/models");
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (worker.HasExited) throw new IOException("Local worker exited before readiness.");
                if (!LocalListenerOwner.IsOwnedBy(port, worker.Id))
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    using var response = await client.GetAsync(health, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        // Verify authentication after checking the listening socket's owner.
                        using var anonymous = new HttpRequestMessage(HttpMethod.Get, health);
                        anonymous.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
                        using var rejected = await client.SendAsync(anonymous, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                        if (rejected.StatusCode != HttpStatusCode.Unauthorized || worker.HasExited ||
                            !LocalListenerOwner.IsOwnedBy(port, worker.Id))
                            throw new IOException("Local worker authentication check failed.");
                        model = new LocalChatModel(new Uri($"http://127.0.0.1:{port}/v1/chat/completions"), secret);
                        return model;
                    }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
        {
            await StopWorkerAsync().ConfigureAwait(false);
            throw new ChatModelException(ChatFailure.Timeout);
        }
        catch (OperationCanceledException) { await StopWorkerAsync().ConfigureAwait(false); throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            await StopWorkerAsync().ConfigureAwait(false);
            throw new ChatModelException(ChatFailure.Unavailable);
        }
        finally { gate.Release(); }
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    internal static ProcessStartInfo BuildStartInfo(string runtimeDirectory, string modelPath, int port, string secret)
    {
        var start = new ProcessStartInfo(Path.Combine(runtimeDirectory, "llama-server.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = runtimeDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        // Ignore inherited runtime, model, proxy and cloud configuration entirely.
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Environment.SystemDirectory;
        start.Environment["LLAMA_API_KEY"] = secret; // ephemeral local IPC guard, never a provider credential
        string[] arguments = ["--model", modelPath, "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--ctx-size", "2048", "--threads", "2", "--threads-batch", "2", "--batch-size", "128", "--ubatch-size", "128",
            "--parallel", "1", "--n-predict", "256", "--gpu-layers", "0", "--device", "none", "--poll", "0", "--poll-batch", "0",
            "--offline", "--no-webui", "--no-slots", "--no-context-shift", "--cache-ram", "0", "--log-disable",
            "--reasoning", "off", "--chat-template-kwargs", "{\"enable_thinking\":false}"];
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        try { while (await reader.ReadAsync(buffer).ConfigureAwait(false) != 0) { } }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task StopWorkerAsync()
    {
        model?.Dispose(); model = null;
        job?.Dispose(); job = null;
        var stoppingWorker = worker;
        worker = null;
        try
        {
            if (stoppingWorker is not null)
            {
                try
                {
                    if (!stoppingWorker.HasExited) stoppingWorker.Kill(entireProcessTree: true);
                    await stoppingWorker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
            }
            try { await Task.WhenAll(drains).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch (TimeoutException) { }
        }
        finally
        {
            stoppingWorker?.Dispose();
            drains = [];
            files?.Dispose(); files = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposeLock) return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try { await StopWorkerAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
}
