using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class LocalModelHostTests
{
    [TestMethod]
    public void HostRejectsNetworkAndRelativeModelPaths()
    {
        foreach (var path in new[] { "models", @"\\server\share\model", @"\\?\C:\model", "" })
            Assert.Throws<ArgumentException>(() => new LocalModelHost(path));
    }

    [TestMethod]
    public async Task CancelledReadinessNeverStartsWorkerAndDisposeCanRepeat()
    {
        using var directory = new TestDirectory();
        var host = new LocalModelHost(directory.PathValue);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.GetModelAsync(cancellation.Token));
        Assert.IsNull(host.WorkerProcessId);
        await host.DisposeAsync();
        await host.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.GetModelAsync());
    }

    [TestMethod]
    public async Task UnpinnedRuntimeIsRefusedBeforeAnyProcessStarts()
    {
        using var directory = new TestDirectory();
        Directory.CreateDirectory(Path.Combine(directory.PathValue, "runtime"));
        await File.WriteAllTextAsync(Path.Combine(directory.PathValue, "runtime", "llama-server.exe"), "not executable");
        await Assert.ThrowsAsync<IOException>(() => LocalModelFiles.OpenAsync(directory.PathValue, CancellationToken.None));
    }

    [TestMethod]
    public void LaunchArgumentsAreOfflineBoundedAndSecretNeverAppearsInCommandLine()
    {
        var runtime = Path.Combine(Path.GetTempPath(), "models with spaces", "runtime");
        var model = Path.Combine(Path.GetTempPath(), "model with spaces.gguf");
        const string secret = "test-internal-session-secret";
        var start = LocalModelHost.BuildStartInfo(runtime, model, 49152, secret);
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.IsTrue(start.RedirectStandardOutput && start.RedirectStandardError);
        CollectionAssert.Contains(start.ArgumentList.ToArray(), model);
        foreach (var flag in new[] { "--offline", "--no-webui", "--no-slots", "--log-disable", "--no-context-shift" })
            CollectionAssert.Contains(start.ArgumentList.ToArray(), flag);
        Assert.IsFalse(string.Join(" ", start.ArgumentList).Contains(secret, StringComparison.Ordinal));
        Assert.AreEqual(secret, start.Environment["LLAMA_API_KEY"]);
        var args = start.ArgumentList.ToArray();
        foreach (var (flag, expected) in new[] { ("--host", "127.0.0.1"), ("--parallel", "1"), ("--ctx-size", "2048"),
                 ("--threads", "2"), ("--n-predict", "256"), ("--cache-ram", "0"), ("--device", "none") })
            Assert.AreEqual(expected, args[Array.IndexOf(args, flag) + 1]);
        Assert.IsTrue(start.Environment.Keys.All(key => new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH", "LLAMA_API_KEY" }.Contains(key)));
    }

    [TestMethod]
    public void SocketOwnerCheckRecognizesOnlyTheActualLoopbackListenerProcess()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            Assert.IsTrue(LocalListenerOwner.IsOwnedBy(port, Environment.ProcessId));
            Assert.IsFalse(LocalListenerOwner.IsOwnedBy(port, 0));
        }
        finally { listener.Stop(); }
        Assert.IsFalse(LocalListenerOwner.IsOwnedBy(port, Environment.ProcessId));
    }

    [TestMethod]
    public async Task ClosingWorkerJobTerminatesOnlyItsAssignedChild()
    {
        using var child = new Process
        {
            StartInfo = new(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true }
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30" })
            child.StartInfo.ArgumentList.Add(argument);
        using var job = LocalModelJob.Create();
        Assert.IsTrue(child.Start());
        try
        {
            job.Assign(child);
            Assert.IsFalse(child.HasExited);
            job.Dispose();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(child.HasExited);
        }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
    }
}
