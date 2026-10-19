using System.Runtime.CompilerServices;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent handoff tests: real project metadata, adversarial admission, and controlled model ports.</summary>
[TestClass]
public sealed class IndependentChatProjectHandoffTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ConfirmTransfersOnlyExactRequestOnceWithoutWorkersOrProjectArtifacts()
    {
        await using var rig = await Rig.CreateAsync();
        const string request = "  Fit the selected μ dataset.\r\nKeep this exact request.\t ";
        var metadata = await File.ReadAllBytesAsync(rig.Catalog);
        var filesBefore = Directory.GetFiles(rig.Files.PathValue, "*", SearchOption.AllDirectories);
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, request);
        Assert.AreEqual(rig.A, ticket.Project);
        Assert.AreEqual(request, ticket.Request);
        var delivered = new List<string>();
        rig.Workspace.AcceptHandoff(ticket, delivered.Add);
        CollectionAssert.AreEqual(new[] { request }, delivered);
        Assert.IsNull(ticket.Request, "Consumed transfer state must not retain private request text.");
        Assert.AreEqual(0, rig.Lab.Executions);
        Assert.AreEqual(1, rig.FactoryCalls);
        Assert.IsFalse(Directory.Exists(rig.A.Directory));
        Assert.IsFalse(Directory.Exists(rig.B.Directory));
        CollectionAssert.AreEqual(filesBefore, Directory.GetFiles(rig.Files.PathValue, "*", SearchOption.AllDirectories));
        CollectionAssert.AreEqual(metadata, await File.ReadAllBytesAsync(rig.Catalog));
        await Assert.ThrowsAsync<MlException>(() => Task.Run(() => rig.Workspace.AcceptHandoff(ticket, delivered.Add)));
        Assert.HasCount(1, delivered);
        foreach (var path in filesBefore)
            Assert.IsFalse((await File.ReadAllTextAsync(path)).Contains(request, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TemporaryModeRefusesAtApplicationBoundaryWithoutRequestLeakage()
    {
        await using var rig = await Rig.CreateAsync();
        const string secret = "PRIVATE_TEMPORARY_HANDOFF_CONTENT";
        var error = Assert.Throws<MlException>(() => rig.Workspace.PrepareHandoff(rig.A.Id, secret, temporary: true));
        Assert.AreEqual("temporary_chat", error.Code);
        StringAssert.Contains(error.Message, "persist");
        Assert.IsFalse(error.ToString().Contains(secret, StringComparison.Ordinal));
        Assert.AreEqual(0, rig.Lab.Executions);
        Assert.IsFalse(Directory.Exists(rig.A.Directory));
    }

    [TestMethod]
    public async Task CancelledOrDismissedTicketCannotDeliverLater()
    {
        await using var rig = await Rig.CreateAsync();
        var dismissed = rig.Workspace.PrepareHandoff(rig.A.Id, "discard this request");
        dismissed.Dispose();
        dismissed.Dispose();
        Assert.IsNull(dismissed.Request);
        var calls = 0;
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(dismissed, _ => calls++));
        using var cancelled = rig.Workspace.PrepareHandoff(rig.A.Id, "cancel this request");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => rig.Workspace.AcceptHandoff(cancelled, _ => calls++, cancellation.Token));
        Assert.IsNull(cancelled.Request);
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(cancelled, _ => calls++));
        Assert.AreEqual(0, calls);
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task UnselectedAndIncorrectProjectIdsFailClosed()
    {
        await using var rig = await Rig.CreateAsync();
        Assert.Throws<MlException>(() => rig.Workspace.PrepareHandoff(rig.B.Id, "selected project only"));
        Assert.Throws<MlException>(() => rig.Workspace.PrepareHandoff(Guid.NewGuid(), "unknown project"));
        await using var unopened = new ResearchLabWorkspace(rig.Store, project => new Lab(project.Directory));
        Assert.Throws<MlException>(() => unopened.PrepareHandoff(rig.A.Id, "no selected service"));
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task TicketFromAnotherWorkspaceCannotTargetItsMatchingProject()
    {
        await using var rig = await Rig.CreateAsync();
        var otherLab = new Lab(rig.A.Directory);
        await using var other = new ResearchLabWorkspace(rig.Store, _ => otherLab);
        await other.OpenAsync(rig.A.Id);
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "private to one exact Lab lease");
        var deliveries = 0;
        Assert.Throws<MlException>(() => other.AcceptHandoff(ticket, _ => deliveries++));
        Assert.IsNull(ticket.Request);
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => deliveries++));
        Assert.AreEqual(0, deliveries);
        Assert.AreEqual(0, otherLab.Executions);
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task ProjectRetirementAndReturnCannotReviveAnOldTransfer()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "for the original A lifecycle");
        await rig.Workspace.OpenAsync(rig.B.Id);
        await rig.Workspace.OpenAsync(rig.A.Id);
        var calls = 0;
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => calls++));
        Assert.AreEqual(0, calls);
        Assert.AreEqual(3, rig.FactoryCalls);
        Assert.IsFalse(Directory.Exists(rig.B.Directory));
    }

    [TestMethod]
    public async Task WorkBeginningAndFinishingDuringPreviewInvalidatesTheTicket()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "must be reviewed again after work");
        await rig.Selected.Service.ExecuteAsync(new MlList());
        Assert.IsFalse(rig.Workspace.IsBusy);
        var calls = 0;
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => calls++));
        Assert.AreEqual(0, calls);
        Assert.AreEqual(1, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task SynchronousFailedWorkAlsoInvalidatesEarlierPreview()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "state predates failed action");
        rig.Lab.Run = () => throw new MlException("fixture_failure", "declared fixture failure");
        Assert.Throws<MlException>(() => rig.Selected.Service.ExecuteAsync(new MlList()));
        Assert.IsFalse(rig.Workspace.IsBusy);
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => Assert.Fail("Stale delivery")));
    }

    [TestMethod]
    public async Task PendingAdmissionRefusesTransferAndSwitchBeforeAnyJobUpdateExists()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "before pending work");
        var finish = NewSource<MlResult>();
        rig.Lab.Run = () => finish.Task;
        var work = rig.Selected.Service.ExecuteAsync(new MlList());
        try
        {
            Assert.IsTrue(rig.Workspace.IsBusy);
            Assert.HasCount(0, rig.Selected.Service.Jobs);
            Assert.Throws<MlException>(() => rig.Workspace.PrepareHandoff(rig.A.Id, "while active"));
            Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => Assert.Fail("Busy delivery")));
            await Assert.ThrowsAsync<MlException>(() => rig.Workspace.OpenAsync(rig.B.Id));
        }
        finally { finish.TrySetResult(Result()); }
        await work.WaitAsync(Deadline);
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => Assert.Fail("Refused ticket replay")));
    }

    [TestMethod]
    public async Task ShutdownInvalidatesPendingTransferAndDisposesItsServiceOnce()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "PRIVATE_SHUTDOWN_REQUEST");
        await rig.Workspace.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => rig.Workspace.AcceptHandoff(ticket, _ => Assert.Fail("Shutdown delivery")));
        Assert.IsNull(ticket.Request);
        Assert.Throws<ObjectDisposedException>(() => rig.Workspace.PrepareHandoff(rig.A.Id, "after shutdown"));
        Assert.AreEqual(1, rig.Lab.Disposals);
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task ConcurrentDuplicateConfirmationsDeliverAtMostOnce()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "one exact request");
        var deliveries = 0;
        var refused = 0;
        var start = NewSource<bool>();
        var attempts = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { rig.Workspace.AcceptHandoff(ticket, _ => Interlocked.Increment(ref deliveries)); }
            catch (MlException) { Interlocked.Increment(ref refused); }
        })).ToArray();
        start.SetResult(true);
        await Task.WhenAll(attempts).WaitAsync(Deadline);
        Assert.AreEqual(1, deliveries);
        Assert.AreEqual(11, refused);
        Assert.IsNull(ticket.Request);
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task FailedDeliveryConsumesTheRequestRatherThanRetryingImplicitly()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "only this UI attempt");
        Assert.Throws<InvalidOperationException>(() => rig.Workspace.AcceptHandoff(ticket, _ => throw new InvalidOperationException("view closed")));
        Assert.IsNull(ticket.Request);
        Assert.Throws<MlException>(() => rig.Workspace.AcceptHandoff(ticket, _ => Assert.Fail("No automatic retry")));
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task SynchronousDeliveryCannotReenterWorkerAdmissionOrProjectSwitch()
    {
        await using var rig = await Rig.CreateAsync();
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, "populate UI only");
        var delivered = false;
        rig.Workspace.AcceptHandoff(ticket, _ =>
        {
            Assert.Throws<MlException>(() => rig.Selected.Service.ExecuteAsync(new MlList()));
            Assert.Throws<MlException>(() => rig.Workspace.OpenAsync(rig.B.Id));
            delivered = true;
        });
        Assert.IsTrue(delivered);
        Assert.AreEqual(0, rig.Lab.Executions);
        Assert.AreEqual(rig.A.Id, rig.Workspace.Current!.Project.Id);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t\r\n ")]
    [DataRow("text\0secret")]
    [DataRow("hidden\u202erequest")]
    [DataRow("hidden\u2066request")]
    public async Task MalformedRequestsAreRefusedBeforeAnyExecutionAndWithoutEcho(string request)
    {
        await using var rig = await Rig.CreateAsync();
        var error = Assert.Throws<ArgumentException>(() => rig.Workspace.PrepareHandoff(rig.A.Id, request));
        if (!string.IsNullOrWhiteSpace(request)) Assert.IsFalse(error.ToString().Contains(request, StringComparison.Ordinal));
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task Utf16AndLengthBoundariesPreserveValidDataWithoutNormalization()
    {
        await using var rig = await Rig.CreateAsync();
        foreach (var invalid in new[] { new string('x', 2001), "bad" + (char)0xd800, "bad" + (char)0xdc00, "hidden" + char.ConvertFromUtf32(0xe0001) })
            Assert.Throws<ArgumentException>(() => rig.Workspace.PrepareHandoff(rig.A.Id, invalid));
        var exact = new string('μ', 1998) + char.ConvertFromUtf32(0x1f52c);
        Assert.AreEqual(2000, exact.Length);
        using var ticket = rig.Workspace.PrepareHandoff(rig.A.Id, exact);
        string? transferred = null;
        rig.Workspace.AcceptHandoff(ticket, request => transferred = request);
        Assert.AreEqual(exact, transferred);
        Assert.AreEqual(0, rig.Lab.Executions);
    }

    [TestMethod]
    public async Task RealServiceWithMissingRuntimeTransfersWithoutStartingAJobThenFailsOnlyOnExplicitAction()
    {
        using var files = new TestDirectory();
        var store = new JsonResearchProjectStore(files.PathValue);
        var project = await store.CreateAsync("Missing runtime");
        await using var workspace = new ResearchLabWorkspace(store, p => new LocalMlLabService(
            files.FilePath("missing/python.exe"), files.FilePath("missing/worker.py"), p.Directory));
        var selection = await workspace.OpenAsync(project.Id);
        using var ticket = workspace.PrepareHandoff(project.Id, "A request is data, even without optional runtimes.");
        string? delivered = null;
        workspace.AcceptHandoff(ticket, text => delivered = text);
        Assert.IsNotNull(delivered);
        Assert.HasCount(0, selection.Service.Jobs);
        Assert.IsFalse(Directory.Exists(project.Directory));
        var error = await Assert.ThrowsAsync<MlException>(() => selection.Service.ExecuteAsync(new MlCapabilities()).WaitAsync(Deadline));
        Assert.AreEqual("missing_runtime", error.Code);
        Assert.HasCount(1, selection.Service.Jobs);
        Assert.AreEqual(MlJobState.Failed, selection.Service.Jobs[0].State);
        Assert.IsFalse(Directory.Exists(project.Directory));
    }

    [TestMethod]
    [DataRow("inspect")]
    [DataRow("browse")]
    [DataRow("train")]
    [DataRow("predict")]
    public async Task PlannerCannotInventAFileSelectionWhenNoDatasetWasCaptured(string operation)
    {
        var dataset = new MlDatasetSelection(Path.GetFullPath("PRIVATE_MODEL_INVENTED.csv"), "events", 3, 90);
        var action = DatasetAction(operation, dataset);
        var model = new Model(Payload(action));
        var error = await Assert.ThrowsAsync<MlException>(() => new MlConversationPlanner(model).PlanAsync(
            "Use PRIVATE_MODEL_INVENTED.csv; this request grants no file permission.", null, [], "Research"));
        Assert.AreEqual("proposal", error.Code);
        Assert.IsFalse(error.ToString().Contains("PRIVATE_MODEL_INVENTED", StringComparison.Ordinal));
        Assert.AreEqual(1, model.Calls);
    }

    [TestMethod]
    [DataRow("inspect")]
    [DataRow("browse")]
    [DataRow("train")]
    [DataRow("predict")]
    public async Task PlannerCannotExpandPathKeyStartOrRowsBeyondTheCapturedSnapshot(string operation)
    {
        var captured = new MlDatasetSelection(Path.GetFullPath("captured.csv"), "events", 11, 75);
        MlDatasetSelection[] expanded =
        [
            captured with { Path = Path.GetFullPath("OTHER_PROJECT_PRIVATE.csv") },
            captured with { Key = "OTHER_DATASET_KEY" },
            captured with { Start = 0 },
            captured with { RowLimit = 76 }
        ];
        foreach (var selection in expanded)
        {
            var model = new Model(Payload(DatasetAction(operation, selection)));
            var error = await Assert.ThrowsAsync<MlException>(() => new MlConversationPlanner(model).PlanAsync(
                "Use only this captured dataset.", captured, ["x", "y"], "Research"));
            Assert.AreEqual("proposal", error.Code);
            Assert.IsFalse(error.ToString().Contains("OTHER_PROJECT_PRIVATE", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [DataRow("inspect")]
    [DataRow("browse")]
    [DataRow("train")]
    [DataRow("predict")]
    public async Task PlannerPreservesValidatedDatasetSnapshotAndOnlyTheExactRequest(string operation)
    {
        var captured = new MlDatasetSelection(Path.GetFullPath("captured.csv"), "events", 11, 75);
        const string prompt = "  Assess μ with the captured selection.\r\nDo not infer a path.  ";
        var model = new Model(Payload(DatasetAction(operation, captured)));
        var result = await new MlConversationPlanner(model).PlanAsync(prompt, captured, ["x", "y"], "Research");
        Assert.AreEqual(captured, ActionDataset(result.Action));
        Assert.HasCount(2, model.Messages);
        Assert.AreEqual(ChatRole.System, model.Messages[0].Role);
        Assert.AreEqual(ChatRole.User, model.Messages[1].Role);
        Assert.AreEqual(prompt, model.Messages[1].Content);
    }

    private static MlAction DatasetAction(string operation, MlDatasetSelection selection) => operation switch
    {
        "inspect" => new MlInspect(selection), "browse" => new MlBrowse(selection),
        "train" => new MlTrain(selection, "regression", "ridge", ["x"], "y"),
        "predict" => new MlPredict("existing-model", selection), _ => throw new ArgumentException("Unknown fixture action.")
    };
    private static MlDatasetSelection? ActionDataset(MlAction action) => action switch
    { MlInspect a => a.Dataset, MlBrowse a => a.Dataset, MlTrain a => a.Dataset, MlPredict a => a.Dataset, _ => null };
    private static string Payload(MlAction action) => JsonSerializer.Serialize(new
    { action = action.Operation, parameters = MlActionCodec.Parameters(action), reason = "A controlled typed proposal fixture, not qualified inference." }, MlActionCodec.Json);
    private static MlResult Result() => new(Guid.NewGuid(), "list", JsonSerializer.SerializeToElement(new { experiments = Array.Empty<object>() }));
    private static TaskCompletionSource<T> NewSource<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Model(string output) : IChatModel
    {
        public int Calls { get; private set; }
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++; Messages = messages.ToArray();
            await Task.Yield();
            yield return new ChatTextDelta(output);
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }
    private sealed class Lab(string directory) : IMlLabService
    {
        public string ProjectDirectory => directory;
        public IReadOnlyList<MlJobUpdate> Jobs => [];
        public int Executions { get; private set; }
        public int Disposals { get; private set; }
        public Func<Task<MlResult>> Run { get; set; } = () => Task.FromResult(Result());
        public event Action<MlJobUpdate>? JobChanged { add { } remove { } }
        public void Cancel(Guid jobId) { }
        public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
        { Executions++; return Run(); }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    private sealed class Rig : IAsyncDisposable
    {
        public TestDirectory Files { get; } = new();
        public JsonResearchProjectStore Store { get; private set; } = null!;
        public ResearchProject A { get; private set; } = null!;
        public ResearchProject B { get; private set; } = null!;
        public ResearchLabWorkspace Workspace { get; private set; } = null!;
        public ResearchLabSelection Selected { get; private set; } = null!;
        public Lab Lab { get; private set; } = null!;
        public int FactoryCalls { get; private set; }
        public string Catalog => Files.FilePath("ml-lab/research-projects.json");
        public static async Task<Rig> CreateAsync()
        {
            var rig = new Rig();
            rig.Store = new(rig.Files.PathValue);
            rig.A = await rig.Store.CreateAsync("Same visible name");
            rig.B = await rig.Store.CreateAsync("Same visible name");
            rig.Workspace = new(rig.Store, project =>
            {
                rig.FactoryCalls++;
                var lab = new Lab(project.Directory);
                if (rig.Lab is null) rig.Lab = lab;
                return lab;
            });
            rig.Selected = await rig.Workspace.OpenAsync(rig.A.Id);
            return rig;
        }
        public async ValueTask DisposeAsync() { await Workspace.DisposeAsync(); Files.Dispose(); }
    }
}
