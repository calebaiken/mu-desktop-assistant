using System.Text.Json;
using System.Text.Json.Nodes;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;
using Companion.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent contract tests: real metadata persistence and adversarial session interleavings.</summary>
[TestClass]
public sealed class IndependentResearchProjectTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(12);

    [TestMethod]
    public async Task SameDisplayNamesRemainDistinctAndLegacyDataIsNeverRelocated()
    {
        using var files = new TestDirectory();
        var legacy = Directory.CreateDirectory(files.FilePath("ml-lab/default-project")).FullName;
        var evidence = Path.Combine(legacy, "retained-private-result.json");
        await File.WriteAllTextAsync(evidence, "legacy result");
        var firstStore = new JsonResearchProjectStore(files.PathValue);
        var first = await firstStore.CreateAsync("../literal <research>");
        var second = await firstStore.CreateAsync(first.Name);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(first.Directory, second.Directory);
        Assert.AreEqual(files.FilePath("ml-lab/projects/" + first.Id.ToString("N")), first.Directory);
        var restarted = new JsonResearchProjectStore(files.PathValue);
        var projects = await restarted.ListAsync();
        Assert.HasCount(3, projects);
        Assert.AreEqual(first, await restarted.OpenAsync(first.Id));
        Assert.AreEqual(second, await restarted.OpenAsync(second.Id));
        var old = await restarted.OpenAsync(ResearchProjects.LegacyId);
        Assert.IsTrue(old.IsLegacy);
        Assert.AreEqual(legacy, old.Directory);
        Assert.AreEqual("legacy result", await File.ReadAllTextAsync(evidence));
        Assert.IsFalse(Directory.Exists(first.Directory), "Metadata creation must not copy/import/create dataset artifacts.");
        Assert.IsFalse(Directory.Exists(second.Directory));
    }

    [TestMethod]
    public async Task IndependentProjectsKeepRealArtifactFilesSeparateAcrossReload()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        Directory.CreateDirectory(Path.Combine(a.Directory, "experiments"));
        Directory.CreateDirectory(Path.Combine(b.Directory, "experiments"));
        await File.WriteAllTextAsync(Path.Combine(a.Directory, "experiments", "same-id.json"), "result A");
        await File.WriteAllTextAsync(Path.Combine(b.Directory, "experiments", "same-id.json"), "result B");
        var reopened = new JsonResearchProjectStore(files.PathValue);
        Assert.AreEqual("result A", await File.ReadAllTextAsync(Path.Combine((await reopened.OpenAsync(a.Id)).Directory, "experiments", "same-id.json")));
        Assert.AreEqual("result B", await File.ReadAllTextAsync(Path.Combine((await reopened.OpenAsync(b.Id)).Directory, "experiments", "same-id.json")));
        await Assert.ThrowsAsync<MlException>(() => reopened.OpenAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<MlException>(() => reopened.OpenAsync(ResearchProjects.LegacyId));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" leading")]
    [DataRow("trailing ")]
    [DataRow("line\nbreak")]
    [DataRow("line\u2028separator")]
    [DataRow("paragraph\u2029separator")]
    [DataRow("hidden\u2066direction")]
    [DataRow("supplementary\udb40\udc01format")]
    public async Task RejectedNamesDoNotWriteCatalog(string name)
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(name));
        Assert.IsFalse(File.Exists(files.FilePath("ml-lab/research-projects.json")));
    }

    [TestMethod]
    public async Task RuntimeConstructedMalformedUtf16CannotBeNormalizedIntoAStoredName()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        foreach (var invalid in new[] { "high" + (char)0xd800, "low" + (char)0xdc00, "format" + char.ConvertFromUtf32(0xe0001) })
            await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(invalid));
        Assert.IsFalse(File.Exists(files.FilePath("ml-lab/research-projects.json")));
    }

    [TestMethod]
    public async Task NameAndCatalogCountBoundsAreEnforcedOnActualWrites()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(new string('x', ResearchProjects.MaximumNameCharacters + 1)));
        await store.CreateAsync(new string('x', ResearchProjects.MaximumNameCharacters));
        for (var i = 1; i < ResearchProjects.MaximumProjects; i++) await store.CreateAsync("project " + i);
        var before = await File.ReadAllBytesAsync(files.FilePath("ml-lab/research-projects.json"));
        await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("too many"));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(files.FilePath("ml-lab/research-projects.json")));
        Assert.HasCount(ResearchProjects.MaximumProjects, await new JsonResearchProjectStore(files.PathValue).ListAsync());
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{")]
    [DataRow("{\"version\":99,\"projects\":[]}")]
    [DataRow("{\"version\":1,\"projects\":null}")]
    [DataRow("{\"version\":1,\"Version\":1,\"projects\":[]}")]
    [DataRow("{\"version\":1,\"projects\":[],\"directory\":\"../outside\"}")]
    [DataRow("{\"version\":1,\"projects\":[null]}")]
    public async Task MalformedCatalogIsPreservedAndCannotBeSilentlyRecreated(string text)
    {
        using var files = new TestDirectory();
        Directory.CreateDirectory(files.FilePath("ml-lab"));
        var catalog = files.FilePath("ml-lab/research-projects.json");
        await File.WriteAllTextAsync(catalog, text);
        var store = new JsonResearchProjectStore(files.PathValue);
        await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
        await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("must not replace"));
        Assert.AreEqual(text, await File.ReadAllTextAsync(catalog));
    }

    [TestMethod]
    public async Task DuplicateIdentityUnknownEntryFieldsAndInvalidDatesFailClosed()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        await store.CreateAsync("valid");
        var path = files.FilePath("ml-lab/research-projects.json");
        var original = await File.ReadAllTextAsync(path);
        var duplicate = JsonNode.Parse(original)!.AsObject();
        duplicate["projects"]!.AsArray().Add(duplicate["projects"]![0]!.DeepClone());
        await CheckInvalidAsync(duplicate);
        var extraPath = JsonNode.Parse(original)!;
        extraPath["projects"]![0]!["directory"] = files.FilePath("outside-projects");
        await CheckInvalidAsync(extraPath);
        var emptyId = JsonNode.Parse(original)!;
        emptyId["projects"]![0]!["id"] = Guid.Empty.ToString();
        await CheckInvalidAsync(emptyId);
        var timezone = JsonNode.Parse(original)!;
        timezone["projects"]![0]!["createdAt"] = "2026-10-19T12:00:00+03:00";
        await CheckInvalidAsync(timezone);
        var missingName = JsonNode.Parse(original)!;
        missingName["projects"]![0]!.AsObject().Remove("name");
        await CheckInvalidAsync(missingName);

        async Task CheckInvalidAsync(JsonNode node)
        {
            var content = node.ToJsonString();
            await File.WriteAllTextAsync(path, content);
            await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
            await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("denied"));
            Assert.AreEqual(content, await File.ReadAllTextAsync(path));
        }
    }

    [TestMethod]
    public async Task OversizedAndDeepCatalogsAreBoundedAndNotOverwritten()
    {
        using var files = new TestDirectory();
        Directory.CreateDirectory(files.FilePath("ml-lab"));
        var catalog = files.FilePath("ml-lab/research-projects.json");
        var store = new JsonResearchProjectStore(files.PathValue);
        foreach (var text in new[] { new string(' ', JsonResearchProjectStore.MaximumCatalogBytes + 1), new string('[', 30) + "0" + new string(']', 30) })
        {
            await File.WriteAllTextAsync(catalog, text);
            await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
            await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("no write"));
            Assert.AreEqual(text, await File.ReadAllTextAsync(catalog));
        }
    }

    [TestMethod]
    public async Task InterruptedWriteRetainsPrimaryAndBackupAndAllowsExplicitRetry()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        await store.CreateAsync("first");
        await store.CreateAsync("second");
        var catalog = files.FilePath("ml-lab/research-projects.json");
        var primary = await File.ReadAllBytesAsync(catalog);
        var backup = await File.ReadAllBytesAsync(catalog + ".bak");
        await Assert.ThrowsAsync<MlException>(() => new JsonResearchProjectStore(files.PathValue, new FailedWriter()).CreateAsync("disk full"));
        CollectionAssert.AreEqual(primary, await File.ReadAllBytesAsync(catalog));
        CollectionAssert.AreEqual(backup, await File.ReadAllBytesAsync(catalog + ".bak"));
        Assert.HasCount(2, await store.ListAsync());
        await store.CreateAsync("retried");
        CollectionAssert.AreEqual(primary, await File.ReadAllBytesAsync(catalog + ".bak"));
    }

    [TestMethod]
    [DataRow("backup")]
    [DataRow("projects")]
    public async Task MissingPrimaryWithRecoveryEvidenceCannotSilentlyCreateAnotherCatalog(string evidence)
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var first = await store.CreateAsync("first");
        var catalog = files.FilePath("ml-lab/research-projects.json");
        string retainedPath;
        if (evidence == "backup")
        {
            await store.CreateAsync("second");
            retainedPath = catalog + ".bak";
        }
        else
        {
            Directory.CreateDirectory(first.Directory);
            retainedPath = Path.Combine(first.Directory, "retained-artifact.txt");
            await File.WriteAllTextAsync(retainedPath, "existing project data");
        }
        var retained = await File.ReadAllBytesAsync(retainedPath);
        File.Delete(catalog);
        await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
        await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("must not reset"));
        await Assert.ThrowsAsync<MlException>(() => store.OpenAsync(first.Id));
        Assert.IsFalse(File.Exists(catalog));
        CollectionAssert.AreEqual(retained, await File.ReadAllBytesAsync(retainedPath));
    }

    [TestMethod]
    public async Task PreCancelledCreateAndOpenDoNotChangeStorageOrSession()
    {
        using var files = new TestDirectory();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var store = new JsonResearchProjectStore(files.PathValue);
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.CreateAsync("cancelled", cancelled.Token));
        Assert.IsFalse(File.Exists(files.FilePath("ml-lab/research-projects.json")));
        var p = await store.CreateAsync("existing");
        var calls = 0;
        await using var workspace = new ResearchLabWorkspace(store, project => { calls++; return new Lab(project.Directory); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => workspace.OpenAsync(p.Id, cancelled.Token));
        Assert.AreEqual(0, calls);
        await workspace.OpenAsync(p.Id);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task ExistingLinkedCatalogAndProjectDirectoryCannotRedirectReads()
    {
        using var files = new TestDirectory();
        var outside = Directory.CreateDirectory(files.FilePath("outside")).FullName;
        var store = new JsonResearchProjectStore(files.PathValue);
        var p = await store.CreateAsync("linked candidate");
        Directory.CreateDirectory(Path.GetDirectoryName(p.Directory)!);
        try { Directory.CreateSymbolicLink(p.Directory, outside); }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        { Assert.Inconclusive("Creating a test link is unavailable in this environment: " + error.GetType().Name); }
        try
        {
            await Assert.ThrowsAsync<MlException>(() => store.OpenAsync(p.Id));
            var catalog = files.FilePath("ml-lab/research-projects.json");
            var retained = await File.ReadAllTextAsync(catalog);
            var external = Path.Combine(outside, "foreign-catalog.json");
            await File.WriteAllTextAsync(external, retained);
            File.Delete(catalog);
            File.CreateSymbolicLink(catalog, external);
            await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
            await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("must not follow link"));
            Assert.AreEqual(retained, await File.ReadAllTextAsync(external));
        }
        finally { Directory.Delete(p.Directory); }
    }

    [TestMethod]
    public async Task AdmittedWorkWithEmptyJobsBlocksSwitchBeforeInnerExecutionCompletes()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var released = NewSource<MlResult>();
        var services = new List<Lab>();
        await using var workspace = new ResearchLabWorkspace(store, project =>
        {
            var service = new Lab(project.Directory) { Run = _ => released.Task };
            services.Add(service); return service;
        });
        var first = await workspace.OpenAsync(a.Id);
        var active = first.Service.ExecuteAsync(new MlList());
        Assert.IsEmpty(first.Service.Jobs, "The adversarial worker deliberately has not published Queued.");
        Assert.IsTrue(workspace.IsBusy);
        await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(b.Id));
        Assert.AreEqual(0, services[0].Disposals);
        released.SetResult(Result());
        await active.WaitAsync(Deadline);
        var selected = await workspace.OpenAsync(b.Id);
        Assert.AreEqual(b, selected.Project);
        Assert.AreEqual(1, services[0].Disposals);
        Assert.HasCount(2, services);
        await Assert.ThrowsAsync<MlException>(() => first.Service.ExecuteAsync(new MlList()));
    }

    [TestMethod]
    public async Task SecondQueuedAdmissionKeepsProjectBusyAfterFirstRequestCompletes()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var firstReply = NewSource<MlResult>();
        var queuedReply = NewSource<MlResult>();
        var entries = 0;
        var lab = new Lab(a.Directory) { Run = _ => ++entries == 1 ? firstReply.Task : queuedReply.Task };
        await using var workspace = new ResearchLabWorkspace(store, project => project.Id == a.Id ? lab : new Lab(project.Directory));
        var selected = await workspace.OpenAsync(a.Id);
        var first = selected.Service.ExecuteAsync(new MlCapabilities());
        var queued = selected.Service.ExecuteAsync(new MlList());
        try
        {
            firstReply.TrySetResult(Result());
            await first.WaitAsync(Deadline);
            Assert.IsTrue(workspace.IsBusy);
            await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(b.Id));
            Assert.AreEqual(0, lab.Disposals);
        }
        finally { firstReply.TrySetResult(Result()); queuedReply.TrySetResult(Result()); }
        await queued.WaitAsync(Deadline);
        Assert.AreEqual(b, (await workspace.OpenAsync(b.Id)).Project);
    }

    [TestMethod]
    public async Task SwitchRetiresFacadeAndWaitsForCleanupBeforeCreatingAnotherService()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var entered = NewSource<bool>();
        var cleanup = NewSource<bool>();
        var services = new List<Lab>();
        await using var workspace = new ResearchLabWorkspace(store, project =>
        {
            var lab = new Lab(project.Directory);
            if (services.Count == 0) lab.Stop = async () => { entered.TrySetResult(true); await cleanup.Task; };
            services.Add(lab); return lab;
        });
        var first = await workspace.OpenAsync(a.Id);
        var switching = workspace.OpenAsync(b.Id);
        await entered.Task.WaitAsync(Deadline);
        try
        {
            Assert.HasCount(1, services);
            await Assert.ThrowsAsync<MlException>(() => first.Service.ExecuteAsync(new MlCapabilities()));
            await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(a.Id));
            Assert.IsTrue(workspace.IsBusy);
        }
        finally { cleanup.TrySetResult(true); }
        var next = await switching.WaitAsync(Deadline);
        Assert.HasCount(2, services);
        Assert.AreEqual(b, next.Project);
        Assert.AreEqual(0, services[0].Executions);
        await Assert.ThrowsAsync<MlException>(() => first.Service.ExecuteAsync(new MlCapabilities()));
    }

    [TestMethod]
    public async Task SameProjectReusesServiceAndUnknownSelectionLeavesExistingFacadeUsable()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var calls = 0;
        await using var workspace = new ResearchLabWorkspace(store, project => { calls++; return new Lab(project.Directory); });
        var first = await workspace.OpenAsync(a.Id);
        var same = await workspace.OpenAsync(a.Id);
        Assert.AreSame(first.Service, same.Service);
        await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(Guid.NewGuid()));
        await first.Service.ExecuteAsync(new MlList());
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task CleanupFailurePoisonsWorkspaceInsteadOfAdmittingFreshService()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var calls = 0;
        var workspace = new ResearchLabWorkspace(store, project =>
        { calls++; return new Lab(project.Directory) { Stop = () => Task.FromException(new IOException("cleanup unconfirmed")) }; });
        var initial = await workspace.OpenAsync(a.Id);
        var failure = await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(b.Id));
        Assert.AreEqual("cleanup", failure.Code);
        await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(a.Id));
        await Assert.ThrowsAsync<MlException>(() => initial.Service.ExecuteAsync(new MlList()));
        Assert.AreEqual(1, calls);
        await Assert.ThrowsAsync<IOException>(() => workspace.DisposeAsync().AsTask());
    }

    [TestMethod]
    public async Task NoncooperativeCleanupTimesOutPoisonsAndRemainsOwnedForShutdown()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var retained = NewSource<bool>();
        var calls = 0;
        var workspace = new ResearchLabWorkspace(store, project =>
        { calls++; return new Lab(project.Directory) { Stop = () => retained.Task }; });
        await workspace.OpenAsync(a.Id);
        try
        {
            var failure = await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(b.Id).WaitAsync(Deadline));
            Assert.AreEqual("cleanup", failure.Code);
            Assert.AreEqual(1, calls);
            await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(b.Id));
            var disposing = workspace.DisposeAsync().AsTask();
            Assert.IsFalse(disposing.IsCompleted, "Timed-out cleanup remains owned until it settles.");
            retained.TrySetResult(true);
            await Assert.ThrowsAsync<MlException>(() => disposing.WaitAsync(Deadline));
        }
        finally { retained.TrySetResult(true); }
    }

    [TestMethod]
    public async Task InnerSynchronousFailureAndCancelledRequestReleaseAdmission()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var lab = new Lab(a.Directory) { Run = _ => throw new ArgumentException("invalid action") };
        await using var workspace = new ResearchLabWorkspace(store, project => project.Id == a.Id ? lab : new Lab(project.Directory));
        var first = await workspace.OpenAsync(a.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => first.Service.ExecuteAsync(new MlCapabilities()));
        Assert.IsFalse(workspace.IsBusy);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => first.Service.ExecuteAsync(new MlList(), cancellationToken: cancelled.Token));
        Assert.AreEqual(b, (await workspace.OpenAsync(b.Id)).Project);
    }

    [TestMethod]
    public async Task MismatchedFactoryPathCannotRunOrAdmitAnotherService()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var wrong = new Lab(files.FilePath("unexpected"));
        var workspace = new ResearchLabWorkspace(store, _ => wrong);
        await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(a.Id));
        await Assert.ThrowsAsync<MlException>(() => workspace.OpenAsync(a.Id));
        await Assert.ThrowsAsync<MlException>(() => workspace.DisposeAsync().AsTask());
        Assert.AreEqual(1, wrong.Disposals);
        Assert.AreEqual(0, wrong.Executions);
    }

    [TestMethod]
    public async Task BorrowedLabFacadeCannotDisposeTheAppOwnedSession()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var lab = new Lab(a.Directory);
        await using var workspace = new ResearchLabWorkspace(store, _ => lab);
        var selected = await workspace.OpenAsync(a.Id);
        var failure = await Assert.ThrowsAsync<MlException>(() => selected.Service.DisposeAsync().AsTask());
        Assert.AreEqual("project_ownership", failure.Code);
        Assert.AreEqual(0, lab.Disposals);
        await selected.Service.ExecuteAsync(new MlList());
        Assert.AreEqual(1, lab.Executions);
    }

    [TestMethod]
    public async Task ConcurrentShutdownSharesCleanupAndPreventsFurtherAdmission()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var stop = NewSource<bool>();
        var lab = new Lab(a.Directory) { Stop = () => stop.Task };
        var workspace = new ResearchLabWorkspace(store, _ => lab);
        var first = await workspace.OpenAsync(a.Id);
        var disposal = workspace.DisposeAsync().AsTask();
        var second = workspace.DisposeAsync().AsTask();
        try
        {
            Assert.AreSame(disposal, second);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => first.Service.ExecuteAsync(new MlList()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.OpenAsync(a.Id));
            Assert.AreEqual(1, lab.Disposals);
        }
        finally { stop.TrySetResult(true); }
        await Task.WhenAll(disposal, second).WaitAsync(Deadline);
    }

    [TestMethod]
    public async Task CancellingAfterRetirementDoesNotAbandonCleanupOrAdmitTheNewProject()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var entered = NewSource<bool>();
        var cleanup = NewSource<bool>();
        var calls = 0;
        await using var workspace = new ResearchLabWorkspace(store, project =>
        {
            calls++;
            return new Lab(project.Directory) { Stop = calls == 1 ? async () => { entered.TrySetResult(true); await cleanup.Task; } : () => Task.CompletedTask };
        });
        var old = await workspace.OpenAsync(a.Id);
        using var caller = new CancellationTokenSource();
        var switching = workspace.OpenAsync(b.Id, caller.Token);
        await entered.Task.WaitAsync(Deadline);
        try
        {
            caller.Cancel();
            Assert.IsFalse(switching.IsCompleted, "Cancellation cannot abandon an owned retirement.");
            Assert.AreEqual(1, calls);
            await Assert.ThrowsAsync<MlException>(() => old.Service.ExecuteAsync(new MlList()));
        }
        finally { cleanup.TrySetResult(true); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => switching.WaitAsync(Deadline));
        Assert.AreEqual(1, calls);
        Assert.AreEqual(b, (await workspace.OpenAsync(b.Id)).Project);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task ShutdownDuringTransitionRetainsCleanupAndNeverCreatesTheRequestedNextService()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("A");
        var b = await store.CreateAsync("B");
        var entered = NewSource<bool>();
        var cleanup = NewSource<bool>();
        var calls = 0;
        var workspace = new ResearchLabWorkspace(store, project =>
        {
            calls++;
            return new Lab(project.Directory) { Stop = async () => { entered.TrySetResult(true); await cleanup.Task; } };
        });
        var old = await workspace.OpenAsync(a.Id);
        var switching = workspace.OpenAsync(b.Id);
        await entered.Task.WaitAsync(Deadline);
        var shutdown = workspace.DisposeAsync().AsTask();
        try
        {
            Assert.IsFalse(shutdown.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => old.Service.ExecuteAsync(new MlList()));
            Assert.AreEqual(1, calls);
        }
        finally { cleanup.TrySetResult(true); }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => switching.WaitAsync(Deadline));
        await shutdown.WaitAsync(Deadline);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task RealWindowsOwnedWorkerRoutesRequestsOnlyToSelectedProjectAndReloadsItsOwnMarker()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("Real Windows Job Object process integration requires Windows.");
        var python = RequireRuntime();
        using var files = new TestDirectory();
        var worker = files.FilePath("worker.py");
        await File.WriteAllTextAsync(worker, """
            import json, sys
            from pathlib import Path
            request = json.loads(sys.stdin.readline())
            project = Path(request["projectDir"])
            project.mkdir(parents=True, exist_ok=True)
            marker = project / "independent-session-marker.json"
            seen = json.loads(marker.read_text(encoding="utf-8")) if marker.exists() else 0
            marker.write_text(json.dumps(seen + 1), encoding="utf-8")
            print(json.dumps({"protocol": 1, "id": request["id"], "ok": True,
                "result": {"directory": str(project), "priorVisits": seen}}), flush=True)
            """);
        var store = new JsonResearchProjectStore(files.PathValue);
        var a = await store.CreateAsync("Worker A");
        var b = await store.CreateAsync("Worker B");
        await using var workspace = new ResearchLabWorkspace(store, project => new LocalMlLabService(python, worker, project.Directory));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var first = await workspace.OpenAsync(a.Id, deadline.Token);
        var resultA = await first.Service.ExecuteAsync(new MlCapabilities(), cancellationToken: deadline.Token).WaitAsync(Deadline);
        Assert.AreEqual(a.Directory, resultA.Data.GetProperty("directory").GetString());
        Assert.AreEqual(0, resultA.Data.GetProperty("priorVisits").GetInt32());
        var second = await workspace.OpenAsync(b.Id, deadline.Token);
        var resultB = await second.Service.ExecuteAsync(new MlCapabilities(), cancellationToken: deadline.Token).WaitAsync(Deadline);
        Assert.AreEqual(b.Directory, resultB.Data.GetProperty("directory").GetString());
        Assert.AreEqual(0, resultB.Data.GetProperty("priorVisits").GetInt32());
        await Assert.ThrowsAsync<MlException>(() => first.Service.ExecuteAsync(new MlCapabilities()));
        var reopened = await workspace.OpenAsync(a.Id, deadline.Token);
        var returned = await reopened.Service.ExecuteAsync(new MlCapabilities(), cancellationToken: deadline.Token).WaitAsync(Deadline);
        Assert.AreEqual(a.Directory, returned.Data.GetProperty("directory").GetString());
        Assert.AreEqual(1, returned.Data.GetProperty("priorVisits").GetInt32());
        Assert.AreEqual("1", await File.ReadAllTextAsync(Path.Combine(b.Directory, "independent-session-marker.json")));
        Assert.IsTrue(reopened.Service.Jobs.All(job => job.State == MlJobState.Completed));
    }

    private static string RequireRuntime()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Companion.slnx"))) continue;
            var runtime = Path.Combine(directory.FullName, "artifacts", "ml-runtime", "Scripts", "python.exe");
            if (!File.Exists(runtime)) Assert.Inconclusive("Optional project ML runtime is absent. Run scripts/setup-ml-lab.ps1 for real worker integration.");
            return runtime;
        }
        Assert.Inconclusive("Optional project ML runtime could not be located from this test deployment.");
        return "";
    }

    private static MlResult Result() => new(Guid.NewGuid(), "list", JsonSerializer.SerializeToElement(new { ok = true }));
    private static TaskCompletionSource<T> NewSource<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class FailedWriter : IAtomicFileWriter
    {
        public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents) => throw new IOException("injected storage failure");
    }
    private sealed class Lab(string projectDirectory) : IMlLabService
    {
        public string ProjectDirectory => projectDirectory;
        public IReadOnlyList<MlJobUpdate> Jobs => [];
        public int Executions { get; private set; }
        public int Disposals { get; private set; }
        public Func<CancellationToken, Task<MlResult>> Run { get; set; } = _ => Task.FromResult(Result());
        public Func<Task> Stop { get; set; } = () => Task.CompletedTask;
        public event Action<MlJobUpdate>? JobChanged { add { } remove { } }
        public void Cancel(Guid jobId) { }
        public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
        { Executions++; return Run(cancellationToken); }
        public ValueTask DisposeAsync() { Disposals++; return new(Stop()); }
    }
}
