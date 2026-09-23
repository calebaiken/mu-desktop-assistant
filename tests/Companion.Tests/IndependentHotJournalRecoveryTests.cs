using System.Buffers.Binary;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>SQLite-generated interrupted-write snapshots, not fabricated journal bytes or a process-kill claim.</summary>
[TestClass]
public sealed class IndependentHotJournalRecoveryTests
{
    [TestMethod]
    public async Task ValidHotJournalRecoversCommittedTreeAndDiscardsUncommittedWrites()
    {
        using var original = new TestDirectory();
        using var interrupted = new TestDirectory();
        var expected = await CreateInterruptedSnapshotAsync(original, interrupted);
        AssertReadOnlyRequiresRollback(interrupted);
        var store = new SqliteConversationStore(interrupted.PathValue);
        var restored = await store.LoadAsync(expected.Id);
        Assert.IsNotNull(restored);
        Assert.AreEqual(expected.Title, restored.Title);
        Assert.AreEqual(expected.LeafTurnId, restored.LeafTurnId);
        Assert.AreEqual(expected.Turns.Count, restored.Turns.Count);
        foreach (var before in expected.Turns)
        {
            var after = restored.Turns.Single(turn => turn.Id == before.Id);
            Assert.AreEqual(before.ParentTurnId, after.ParentTurnId);
            Assert.AreEqual(before.Prompt, after.Prompt);
            Assert.AreEqual(before.Response, after.Response);
            Assert.AreEqual(before.Outcome, after.Outcome);
        }
        Assert.AreEqual(0, (await store.ListAsync("UNCOMMITTED_TITLE")).Count);
        await store.RenameAsync(expected.Id, "usable after rollback");
        Assert.AreEqual("usable after rollback", (await new SqliteConversationStore(interrupted.PathValue).LoadAsync(expected.Id))!.Title);
        AssertNoRecoveryScratch(interrupted);
    }

    [TestMethod]
    public async Task UnsupportedPreTransactionSchemaWithHotJournalPreservesBothOriginalFiles()
    {
        using var original = new TestDirectory();
        using var interrupted = new TestDirectory();
        var expected = await CreateInterruptedSnapshotAsync(original, interrupted, beforeTransactionSql: "PRAGMA user_version=2;");
        AssertReadOnlyRequiresRollback(interrupted);
        var main = File.ReadAllBytes(interrupted.FilePath("conversations.db"));
        var journal = File.ReadAllBytes(interrupted.FilePath("conversations.db-journal"));
        var failure = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(interrupted.PathValue).LoadAsync(expected.Id));
        Assert.AreEqual(ConversationStoreFailure.UnsupportedSchema, failure.Failure);
        CollectionAssert.AreEqual(main, File.ReadAllBytes(interrupted.FilePath("conversations.db")));
        CollectionAssert.AreEqual(journal, File.ReadAllBytes(interrupted.FilePath("conversations.db-journal")));
        AssertNoRecoveryScratch(interrupted);
    }

    [TestMethod]
    public async Task CorruptPreTransactionRowsWithHotJournalPreserveBothOriginalFiles()
    {
        using var original = new TestDirectory();
        using var interrupted = new TestDirectory();
        var expected = await CreateInterruptedSnapshotAsync(original, interrupted,
            beforeTransactionSql: "PRAGMA ignore_check_constraints=ON; UPDATE turns SET outcome=99 WHERE rowid=(SELECT min(rowid) FROM turns); PRAGMA ignore_check_constraints=OFF;");
        AssertReadOnlyRequiresRollback(interrupted);
        var main = File.ReadAllBytes(interrupted.FilePath("conversations.db"));
        var journal = File.ReadAllBytes(interrupted.FilePath("conversations.db-journal"));
        var failure = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(interrupted.PathValue).LoadAsync(expected.Id));
        Assert.AreEqual(ConversationStoreFailure.Corrupt, failure.Failure);
        CollectionAssert.AreEqual(main, File.ReadAllBytes(interrupted.FilePath("conversations.db")));
        CollectionAssert.AreEqual(journal, File.ReadAllBytes(interrupted.FilePath("conversations.db-journal")));
        AssertNoRecoveryScratch(interrupted);
    }

    [TestMethod]
    public async Task JournalRequestingGrowthPastDatabaseCapIsRejectedWithoutChangingOriginals()
    {
        using var original = new TestDirectory();
        using var interrupted = new TestDirectory();
        var expected = await CreateInterruptedSnapshotAsync(original, interrupted);
        var journalFile = interrupted.FilePath("conversations.db-journal");
        var journal = File.ReadAllBytes(journalFile);
        // SQLite's rollback-journal header stores the original database page count at offset 16.
        // Request only one page beyond 64 MiB, never an unbounded disk-filling test value.
        var pageSize = BinaryPrimitives.ReadUInt32BigEndian(journal.AsSpan(24, 4));
        Assert.IsTrue(pageSize is >= 512 and <= 65536);
        BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(16, 4), 64 * 1024 * 1024 / pageSize + 1);
        await File.WriteAllBytesAsync(journalFile, journal);
        var main = File.ReadAllBytes(interrupted.FilePath("conversations.db"));
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(interrupted.PathValue).LoadAsync(expected.Id));
        Assert.IsTrue(error.Failure is ConversationStoreFailure.Corrupt or ConversationStoreFailure.LimitReached);
        CollectionAssert.AreEqual(main, File.ReadAllBytes(interrupted.FilePath("conversations.db")));
        CollectionAssert.AreEqual(journal, File.ReadAllBytes(journalFile));
        AssertNoRecoveryScratch(interrupted);
    }

    [TestMethod]
    public async Task SuperJournalTrailerMarkerIsRejectedWithoutFollowingAnotherFile()
    {
        using var original = new TestDirectory();
        using var interrupted = new TestDirectory();
        var expected = await CreateInterruptedSnapshotAsync(original, interrupted);
        var journalFile = interrupted.FilePath("conversations.db-journal");
        var originalJournal = File.ReadAllBytes(journalFile);
        // A recognizable master/super-journal trailer ends in the same fixed eight-byte magic.
        // This is an adversarial marker fixture; the app never uses attached-database transactions.
        var journal = originalJournal.Concat(new byte[8]).Concat(originalJournal.Take(8)).ToArray();
        await File.WriteAllBytesAsync(journalFile, journal);
        var main = File.ReadAllBytes(interrupted.FilePath("conversations.db"));
        var error = await Assert.ThrowsExactlyAsync<ConversationStoreException>(() =>
            new SqliteConversationStore(interrupted.PathValue).LoadAsync(expected.Id));
        Assert.AreEqual(ConversationStoreFailure.Corrupt, error.Failure);
        CollectionAssert.AreEqual(main, File.ReadAllBytes(interrupted.FilePath("conversations.db")));
        CollectionAssert.AreEqual(journal, File.ReadAllBytes(journalFile));
        AssertNoRecoveryScratch(interrupted);
    }

    private static async Task<ConversationSnapshot> CreateInterruptedSnapshotAsync(TestDirectory original,
        TestDirectory interrupted, string? beforeTransactionSql = null)
    {
        var store = new SqliteConversationStore(original.PathValue);
        var expected = await store.CreateAsync("committed title");
        for (var index = 0; index < 6; index++)
            expected = await store.AppendTurnAsync(expected.Id, expected.LeafTurnId, "committed prompt " + index,
                new string('c', 7000) + index, TurnOutcome.Completed);
        var source = original.FilePath("conversations.db");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = source, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        if (beforeTransactionSql is not null)
        {
            command.CommandText = beforeTransactionSql;
            command.ExecuteNonQuery();
        }
        command.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA cache_size=1; PRAGMA cache_spill=ON; BEGIN EXCLUSIVE;";
        command.ExecuteNonQuery();
        try
        {
            // More dirty pages than the one-page cache forces SQLite to flush a real hot journal.
            // After this synchronous statement returns, the connection is idle while we copy it.
            command.CommandText = "UPDATE conversations SET title='UNCOMMITTED_TITLE'; UPDATE turns SET response=replace(response,'c','z') WHERE outcome<>99;";
            command.ExecuteNonQuery();
            var sourceJournal = source + "-journal";
            Assert.IsTrue(File.Exists(sourceJournal));
            var bytes = ReadIdleWriterSnapshot(sourceJournal);
            Assert.IsTrue(bytes.Length > 512 && bytes.Take(8).Any(value => value != 0), "SQLite must have flushed a nonzero journal header.");
            Assert.IsTrue(new FileInfo(source).Length < 1024 * 1024 && bytes.Length < 1024 * 1024, "The interrupted-write fixture stays below 2 MiB.");
            File.WriteAllBytes(interrupted.FilePath("conversations.db"), ReadIdleWriterSnapshot(source));
            File.WriteAllBytes(interrupted.FilePath("conversations.db-journal"), bytes);
        }
        finally
        {
            command.CommandText = "ROLLBACK";
            command.ExecuteNonQuery();
        }
        return expected;
    }

    private static byte[] ReadIdleWriterSnapshot(string file)
    {
        // The SQLite connection retains its write handle while the synchronous transaction
        // is deliberately idle. Share that existing handle; no writer work occurs during
        // either copy, and the connection remains alive until both copies are complete.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        Assert.IsTrue(stream.Length < 1024 * 1024, "Each interrupted-write snapshot is bounded below 1 MiB.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void AssertReadOnlyRequiresRollback(TestDirectory directory)
    {
        var failure = Assert.ThrowsExactly<SqliteException>(() =>
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = directory.FilePath("conversations.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            command.ExecuteScalar();
        });
        Assert.AreEqual(776, failure.SqliteExtendedErrorCode, "This fixture specifically requires hot-journal rollback, not ordinary corruption.");
    }

    private static void AssertNoRecoveryScratch(TestDirectory directory) =>
        Assert.AreEqual(0, Directory.GetFiles(directory.PathValue, "conversations.recovery*").Length,
            "Disposable recovery copies must not retain a second saved transcript after the operation.");
}
