using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>
/// Real Windows job/stdio integration using the optional project Python runtime.
/// Fixtures read the request before acting, never spawn another process, and do not
/// import ML packages, train models, access audio or use the network.
/// </summary>
[TestClass]
public sealed class MlWorkerHostTests
{
    private static readonly TimeSpan CaseDeadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AdmissionDeadline = TimeSpan.FromSeconds(6);

    [TestMethod]
    [DataRow("stdout", 2 * 1024 * 1024)]
    [DataRow("stderr", 64 * 1024)]
    public async Task OutputFloodThenSleepFailsPromptlyTerminatesHeldWorkerAndAllowsReuse(string stream, int limit)
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        var body = $$"""
            if request["action"] == "capabilities":
                reply({"reused": True})
            else:
                hold()
                sys.{{stream}}.buffer.write(b"x" * {{limit + 8192}})
                sys.{{stream}}.buffer.flush()
                time.sleep(60)
            """;
        await WriteWorkerAsync(files, body);
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        using var deadline = new CancellationTokenSource(CaseDeadline);
        var operation = service.ExecuteAsync(new MlList(), cancellationToken: deadline.Token);
        using var worker = await OpenHeldWorkerAsync(project, operation, deadline.Token);

        var elapsed = Stopwatch.StartNew();
        await File.WriteAllTextAsync(Path.Combine(project, "release"), "release", deadline.Token);
        var failure = await Assert.ThrowsExactlyAsync<MlException>(() => operation.WaitAsync(AdmissionDeadline));
        Assert.AreEqual("output_limit", failure.Code, "A flood must be classified by its actual output limit, not by a later overall timeout.");
        Assert.IsTrue(elapsed.Elapsed < AdmissionDeadline, "A flooding worker's deliberate 60-second sleep must be terminated promptly.");
        Assert.IsTrue(worker.HasExited, "Completion of the failed operation must follow termination of its held process identity.");
        Assert.AreEqual(MlJobState.Failed, service.Jobs.Single().State);

        var reused = await service.ExecuteAsync(new MlCapabilities(), cancellationToken: deadline.Token).WaitAsync(AdmissionDeadline);
        Assert.IsTrue(reused.Data.GetProperty("reused").GetBoolean());
        Assert.AreEqual(1, service.Jobs.Count(job => job.State == MlJobState.Failed));
        Assert.AreEqual(1, service.Jobs.Count(job => job.State == MlJobState.Completed));
    }

    [TestMethod]
    [DataRow("invalid-json", "worker_failure")]
    [DataRow("wrong-identity", "protocol")]
    [DataRow("nonzero-success", "exit")]
    public async Task MalformedOrUnsuccessfulReplyHasFailedTerminalStateAndReleasesHeldProcess(string kind, string expectedCode)
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        var body = $$$"""
            hold()
            kind = "{{{kind}}}"
            if kind == "invalid-json":
                print("{not-json", flush=True)
            elif kind == "wrong-identity":
                print(json.dumps({"protocol": 1, "id": "wrong-id", "ok": True, "result": {}}), flush=True)
            else:
                reply({"notActuallySuccessful": True})
                sys.exit(7)
            """;
        await WriteWorkerAsync(files, body);
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        using var deadline = new CancellationTokenSource(CaseDeadline);
        var operation = service.ExecuteAsync(new MlList(), cancellationToken: deadline.Token);
        using var worker = await OpenHeldWorkerAsync(project, operation, deadline.Token);
        await File.WriteAllTextAsync(Path.Combine(project, "release"), "release", deadline.Token);

        var failure = await Assert.ThrowsExactlyAsync<MlException>(() => operation.WaitAsync(AdmissionDeadline));
        Assert.AreEqual(expectedCode, failure.Code);
        Assert.AreEqual(MlJobState.Failed, service.Jobs.Single().State);
        Assert.IsTrue(worker.HasExited);
        Assert.IsFalse(failure.Message.Contains("not-json", StringComparison.Ordinal));
        Assert.IsFalse(failure.Message.Contains(files.PathValue, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MissingRuntimeReportsSetupActionWithoutLaunchingWorker()
    {
        using var files = new TestDirectory();
        await WriteWorkerAsync(files, "reply({\"unexpected\": True})");
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        await using var service = new LocalMlLabService(files.FilePath("python.exe"), files.FilePath("worker.py"), project);
        using var deadline = new CancellationTokenSource(CaseDeadline);
        var states = new List<MlJobState>();
        service.JobChanged += update => { lock (states) states.Add(update.State); };

        var failure = await Assert.ThrowsExactlyAsync<MlException>(() => service.ExecuteAsync(new MlList(), cancellationToken: deadline.Token).WaitAsync(AdmissionDeadline));
        Assert.AreEqual("missing_runtime", failure.Code);
        StringAssert.Contains(failure.Message, "scripts/setup-ml-lab.ps1");
        Assert.IsFalse(failure.Message.Contains(files.PathValue, StringComparison.Ordinal));
        Assert.AreEqual(MlJobState.Failed, service.Jobs.Single().State);
        lock (states) Assert.IsFalse(states.Contains(MlJobState.Running));
        Assert.IsFalse(File.Exists(Path.Combine(project, "ready.json")));
    }

    [TestMethod]
    public async Task AlreadyCancelledRequestNeverReachesRunningOrExecutesWorker()
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        await WriteWorkerAsync(files, "hold()\nreply({\"unexpected\": True})");
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        var states = new List<MlJobState>();
        service.JobChanged += update => { lock (states) states.Add(update.State); };
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(new MlList(), cancellationToken: cancelled.Token).WaitAsync(AdmissionDeadline));
        Assert.AreEqual(MlJobState.Cancelled, service.Jobs.Single().State);
        lock (states) Assert.IsFalse(states.Contains(MlJobState.Running));
        Assert.IsFalse(File.Exists(Path.Combine(project, "ready.json")), "A cancelled admission must not run even the synthetic worker's first action.");
    }

    [TestMethod]
    public async Task CancellingQueuedMiddleRequestDoesNotLetSuccessorOvertakeHeldActiveWorker()
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        await WriteWorkerAsync(files, """
            action = request["action"]
            with (project / "order.jsonl").open("a", encoding="utf-8") as stream:
                stream.write(json.dumps("start:" + action) + "\n")
            if action == "inspect":
                hold()
            else:
                (project / (action + ".started")).write_text("started", encoding="utf-8")
            with (project / "order.jsonl").open("a", encoding="utf-8") as stream:
                stream.write(json.dumps("end:" + action) + "\n")
            reply({"action": action})
            """);
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        using var deadline = new CancellationTokenSource(CaseDeadline);
        var dataset = new MlDatasetSelection(files.FilePath("public-fixture.csv"));
        var active = service.ExecuteAsync(new MlInspect(dataset), cancellationToken: deadline.Token);
        using var worker = await OpenHeldWorkerAsync(project, active, deadline.Token);
        var middle = service.ExecuteAsync(new MlBrowse(dataset), cancellationToken: deadline.Token);
        var successor = service.ExecuteAsync(new MlList(), cancellationToken: deadline.Token);
        await WaitUntilAsync(() => service.Jobs.Count == 3, AdmissionDeadline, deadline.Token);
        var middleJob = service.Jobs.Single(job => job.Operation == "browse");
        Assert.AreEqual(MlJobState.Queued, middleJob.State);
        service.Cancel(middleJob.Id);
        await Assert.ThrowsAsync<OperationCanceledException>(() => middle.WaitAsync(AdmissionDeadline));

        // The first worker is deliberately held at a filesystem barrier. A cancelled
        // middle request must not release its successor's queue ownership.
        await Task.Delay(200, deadline.Token);
        Assert.IsFalse(successor.IsCompleted);
        Assert.IsFalse(File.Exists(Path.Combine(project, "list.started")));
        Assert.IsFalse(File.Exists(Path.Combine(project, "browse.started")));
        Assert.IsFalse(worker.HasExited);
        await File.WriteAllTextAsync(Path.Combine(project, "release"), "release", deadline.Token);
        var firstResult = await active.WaitAsync(AdmissionDeadline);
        var lastResult = await successor.WaitAsync(AdmissionDeadline);
        Assert.AreEqual("inspect", firstResult.Data.GetProperty("action").GetString());
        Assert.AreEqual("list", lastResult.Data.GetProperty("action").GetString());
        Assert.IsTrue(worker.HasExited);
        var order = (await File.ReadAllLinesAsync(Path.Combine(project, "order.jsonl"), deadline.Token))
            .Select(line => JsonSerializer.Deserialize<string>(line)).ToArray();
        CollectionAssert.AreEqual(new[] { "start:inspect", "end:inspect", "start:list", "end:list" }, order);
        Assert.AreEqual(2, service.Jobs.Count(job => job.State == MlJobState.Completed));
        Assert.AreEqual(1, service.Jobs.Count(job => job.State == MlJobState.Cancelled));
    }

    [TestMethod]
    public async Task MinimumMemoryJobRejectsAllocationLargerThanHardCommittedMemoryCap()
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        await WriteWorkerAsync(files, """
            requested = 320 * 1024 * 1024
            denied = False
            try:
                allocation = bytearray(requested)
                del allocation
            except MemoryError:
                denied = True
            reply({"allocationDenied": denied, "requestedBytes": requested})
            """);
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        using var deadline = new CancellationTokenSource(CaseDeadline);

        var result = await service.ExecuteAsync(new MlCapabilities(), new MlLimits(MemoryMiB: 256), deadline.Token).WaitAsync(AdmissionDeadline);
        Assert.AreEqual(320 * 1024 * 1024, result.Data.GetProperty("requestedBytes").GetInt32());
        Assert.IsTrue(result.Data.GetProperty("allocationDenied").GetBoolean(), "The real worker must not commit a single 320 MiB allocation inside its 256 MiB job.");
        Assert.AreEqual(MlJobState.Completed, service.Jobs.Single().State,
            "The fixture catches allocation denial and returns normally; a timeout is not evidence of the memory cap.");
    }

    [TestMethod]
    public async Task ConcurrentDisposeStopsOwnedHeldAndQueuedJobsWithoutStoppingAnotherService()
    {
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var project = Directory.CreateDirectory(files.FilePath("project")).FullName;
        var otherProject = Directory.CreateDirectory(files.FilePath("other-project")).FullName;
        await WriteWorkerAsync(files, "hold()\nreply({\"released\": True})");
        await using var service = new LocalMlLabService(python, files.FilePath("worker.py"), project);
        await using var otherService = new LocalMlLabService(python, files.FilePath("worker.py"), otherProject);
        using var deadline = new CancellationTokenSource(CaseDeadline);
        var active = service.ExecuteAsync(new MlList(), cancellationToken: deadline.Token);
        using var worker = await OpenHeldWorkerAsync(project, active, deadline.Token);
        var otherActive = otherService.ExecuteAsync(new MlList(), cancellationToken: deadline.Token);
        using var otherWorker = await OpenHeldWorkerAsync(otherProject, otherActive, deadline.Token);
        var queued = service.ExecuteAsync(new MlCapabilities(), cancellationToken: deadline.Token);
        await WaitUntilAsync(() => service.Jobs.Count == 2, AdmissionDeadline, deadline.Token);
        Assert.AreEqual(MlJobState.Queued, service.Jobs.Single(job => job.Operation == "capabilities").State);
        var firstDispose = service.DisposeAsync().AsTask();
        var secondDispose = service.DisposeAsync().AsTask();
        Assert.AreSame(firstDispose, secondDispose, "Concurrent callers share one cleanup task.");
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(AdmissionDeadline);
        await Assert.ThrowsAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAsync<OperationCanceledException>(() => queued);
        Assert.IsTrue(worker.HasExited, "Disposal must terminate the exact owned child.");
        Assert.IsFalse(otherWorker.HasExited, "Another service's held child must remain alive.");
        Assert.IsTrue(service.Jobs.All(job => job.State == MlJobState.Cancelled));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.ExecuteAsync(new MlList()));
        await File.WriteAllTextAsync(Path.Combine(otherProject, "release"), "release", deadline.Token);
        Assert.IsTrue((await otherActive.WaitAsync(AdmissionDeadline)).Data.GetProperty("released").GetBoolean());
    }

    private static async Task WriteWorkerAsync(TestDirectory files, string body) =>
        await File.WriteAllTextAsync(files.FilePath("worker.py"), WorkerPrelude + "\n" + body + "\n", new UTF8Encoding(false));

    private static async Task<Process> OpenHeldWorkerAsync(string project, Task operation, CancellationToken token)
    {
        var marker = Path.Combine(project, "ready.json");
        await WaitUntilAsync(() => File.Exists(marker) || operation.IsCompleted, AdmissionDeadline, token);
        if (operation.IsCompleted)
        {
            await operation;
            Assert.Fail("Synthetic worker completed before entering its held barrier.");
        }
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(marker, token));
        var process = Process.GetProcessById(document.RootElement.GetProperty("pid").GetInt32());
        try
        {
            _ = process.Handle; // Hold this exact live identity before releasing the barrier.
            Assert.IsFalse(process.HasExited);
            return process;
        }
        catch { process.Dispose(); throw; }
    }

    private static async Task WaitUntilAsync(Func<bool> ready, TimeSpan maximum, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            Assert.IsTrue(watch.Elapsed < maximum, "The synthetic worker did not reach its expected bounded state.");
            await Task.Delay(20, token);
        }
    }

    private static string RequireRuntime()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Companion.slnx"))) continue;
            var runtime = Path.Combine(directory.FullName, "artifacts", "ml-runtime", "Scripts", "python.exe");
            if (!File.Exists(runtime)) Assert.Inconclusive("Optional project ML runtime is absent. Run scripts/setup-ml-lab.ps1 to execute real worker host checks.");
            return runtime;
        }
        Assert.Inconclusive("Optional project ML runtime could not be located from this test deployment.");
        return "";
    }

    private const string WorkerPrelude = """
        import json, os, sys, time
        from pathlib import Path
        request = json.loads(sys.stdin.readline())
        project = Path(request["projectDir"])
        project.mkdir(parents=True, exist_ok=True)
        def reply(result):
            print(json.dumps({"protocol": 1, "id": request["id"], "ok": True, "result": result}), flush=True)
        def hold():
            temporary = project / "ready.tmp"
            temporary.write_text(json.dumps({"pid": os.getpid()}), encoding="utf-8")
            os.replace(temporary, project / "ready.json")
            deadline = time.monotonic() + 60
            while not (project / "release").exists():
                if time.monotonic() >= deadline:
                    raise RuntimeError("fixture barrier deadline")
                time.sleep(0.01)
        """;
}
