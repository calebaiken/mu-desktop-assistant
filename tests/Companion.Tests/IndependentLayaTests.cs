using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent protocol, authority and real owned-process boundary checks; synthetic workers do not qualify model accuracy.</summary>
[TestClass]
public sealed class IndependentLayaTests
{
    private const string Valid = """
        {"category":"regression","confidence":0.75,"probabilities":{"regression":0.75,"classification":0.05,"clustering":0.05,"reduction":0.05,"anomaly":0.05,"other":0.05},"truncated":false,"modelRevision":"55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851","elapsedSeconds":1.25,"warnings":["Scores are uncalibrated."]}
        """;

    [TestMethod]
    public void TypedAdvicePreservesDistributionAndHasNoExecutionAuthority()
    {
        using var json = JsonDocument.Parse(Valid);
        var advice = LocalLayaDecisionService.ParseAdvice(json.RootElement);
        Assert.AreEqual("regression", advice.Category);
        Assert.AreEqual(.75, advice.Confidence);
        Assert.AreEqual(1d, advice.Probabilities.Values.Sum(), .00001);
        Assert.HasCount(6, advice.Probabilities);
        Assert.IsTrue(advice.CanSuggest);
        Assert.IsFalse((advice with { Truncated = true }).CanSuggest);
        Assert.IsFalse((advice with { Confidence = .649 }).CanSuggest);
        Assert.IsFalse((advice with { Category = "other" }).CanSuggest);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IDictionary<string, double>)advice.Probabilities).Add("execute", 1));
        StringAssert.Contains(advice.Explanation, "training rows");
    }

    [TestMethod]
    public void AdviceRefusesUnknownDuplicateInconsistentAndNonfiniteValues()
    {
        var malformed = new[]
        {
            Valid.Replace("\"category\":\"regression\"", "\"category\":\"shell\""),
            Valid.Replace("\"category\":\"regression\"", "\"category\":\"regression\",\"category\":\"regression\""),
            Valid.Replace("\"confidence\":0.75", "\"confidence\":0.8"),
            Valid.Replace("\"regression\":0.75", "\"regression\":1e400"),
            Valid.Replace("\"other\":0.05", "\"other\":0.5"),
            Valid.Replace("\"elapsedSeconds\":1.25", "\"elapsedSeconds\":121"),
            Valid.Replace("55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851", "unknown"),
            Valid.Replace("\"truncated\":false", "\"truncated\":false,\"command\":\"delete\""),
            Valid.Replace("Scores are uncalibrated.", "bad\\ntext")
        };
        foreach (var raw in malformed)
        {
            using var json = JsonDocument.Parse(raw);
            Assert.ThrowsExactly<JsonException>(() => LocalLayaDecisionService.ParseAdvice(json.RootElement), raw);
        }
    }

    [TestMethod]
    public async Task MissingRuntimeAndInvalidPromptDoNotStartAnything()
    {
        using var files = new TestDirectory();
        await using var service = new LocalLayaDecisionService(files.FilePath("python.exe"), files.FilePath("worker.py"), files.PathValue);
        foreach (var prompt in new[] { "", " ", "\0", new string('x', 2001), "\ud800" })
            Assert.Throws<ArgumentException>(() => service.AdviseAsync(prompt));
        var error = await Assert.ThrowsExactlyAsync<LayaException>(() => service.AdviseAsync("Predict amplitude"));
        Assert.AreEqual("missing_runtime", error.Code);
        StringAssert.Contains(error.Message, "setup-laya.ps1");
        Assert.IsEmpty(Directory.GetFiles(files.PathValue));
    }

    [TestMethod]
    [DataRow("decimal-protocol")]
    [DataRow("overflow-protocol")]
    [DataRow("wrong-identity")]
    [DataRow("duplicate")]
    [DataRow("nonzero")]
    [DataRow("invalid-json")]
    public async Task MalformedWorkerRepliesBecomeBoundedLayaFailures(string kind)
    {
        using var files = new TestDirectory();
        await WriteWorker(files, $$$"""
            kind = "{{{kind}}}"
            if kind == "decimal-protocol": response["protocol"] = 1.5
            if kind == "overflow-protocol": response["protocol"] = 2147483648
            if kind == "wrong-identity": response["id"] = "different"
            raw = json.dumps(response)
            if kind == "duplicate": raw = raw.replace('"ok": true', '"ok": true, "ok": true')
            if kind == "invalid-json": raw = "{raw private prompt"
            print(raw, flush=True)
            if kind == "nonzero": sys.exit(3)
            """);
        await using var service = MakeService(files);
        var error = await Assert.ThrowsExactlyAsync<LayaException>(() => service.AdviseAsync("Predict amplitude").WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.AreEqual("worker_failure", error.Code);
        Assert.IsFalse(error.Message.Contains("private prompt", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("stdout", 70000)]
    [DataRow("stderr", 40000)]
    public async Task FloodingWorkerIsKilledPromptlyAndServiceCanBeReused(string stream, int bytes)
    {
        using var files = new TestDirectory();
        await WriteWorker(files, $$"""
            if request["prompt"] == "flood":
                sys.{{stream}}.write("x" * {{bytes}})
                sys.{{stream}}.flush()
                time.sleep(60)
            print(json.dumps(response), flush=True)
            """);
        await using var service = MakeService(files);
        var error = await Assert.ThrowsExactlyAsync<LayaException>(() => service.AdviseAsync("flood").WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.AreEqual("output_limit", error.Code);
        Assert.AreEqual("regression", (await service.AdviseAsync("reuse").WaitAsync(TimeSpan.FromSeconds(10))).Category);
    }

    [TestMethod]
    public async Task CancellationBusyAndSharedDisposalTerminateOnlyOwnedWorker()
    {
        using var files = new TestDirectory();
        using var otherFiles = new TestDirectory();
        const string held = """
            (project / "ready.tmp").write_text(str(os.getpid()), encoding="utf-8")
            os.replace(project / "ready.tmp", project / "ready")
            while not (project / "release").exists(): time.sleep(.01)
            print(json.dumps(response), flush=True)
            """;
        await WriteWorker(files, held); await WriteWorker(otherFiles, held);
        await using var service = MakeService(files);
        await using var other = MakeService(otherFiles);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var active = service.AdviseAsync("held", deadline.Token);
        var otherActive = other.AdviseAsync("held", deadline.Token);
        using var worker = await HoldIdentity(files, active, deadline.Token);
        using var otherWorker = await HoldIdentity(otherFiles, otherActive, deadline.Token);
        var busy = Assert.ThrowsExactly<LayaException>(() => service.AdviseAsync("second"));
        Assert.AreEqual("busy", busy.Code);
        var first = service.DisposeAsync().AsTask(); var second = service.DisposeAsync().AsTask();
        Assert.AreSame(first, second);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(8));
        await Assert.ThrowsAsync<OperationCanceledException>(() => active);
        Assert.IsTrue(worker.HasExited);
        Assert.IsFalse(otherWorker.HasExited);
        Assert.ThrowsExactly<ObjectDisposedException>(() => service.AdviseAsync("after disposal"));
        await File.WriteAllTextAsync(otherFiles.FilePath("release"), "go", deadline.Token);
        Assert.AreEqual("regression", (await otherActive.WaitAsync(TimeSpan.FromSeconds(8))).Category);
    }

    [TestMethod]
    public async Task WorkerReceivesClosedOfflineEnvironmentAndOneStdinFrame()
    {
        using var files = new TestDirectory();
        await WriteWorker(files, """
            assert sys.stdin.read() == ""
            assert set(request) == {"protocol", "id", "prompt", "modelDir"}
            assert all(os.environ[k] == "1" for k in ["HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY", "DO_NOT_TRACK"])
            assert all(os.environ[k] == "2" for k in ["OMP_NUM_THREADS", "MKL_NUM_THREADS", "OPENBLAS_NUM_THREADS", "NUMEXPR_NUM_THREADS"])
            assert "HF_TOKEN" not in os.environ and "OPENAI_API_KEY" not in os.environ
            assert sys.flags.isolated == 1
            print(json.dumps(response), flush=True)
            """);
        await using var service = MakeService(files);
        Assert.AreEqual("regression", (await service.AdviseAsync("safe prompt").WaitAsync(TimeSpan.FromSeconds(10))).Category);
    }

    private static LocalLayaDecisionService MakeService(TestDirectory files)
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root is not null; root = root.Parent)
        {
            if (!File.Exists(Path.Combine(root.FullName, "Companion.slnx"))) continue;
            var python = Path.Combine(root.FullName, "artifacts", "ml-runtime", "Scripts", "python.exe");
            Assert.IsTrue(File.Exists(python), "Optional ML Python runtime is required for real process-boundary review.");
            return new(python, files.FilePath("worker.py"), files.PathValue);
        }
        throw new AssertFailedException("Repository runtime not found.");
    }

    private static async Task WriteWorker(TestDirectory files, string body) =>
        await File.WriteAllTextAsync(files.FilePath("worker.py"), "import json, os, sys, time\nfrom pathlib import Path\nrequest=json.loads(sys.stdin.readline())\nproject=Path(request['modelDir'])\nresponse={'protocol':1,'id':request['id'],'ok':True,'result':json.loads(" + JsonSerializer.Serialize(Valid) + ")}\n" + body + "\n", new UTF8Encoding(false));

    private static async Task<Process> HoldIdentity(TestDirectory files, Task operation, CancellationToken token)
    {
        while (!File.Exists(files.FilePath("ready")))
        {
            if (operation.IsCompleted) { await operation; Assert.Fail("Worker did not enter its held barrier."); }
            await Task.Delay(20, token);
        }
        var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(files.FilePath("ready"), token), System.Globalization.CultureInfo.InvariantCulture));
        _ = process.Handle;
        return process;
    }
}
