using Companion.Application.Voice;
using Companion.Infrastructure.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Parent-side admission only. These tests never start a speech child or access audio.</summary>
[TestClass]
public sealed class IndependentSpeechWorkerAdmissionTests
{
    [TestMethod]
    public void ChildLaunchHasOnlyFixedModeAndExplicitEnvironmentAllowlist()
    {
        var executable = Environment.ProcessPath!;
        var start = WindowsSpeechOutput.BuildStartInfo(executable);
        Assert.AreEqual(executable, start.FileName);
        Assert.AreEqual(Path.GetDirectoryName(executable), start.WorkingDirectory);
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.IsTrue(start.RedirectStandardInput && start.RedirectStandardOutput && start.RedirectStandardError);
        CollectionAssert.AreEqual(new[] { "--speech-worker" }, start.ArgumentList.ToArray());
        Assert.AreEqual(string.Empty, start.Arguments);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "USERPROFILE",
            "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "PATH"
        };
        Assert.IsTrue(start.Environment.Keys.All(allowed.Contains));
        Assert.AreEqual(Environment.SystemDirectory, start.Environment["PATH"]);
        foreach (var forbidden in new[] { "OPENAI_API_KEY", "HTTP_PROXY", "HTTPS_PROXY", "DOTNET_STARTUP_HOOKS", "COR_ENABLE_PROFILING" })
            Assert.IsFalse(start.Environment.ContainsKey(forbidden));
    }

    [TestMethod]
    public void ParentIdentityRejectsRelativeRemoteAndDifferentExecutableWithoutIo()
    {
        var other = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "not-the-running-executable.exe");
        foreach (var path in new[] { "relative.exe", @"\\server\share\speech.exe", "//server/share/speech.exe", other })
        {
            var error = Assert.ThrowsExactly<SpeechException>(() => new WindowsSpeechOutput(path));
            Assert.AreEqual(SpeechFailure.InvalidRequest, error.Failure);
            Assert.IsNull(error.InnerException);
            Assert.IsFalse(error.ToString().Contains(path, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task CancelledAdmissionDoesNotCreateAChildForAnyOperationKind()
    {
        await using var output = new WindowsSpeechOutput(Environment.ProcessPath!);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var request = new SpeechRequest("Public cancelled admission", "voice-id");
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.GetVoicesAsync(cancelled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.SpeakAsync(request, null, cancelled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.SynthesizeProbeAsync(request, cancelled.Token));
        Assert.IsNull(output.ActiveWorkerId);
    }

    [TestMethod]
    public async Task InvalidRequestDoesNotCreateAChildOrExposeText()
    {
        await using var output = new WindowsSpeechOutput(Environment.ProcessPath!);
        const string marker = "PRIVATE_ADMISSION_4BB2C519";
        var request = new SpeechRequest(marker + new string('x', 2000), "voice-id");
        var speak = await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(request, null));
        var probe = await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SynthesizeProbeAsync(request));
        foreach (var error in new[] { speak, probe })
        {
            Assert.AreEqual(SpeechFailure.InvalidRequest, error.Failure);
            Assert.IsNull(error.InnerException);
            Assert.IsFalse(error.ToString().Contains(marker, StringComparison.Ordinal));
        }
        Assert.IsNull(output.ActiveWorkerId);
    }

    [TestMethod]
    public async Task ConcurrentIdleDisposalIsIdempotentAndPreventsLaterStarts()
    {
        var output = new WindowsSpeechOutput(Environment.ProcessPath!);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposals = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            await output.DisposeAsync();
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(disposals).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(disposals.All(task => task.IsCompletedSuccessfully));
        Assert.IsNull(output.ActiveWorkerId);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => output.GetVoicesAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => output.SpeakAsync(new("Public later request", "voice-id"), null));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => output.SynthesizeProbeAsync(new("Public later request", "voice-id")));
        await output.DisposeAsync();
    }
}
