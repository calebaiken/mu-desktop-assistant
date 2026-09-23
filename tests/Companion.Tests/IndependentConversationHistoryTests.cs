using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Reviewer-authored storage checks, independent of the repository implementation.</summary>
[TestClass]
public sealed class IndependentConversationHistoryTests
{
    [TestMethod]
    public async Task SqliteSearchTreatsWildcardsQuotesAndSqlFragmentsAsLiteralText()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var ordinary = await store.CreateAsync("ordinary");
        var punctuation = await store.CreateAsync("quotes ' and percent % and underscore _");
        const string injection = "'); DROP TABLE conversations; --";
        await store.AppendTurnAsync(punctuation.Id, null, "literal [brackets] and \\ slash", injection, TurnOutcome.Completed);
        foreach (var search in new[] { "%", "_", "'", "[brackets]", "\\", injection })
        {
            var found = await store.ListAsync(search);
            Assert.AreEqual(1, found.Count, $"Literal fixture search: {search}");
            Assert.AreEqual(punctuation.Id, found[0].Id);
        }
        Assert.IsNotNull(await new SqliteConversationStore(directory.PathValue).LoadAsync(ordinary.Id));
        Assert.AreEqual(2, (await store.ListAsync()).Count);
        Assert.AreEqual(0, (await store.ListAsync("' OR 1=1 --")).Count);
    }

    [TestMethod]
    public async Task CrossConversationParentAndLeafCannotMutateEitherSavedTree()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var first = await store.CreateAsync("first");
        first = await store.AppendTurnAsync(first.Id, null, "first prompt", "first answer", TurnOutcome.Completed);
        var second = await store.CreateAsync("second");
        second = await store.AppendTurnAsync(second.Id, null, "second prompt", "second answer", TurnOutcome.Completed);
        var foreignId = first.LeafTurnId;
        var append = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            store.AppendTurnAsync(second.Id, foreignId, "must not enter database", "must not enter database", TurnOutcome.Completed));
        Assert.AreEqual(ConversationStoreFailure.NotFound, append.Failure);
        var select = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() => store.SelectBranchAsync(second.Id, foreignId));
        Assert.AreEqual(ConversationStoreFailure.NotFound, select.Failure);
        var reopened = new SqliteConversationStore(directory.PathValue);
        AssertSameTree(first, (await reopened.LoadAsync(first.Id))!);
        AssertSameTree(second, (await reopened.LoadAsync(second.Id))!);
        Assert.AreEqual(0, (await reopened.ListAsync("must not enter database")).Count);
    }

    [TestMethod]
    public async Task ForkPreservesOriginalTurnRecordsAndSnapshotsAcrossRestart()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var conversation = await store.CreateAsync("fork fixture");
        var original = await store.AppendTurnAsync(conversation.Id, null, "original root", "root answer", TurnOutcome.Completed);
        var oldBranch = await store.AppendTurnAsync(conversation.Id, original.LeafTurnId, "old followup", "old answer", TurnOutcome.Completed);
        var forked = await store.AppendTurnAsync(conversation.Id, original.LeafTurnId, "edited followup", "new answer", TurnOutcome.Completed);
        Assert.AreEqual(1, original.Turns.Count, "Previously returned snapshots remain immutable.");
        Assert.AreEqual(2, oldBranch.Turns.Count);
        CollectionAssert.AreEqual(new[] { "original root", "edited followup" },
            ConversationHistory.GetActiveBranch(forked).Select(turn => turn.Prompt).ToArray());
        var reopened = new SqliteConversationStore(directory.PathValue);
        var restored = await reopened.SelectBranchAsync(conversation.Id, oldBranch.LeafTurnId);
        CollectionAssert.AreEqual(new[] { "original root", "old followup" },
            ConversationHistory.GetActiveBranch(restored).Select(turn => turn.Prompt).ToArray());
        foreach (var oldTurn in oldBranch.Turns)
            AssertSameTurn(oldTurn, restored.Turns.Single(turn => turn.Id == oldTurn.Id));
        Assert.AreEqual(3, restored.Turns.Count);
        Assert.AreEqual("new answer", restored.Turns.Single(turn => turn.Id == forked.LeafTurnId).Response);
    }

    [TestMethod]
    public void ContextUsesSelectedAncestryInOrderAndExcludesInterruptedAndAbandonedText()
    {
        var now = DateTimeOffset.UtcNow;
        var root = new ConversationTurn(Guid.NewGuid(), null, "root question", "root answer", TurnOutcome.Completed, now);
        var abandoned = new ConversationTurn(Guid.NewGuid(), root.Id, "abandoned private question", "abandoned private answer", TurnOutcome.Completed, now);
        var interrupted = new ConversationTurn(Guid.NewGuid(), root.Id, "stopped question", "stopped partial", TurnOutcome.Interrupted, now);
        var leaf = new ConversationTurn(Guid.NewGuid(), interrupted.Id, "selected question", "selected answer", TurnOutcome.Completed, now);
        // Storage order deliberately disagrees with ancestry order.
        var snapshot = new ConversationSnapshot(Guid.NewGuid(), "tree", now, leaf.Id, [leaf, abandoned, root, interrupted]);
        var context = ConversationHistory.BuildContext(snapshot, "new question");
        CollectionAssert.AreEqual(new[] { "root question", "root answer", "selected question", "selected answer", "new question" },
            context.Select(message => message.Content).ToArray());
        CollectionAssert.AreEqual(new[] { ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Assistant, ChatRole.User },
            context.Select(message => message.Role).ToArray());
    }

    [TestMethod]
    public void MultibyteContextBudgetDropsWholeOldPairsUsingIndependentExpectedCounts()
    {
        var turns = new List<ConversationTurn>();
        Guid? previous = null;
        for (var index = 0; index < 6; index++)
        {
            var turn = new ConversationTurn(Guid.NewGuid(), previous, index + new string('€', 1000),
                index + new string('€', 1000), TurnOutcome.Completed, DateTimeOffset.UtcNow);
            turns.Add(turn);
            previous = turn.Id;
        }
        var snapshot = new ConversationSnapshot(Guid.NewGuid(), "byte fixture", DateTimeOffset.UtcNow, previous, turns);
        var context = ConversationHistory.BuildContext(snapshot, new string('€', 1000));
        // Each complete pair is 6002 UTF-8 bytes; the new prompt is 3000 bytes.
        // Four pairs require 27008 > 24576; three require 21006 <= 24576.
        Assert.AreEqual(7, context.Count);
        Assert.AreEqual(turns[3].Prompt, context[0].Content);
        Assert.AreEqual(turns[5].Response, context[^2].Content);
        Assert.AreEqual(21006, context.Sum(message => System.Text.Encoding.UTF8.GetByteCount(message.Content)));
    }

    [TestMethod]
    public void CyclicOrMissingSelectedAncestryFailsWithoutInfiniteTraversal()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var first = new ConversationTurn(firstId, secondId, "one", "one", TurnOutcome.Completed, now);
        var second = new ConversationTurn(secondId, firstId, "two", "two", TurnOutcome.Completed, now);
        foreach (var snapshot in new[]
        {
            new ConversationSnapshot(Guid.NewGuid(), "cycle", now, firstId, [first, second]),
            new ConversationSnapshot(Guid.NewGuid(), "missing", now, firstId, [first]),
            new ConversationSnapshot(Guid.NewGuid(), "duplicate", now, firstId, [first, first])
        })
        {
            var error = Assert.ThrowsExactly<ConversationStoreException>(() => ConversationHistory.GetActiveBranch(snapshot));
            Assert.AreEqual(ConversationStoreFailure.Corrupt, error.Failure);
        }
    }

    [TestMethod]
    public async Task TemporaryRepositoryInstancesDoNotShareOrPersistSentinelContent()
    {
        using var directory = new TestDirectory();
        var saved = new SqliteConversationStore(directory.PathValue);
        await saved.CreateAsync("saved baseline");
        var before = Directory.GetFiles(directory.PathValue).ToDictionary(file => Path.GetFileName(file)!, File.ReadAllBytes);
        var sentinel = "temporary-" + Guid.NewGuid().ToString("N");
        var temporary = new InMemoryConversationStore();
        var conversation = await temporary.CreateAsync(sentinel);
        await temporary.AppendTurnAsync(conversation.Id, null, sentinel, sentinel, TurnOutcome.Completed);
        Assert.AreEqual(1, (await temporary.ListAsync(sentinel)).Count);
        Assert.AreEqual(0, (await new InMemoryConversationStore().ListAsync(sentinel)).Count);
        Assert.IsNull(await new InMemoryConversationStore().LoadAsync(conversation.Id));
        Assert.AreEqual(0, (await saved.ListAsync(sentinel)).Count);
        // This proves repository separation only; the real app crash sentinel is a separate required check.
        var after = Directory.GetFiles(directory.PathValue).ToDictionary(file => Path.GetFileName(file)!, File.ReadAllBytes);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray());
        foreach (var (name, bytes) in before) CollectionAssert.AreEqual(bytes, after[name]);
    }

    [TestMethod]
    public async Task NewerSchemaIsRejectedWithoutChangingAnyDatabaseBytes()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var existing = await store.CreateAsync("preserve this");
        var file = Path.Combine(directory.PathValue, "conversations.db");
        using (var connection = Open(file))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version=2147483647;";
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(file);
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(directory.PathValue).LoadAsync(existing.Id));
        Assert.AreEqual(ConversationStoreFailure.UnsupportedSchema, error.Failure);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        Assert.IsNull(error.InnerException);
        Assert.IsFalse(error.ToString().Contains("preserve this", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CorruptDatabaseRemainsByteIdenticalAndErrorsExcludeContentAndPath()
    {
        using var directory = new TestDirectory();
        var file = Path.Combine(directory.PathValue, "conversations.db");
        var sentinel = "private-corrupt-content-" + Guid.NewGuid().ToString("N");
        var bytes = System.Text.Encoding.UTF8.GetBytes(sentinel);
        await File.WriteAllBytesAsync(file, bytes);
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(directory.PathValue).ListAsync());
        Assert.AreEqual(ConversationStoreFailure.Corrupt, error.Failure);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(file));
        Assert.IsNull(error.InnerException);
        Assert.IsFalse(error.ToString().Contains(sentinel, StringComparison.Ordinal));
        Assert.IsFalse(error.ToString().Contains(directory.PathValue, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SqliteAbortBetweenTurnInsertAndLeafUpdateRollsBackBothChanges()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var original = await store.CreateAsync("atomic fixture");
        original = await store.AppendTurnAsync(original.Id, null, "prior question", "prior answer", TurnOutcome.Completed);
        var file = Path.Combine(directory.PathValue, "conversations.db");
        using (var connection = Open(file))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER independent_abort_leaf BEFORE UPDATE OF leaf_turn_id ON conversations
                BEGIN SELECT RAISE(ABORT,'private fixture failure'); END;
                """;
            command.ExecuteNonQuery();
        }
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() => store.AppendTurnAsync(original.Id,
            original.LeafTurnId, "must roll back", "must roll back", TurnOutcome.Completed));
        Assert.IsNull(error.InnerException);
        Assert.IsFalse(error.ToString().Contains("private fixture failure", StringComparison.Ordinal));
        using (var connection = Open(file))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TRIGGER independent_abort_leaf";
            command.ExecuteNonQuery();
        }
        var reopened = new SqliteConversationStore(directory.PathValue);
        AssertSameTree(original, (await reopened.LoadAsync(original.Id))!);
        Assert.AreEqual(0, (await reopened.ListAsync("must roll back")).Count);
        var next = await reopened.AppendTurnAsync(original.Id, original.LeafTurnId, "after failure", "usable again", TurnOutcome.Completed);
        Assert.AreEqual(2, next.Turns.Count);
    }

    [TestMethod]
    public async Task ExclusiveDatabaseLockFailsWithinBoundAndPreservesPreviousTitle()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var original = await store.CreateAsync("before lock");
        using (var connection = Open(Path.Combine(directory.PathValue, "conversations.db")))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "BEGIN EXCLUSIVE";
            command.ExecuteNonQuery();
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
                    store.RenameAsync(original.Id, "must not commit", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.AreEqual(ConversationStoreFailure.Unavailable, error.Failure);
            }
            finally
            {
                command.CommandText = "ROLLBACK";
                command.ExecuteNonQuery();
            }
        }
        Assert.AreEqual("before lock", (await new SqliteConversationStore(directory.PathValue).LoadAsync(original.Id))!.Title);
        await store.RenameAsync(original.Id, "after lock");
        Assert.AreEqual("after lock", (await store.LoadAsync(original.Id))!.Title);
    }

    [TestMethod]
    public async Task CorruptUnselectedBranchIsRejectedAndPreserved()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var conversation = await store.CreateAsync("all branches validated");
        var root = await store.AppendTurnAsync(conversation.Id, null, "root", "root", TurnOutcome.Completed);
        var abandoned = await store.AppendTurnAsync(conversation.Id, root.LeafTurnId, "unselected", "unselected", TurnOutcome.Completed);
        await store.AppendTurnAsync(conversation.Id, root.LeafTurnId, "selected", "selected", TurnOutcome.Completed);
        var file = Path.Combine(directory.PathValue, "conversations.db");
        using (var connection = Open(file))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE turns SET parent_turn_id=id WHERE id=$id";
            command.Parameters.AddWithValue("$id", abandoned.LeafTurnId!.Value.ToString("D"));
            command.ExecuteNonQuery();
        }
        var bytes = File.ReadAllBytes(file);
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(directory.PathValue).LoadAsync(conversation.Id));
        Assert.AreEqual(ConversationStoreFailure.Corrupt, error.Failure);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(file));
    }

    [TestMethod]
    public async Task DeleteRemovesEveryBranchFromSearchAndRetainsOtherConversations()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var conversation = await store.CreateAsync("to delete");
        var first = await store.AppendTurnAsync(conversation.Id, null, "unique_deleted_sentinel", "first branch", TurnOutcome.Completed);
        await store.AppendTurnAsync(conversation.Id, null, "alternate", "unique_deleted_sentinel", TurnOutcome.Completed);
        await store.SelectBranchAsync(conversation.Id, first.LeafTurnId);
        var retained = await store.CreateAsync("retained");
        await store.DeleteAsync(conversation.Id);
        var reopened = new SqliteConversationStore(directory.PathValue);
        Assert.IsNull(await reopened.LoadAsync(conversation.Id));
        Assert.AreEqual(0, (await reopened.ListAsync("unique_deleted_sentinel")).Count);
        Assert.IsNotNull(await reopened.LoadAsync(retained.Id));
        Assert.AreEqual(1, (await reopened.ListAsync()).Count);
        using var connection = Open(Path.Combine(directory.PathValue, "conversations.db"));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM turns WHERE conversation_id=$id";
        command.Parameters.AddWithValue("$id", conversation.Id.ToString("D"));
        Assert.AreEqual(0L, (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    public async Task RealSqliteFullAtSmallPageCeilingPreservesPreviouslyCommittedTree()
    {
        using var directory = new TestDirectory();
        var store = new SqliteConversationStore(directory.PathValue);
        var original = await store.CreateAsync("small disk fixture");
        original = await store.AppendTurnAsync(original.Id, null, "before full", "still present", TurnOutcome.Completed);
        var file = Path.Combine(directory.PathValue, "conversations.db");
        var ceiling = Math.Max(32768L, new FileInfo(file).Length);
        Assert.IsTrue(ceiling <= 65536, "Fixture begins with a small actual SQLite file.");
        var constrained = new SqliteConversationStore(directory.PathValue, ceiling);
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() => constrained.AppendTurnAsync(original.Id,
            original.LeafTurnId, new string('界', 2000), new string('界', 8192), TurnOutcome.Completed));
        Assert.AreEqual(ConversationStoreFailure.LimitReached, error.Failure);
        Assert.IsTrue(new FileInfo(file).Length <= ceiling);
        var restored = await new SqliteConversationStore(directory.PathValue).LoadAsync(original.Id);
        AssertSameTree(original, restored!);
        Assert.AreEqual(0, (await store.ListAsync(new string('界', 20))).Count);
    }

    [TestMethod]
    public async Task UnicodeCaseInsensitiveLiteralSearchAgreesInTemporaryAndSavedStores()
    {
        using var directory = new TestDirectory();
        IConversationStore[] stores = [new InMemoryConversationStore(), new SqliteConversationStore(directory.PathValue)];
        foreach (var store in stores)
        {
            var conversation = await store.CreateAsync("CAFÉ ΩMEGA");
            await store.AppendTurnAsync(conversation.Id, null, "Äpfel [literal]", "Éclair", TurnOutcome.Completed);
            foreach (var term in new[] { "café", "ωmega", "äPFEL", "éCLAIR", "[literal]" })
            {
                var result = await store.ListAsync(term);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(conversation.Id, result[0].Id);
            }
        }
    }

    [TestMethod]
    public void UncAndRelativeProfilesAreRejectedBeforeAnyDatabaseOperation()
    {
        foreach (var profile in new[] { "relative-profile", @"\\server\share\profile", "//server/share/profile" })
            Assert.ThrowsExactly<ArgumentException>(() => new SqliteConversationStore(profile));
    }

    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = file, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static void AssertSameTree(ConversationSnapshot expected, ConversationSnapshot actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(expected.Title, actual.Title);
        Assert.AreEqual(expected.LeafTurnId, actual.LeafTurnId);
        Assert.AreEqual(expected.Turns.Count, actual.Turns.Count);
        foreach (var turn in expected.Turns) AssertSameTurn(turn, actual.Turns.Single(item => item.Id == turn.Id));
    }

    private static void AssertSameTurn(ConversationTurn expected, ConversationTurn actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(expected.ParentTurnId, actual.ParentTurnId);
        Assert.AreEqual(expected.Prompt, actual.Prompt);
        Assert.AreEqual(expected.Response, actual.Response);
        Assert.AreEqual(expected.Outcome, actual.Outcome);
        Assert.AreEqual(expected.CreatedAt.ToUnixTimeMilliseconds(), actual.CreatedAt.ToUnixTimeMilliseconds());
    }
}
