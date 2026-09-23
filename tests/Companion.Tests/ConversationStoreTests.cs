using System.Text;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class ConversationStoreTests
{
    private string directory = null!;

    [TestInitialize]
    public void Initialize() => directory = Path.Combine(Path.GetTempPath(), "companion-conversation-tests-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EditAndRegenerationPreserveOriginalBranchesAcrossReload(bool durable)
    {
        var store = Store(durable);
        var conversation = await store.CreateAsync("A conversation 🌍");
        var first = await store.AppendTurnAsync(conversation.Id, null, "Original prompt", "Original reply", TurnOutcome.Completed);
        var originalId = first.LeafTurnId!.Value;
        var second = await store.AppendTurnAsync(conversation.Id, originalId, "Follow up", "Follow up reply", TurnOutcome.Completed);
        var followUpId = second.LeafTurnId!.Value;
        var edited = await store.AppendTurnAsync(conversation.Id, null, "Edited prompt", "Edited reply", TurnOutcome.Completed);
        var editedId = edited.LeafTurnId!.Value;
        var regenerated = await store.AppendTurnAsync(conversation.Id, null, "Edited prompt", "Different reply", TurnOutcome.Completed);
        Assert.AreEqual(4, regenerated.Turns.Count);
        Assert.AreEqual("Different reply", ConversationHistory.GetActiveBranch(regenerated).Single().Response);
        Assert.AreEqual("Original reply", first.Turns.Single().Response);
        Assert.AreEqual(1, first.Turns.Count, "Older immutable snapshots must not change after appending branches.");

        var selected = await store.SelectBranchAsync(conversation.Id, followUpId);
        CollectionAssert.AreEqual(new[] { originalId, followUpId }, ConversationHistory.GetActiveBranch(selected).Select(turn => turn.Id).ToArray());
        var reopened = durable ? Store(true) : store;
        var loaded = await reopened.LoadAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(followUpId, loaded.LeafTurnId);
        Assert.AreEqual("A conversation 🌍", loaded.Title);
        Assert.AreEqual("Edited reply", loaded.Turns.Single(turn => turn.Id == editedId).Response);
        Assert.AreEqual(TurnOutcome.Completed, loaded.Turns.Single(turn => turn.Id == editedId).Outcome);
        await reopened.RenameAsync(conversation.Id, "Renamed locally");
        Assert.AreEqual("Renamed locally", (await reopened.LoadAsync(conversation.Id))!.Title);
        await reopened.DeleteAsync(conversation.Id);
        Assert.IsNull(await reopened.LoadAsync(conversation.Id));
        Assert.AreEqual(0, (await reopened.ListAsync()).Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InterruptedAndFailedRepliesRemainVisibleButAreExcludedFromFutureModelContext(bool durable)
    {
        var store = Store(durable);
        var conversation = await store.CreateAsync("Interrupted examples");
        conversation = await store.AppendTurnAsync(conversation.Id, null, "Keep this", "Known answer", TurnOutcome.Completed);
        conversation = await store.AppendTurnAsync(conversation.Id, conversation.LeafTurnId, "Stopped prompt", "Partial output", TurnOutcome.Interrupted);
        conversation = await store.AppendTurnAsync(conversation.Id, conversation.LeafTurnId, "Failed prompt", "", TurnOutcome.Failed);
        conversation = await store.AppendTurnAsync(conversation.Id, conversation.LeafTurnId, "Continue", "Next answer", TurnOutcome.Completed);
        var loaded = await store.LoadAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(4, ConversationHistory.GetActiveBranch(loaded).Count);
        Assert.AreEqual("Partial output", loaded.Turns[1].Response);
        CollectionAssert.AreEqual(new[] { "Keep this", "Known answer", "Continue", "Next answer", "Now?" },
            ConversationHistory.BuildContext(loaded, "Now?").Select(message => message.Content).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PendingPromptSurvivesReopenAsInterruptedWithOriginalIdentity(bool durable)
    {
        var store = Store(durable);
        var conversation = await store.CreateAsync("Crash recovery");
        conversation = await store.AppendTurnAsync(conversation.Id, null, "A saved prompt before inference", "", TurnOutcome.InProgress);
        var pending = conversation.Turns.Single();
        Assert.AreEqual(TurnOutcome.InProgress, pending.Outcome);
        var restarted = durable ? Store(true) : store;
        var recovered = await restarted.LoadAsync(conversation.Id);
        Assert.IsNotNull(recovered);
        Assert.AreEqual(pending.Id, recovered.LeafTurnId);
        Assert.AreEqual(pending.Id, recovered.Turns.Single().Id);
        Assert.AreEqual(pending.Prompt, recovered.Turns.Single().Prompt);
        Assert.AreEqual(TurnOutcome.Interrupted, recovered.Turns.Single().Outcome);
        Assert.AreEqual(1, ConversationHistory.BuildContext(recovered, "Retry now").Count);
        Assert.AreEqual(TurnOutcome.Interrupted, (await restarted.LoadAsync(conversation.Id))!.Turns.Single().Outcome);
        var error = await Assert.ThrowsAsync<ConversationStoreException>(() => restarted.CompleteTurnAsync(conversation.Id,
            pending.Id, "A late result must not overwrite recovery", TurnOutcome.Completed));
        Assert.AreEqual(ConversationStoreFailure.Conflict, error.Failure);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PendingTurnCompletesAtomicallyAndTerminalContentCannotBeReplaced(bool durable)
    {
        var store = Store(durable);
        var conversation = await store.CreateAsync("Completion");
        conversation = await store.AppendTurnAsync(conversation.Id, null, "Prompt", "", TurnOutcome.InProgress);
        var turnId = conversation.LeafTurnId!.Value;
        var busy = await Assert.ThrowsAsync<ConversationStoreException>(() => store.AppendTurnAsync(conversation.Id, turnId,
            "Another prompt", "", TurnOutcome.InProgress));
        Assert.AreEqual(ConversationStoreFailure.Conflict, busy.Failure);
        var completed = await store.CompleteTurnAsync(conversation.Id, turnId, "Complete answer 🌎", TurnOutcome.Completed);
        Assert.AreEqual(turnId, completed.LeafTurnId);
        Assert.AreEqual(1, completed.Turns.Count);
        Assert.AreEqual("Prompt", completed.Turns.Single().Prompt);
        Assert.AreEqual("Complete answer 🌎", completed.Turns.Single().Response);
        Assert.AreEqual(TurnOutcome.Completed, (await store.LoadAsync(conversation.Id))!.Turns.Single().Outcome);
        var repeated = await Assert.ThrowsAsync<ConversationStoreException>(() => store.CompleteTurnAsync(conversation.Id,
            turnId, "Overwrite attempt", TurnOutcome.Completed));
        Assert.AreEqual(ConversationStoreFailure.Conflict, repeated.Failure);
        Assert.AreEqual("Complete answer 🌎", (await store.LoadAsync(conversation.Id))!.Turns.Single().Response);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConcurrentAppendsPreserveEverySiblingAndDeletionDoesNotAffectOtherConversations(bool durable)
    {
        var store = Store(durable);
        var first = await store.CreateAsync("First");
        var second = await store.CreateAsync("Second");
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => store.AppendTurnAsync(first.Id, null,
            $"Prompt {index}", $"Answer {index}", TurnOutcome.Completed)));
        var loaded = await store.LoadAsync(first.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(12, loaded.Turns.Count);
        Assert.AreEqual(12, loaded.Turns.Select(turn => turn.Id).Distinct().Count());
        Assert.AreEqual(1, ConversationHistory.GetActiveBranch(loaded).Count);
        await store.DeleteAsync(first.Id);
        Assert.IsNotNull(await store.LoadAsync(second.Id));
        Assert.AreEqual(second.Id, (await store.ListAsync()).Single().Id);
    }

    [TestMethod]
    public async Task PreCancelledOperationsDoNotCreateDatabaseOrProfileDirectory()
    {
        var store = Store(true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.CreateAsync("Canceled", cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.ListAsync(null, cancellation.Token));
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public async Task InvalidInputsAreRejectedBeforeCreatingDurableStorage()
    {
        var store = Store(true);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(new string('T', 81)));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("Multiline\ntitle"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ListAsync(new string('S', 121)));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AppendTurnAsync(Guid.NewGuid(), null, new string('P', 2001), "Reply", TurnOutcome.Completed));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AppendTurnAsync(Guid.NewGuid(), null, "Prompt", new string('R', 8193), TurnOutcome.Completed));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AppendTurnAsync(Guid.NewGuid(), null, "Prompt", "", TurnOutcome.Completed));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AppendTurnAsync(Guid.NewGuid(), null, "Prompt", "Unexpected pending content", TurnOutcome.InProgress));
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public void ContextTrimsCompletePairsByUtf8BytesAndKeepsLatestPrompt()
    {
        var turns = new List<ConversationTurn>();
        Guid? parent = null;
        for (var index = 0; index < 20; index++)
        {
            var turn = new ConversationTurn(Guid.NewGuid(), parent, "Question " + index, new string('界', 2000),
                TurnOutcome.Completed, DateTimeOffset.UtcNow);
            turns.Add(turn);
            parent = turn.Id;
        }
        var snapshot = new ConversationSnapshot(Guid.NewGuid(), "Bounded context", DateTimeOffset.UtcNow, parent, turns);
        var request = ConversationHistory.BuildContext(snapshot, "Latest prompt");
        Assert.IsTrue(request.Count <= ConversationHistory.MaximumContextMessages);
        Assert.IsTrue(request.Sum(message => Encoding.UTF8.GetByteCount(message.Content)) <= ConversationHistory.MaximumContextBytes);
        Assert.AreEqual("Latest prompt", request[^1].Content);
        Assert.AreEqual(ChatRole.User, request[^1].Role);
        Assert.AreEqual(1, request.Count % 2);
        for (var index = 0; index < request.Count - 1; index += 2)
        {
            Assert.AreEqual(ChatRole.User, request[index].Role);
            Assert.AreEqual(ChatRole.Assistant, request[index + 1].Role);
        }
        Assert.AreEqual("Question 19", request[^3].Content);
    }

    [TestMethod]
    public void GeneratedTitlesAreSingleLineAndDoNotSplitSurrogatePairs()
    {
        Assert.AreEqual("A title with spaces", ConversationHistory.TitleFromPrompt(" A  title\r\nwith\tspaces "));
        Assert.AreEqual("New conversation", ConversationHistory.TitleFromPrompt(" \r\n"));
        var title = ConversationHistory.TitleFromPrompt(new string('A', 78) + "🌍more");
        Assert.IsTrue(title.Length <= ConversationHistory.MaximumTitleCharacters);
        Assert.IsFalse(title.Any(char.IsSurrogate));
        Assert.IsTrue(title.EndsWith('…'));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConversationLimitIsExplicitAndDeletingOneRestoresCapacity(bool durable)
    {
        var store = Store(durable);
        ConversationSnapshot? first = null;
        for (var index = 0; index < ConversationHistory.MaximumConversations; index++)
        {
            var next = await store.CreateAsync("Conversation " + index);
            first ??= next;
        }
        var error = await Assert.ThrowsAsync<ConversationStoreException>(() => store.CreateAsync("Over the limit"));
        Assert.AreEqual(ConversationStoreFailure.LimitReached, error.Failure);
        Assert.AreEqual(ConversationHistory.MaximumConversations, (await store.ListAsync()).Count);
        await store.DeleteAsync(first!.Id);
        Assert.IsNotNull(await store.CreateAsync("Capacity restored"));
    }

    private IConversationStore Store(bool durable) => durable ? new SqliteConversationStore(directory) : new InMemoryConversationStore();
}
