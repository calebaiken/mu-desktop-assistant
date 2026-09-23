using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Companion.Application.Conversation;
using Microsoft.Data.Sqlite;

namespace Companion.Infrastructure.Conversation;

/// <summary>Bounded local history. SQLite work runs off the caller thread behind a serialized gate.</summary>
public sealed class SqliteConversationStore : IConversationStore
{
    private const int SchemaVersion = 1;
    private const int ApplicationId = 0x434F4D50;
    private const long MaximumDatabaseBytes = 64L * 1024 * 1024;
    private readonly string path;
    private readonly long maximumDatabaseBytes;
    private readonly SemaphoreSlim gate = new(1);

    public SqliteConversationStore(string profileDirectory) : this(profileDirectory, MaximumDatabaseBytes) { }

    internal SqliteConversationStore(string profileDirectory, long maximumDatabaseBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
        var normalized = Path.GetFullPath(profileDirectory);
        if (!Path.IsPathFullyQualified(profileDirectory) || normalized.StartsWith(@"\\", StringComparison.Ordinal) ||
            normalized.Any(char.IsControl))
            throw new ArgumentException("Conversation storage requires an absolute local profile directory.", nameof(profileDirectory));
        if (maximumDatabaseBytes is < 32768 or > MaximumDatabaseBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumDatabaseBytes));
        this.maximumDatabaseBytes = maximumDatabaseBytes;
        path = Path.Combine(normalized, "conversations.db");
    }

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateSearch(search);
        return RunAsync<IReadOnlyList<ConversationSummary>>(connection =>
        {
            using var command = Command(connection, """
                SELECT c.id,c.title,c.updated_at,(SELECT count(*) FROM turns t WHERE t.conversation_id=c.id),length(c.title)
                FROM conversations c
                WHERE $search='' OR literal_contains(c.title,$search) OR EXISTS
                  (SELECT 1 FROM turns t WHERE t.conversation_id=c.id AND
                   (literal_contains(t.prompt,$search) OR literal_contains(t.response,$search)))
                ORDER BY c.updated_at DESC,c.id LIMIT 101
                """, ("$search", search ?? string.Empty));
            using var reader = command.ExecuteReader();
            var result = new List<ConversationSummary>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.GetInt64(4) > ConversationHistory.MaximumTitleCharacters) throw Corrupt();
                var title = reader.GetString(1);
                ConversationHistory.ValidateTitle(title);
                var count = reader.GetInt32(3);
                if (count > ConversationHistory.MaximumTurns) throw Corrupt();
                result.Add(new(ReadGuid(reader.GetString(0)), title, ReadTime(reader.GetInt64(2)), count));
            }
            if (result.Count > ConversationHistory.MaximumConversations) throw Corrupt();
            return result.AsReadOnly();
        }, cancellationToken);
    }

    public Task<ConversationSnapshot> CreateAsync(string title, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTitle(title);
        return RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            if (ScalarLong(connection, "SELECT count(*) FROM conversations") >= ConversationHistory.MaximumConversations)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var snapshot = new ConversationSnapshot(Guid.NewGuid(), title, DateTimeOffset.UtcNow, null, []);
            Execute(connection, "INSERT INTO conversations(id,title,updated_at) VALUES($id,$title,$updated)",
                ("$id", Id(snapshot.Id)), ("$title", title), ("$updated", Time(snapshot.UpdatedAt)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return snapshot;
        }, cancellationToken);
    }

    public Task<ConversationSnapshot?> LoadAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var snapshot = ReadSnapshot(connection, conversationId, cancellationToken);
            if (snapshot is not null && snapshot.Turns.Any(turn => turn.Outcome == TurnOutcome.InProgress))
            {
                var now = DateTimeOffset.UtcNow;
                Execute(connection, "UPDATE turns SET outcome=$interrupted WHERE conversation_id=$id AND outcome=$pending",
                    ("$interrupted", (int)TurnOutcome.Interrupted), ("$id", Id(conversationId)), ("$pending", (int)TurnOutcome.InProgress));
                Execute(connection, "UPDATE conversations SET updated_at=$updated WHERE id=$id", ("$updated", Time(now)), ("$id", Id(conversationId)));
                snapshot = new(snapshot.Id, snapshot.Title, now, snapshot.LeafTurnId,
                    snapshot.Turns.Select(turn => turn.Outcome == TurnOutcome.InProgress ? turn with { Outcome = TurnOutcome.Interrupted } : turn));
            }
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return snapshot;
        }, cancellationToken);

    public Task<ConversationSnapshot> AppendTurnAsync(Guid conversationId, Guid? parentTurnId, string prompt, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTurn(prompt, response, outcome);
        return RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var previous = ReadSnapshot(connection, conversationId, cancellationToken) ?? throw NotFound();
            ValidateParent(previous, parentTurnId);
            if (previous.Turns.Any(turn => turn.Outcome == TurnOutcome.InProgress))
                throw new ConversationStoreException(ConversationStoreFailure.Conflict);
            if (previous.Turns.Count >= ConversationHistory.MaximumTurns ||
                ScalarLong(connection, "SELECT coalesce(sum(character_count),0) FROM turns") + prompt.Length + response.Length >
                ConversationHistory.MaximumStoredCharacters)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var now = DateTimeOffset.UtcNow;
            var turn = new ConversationTurn(Guid.NewGuid(), parentTurnId, prompt, response, outcome, now);
            Execute(connection, """
                INSERT INTO turns(id,conversation_id,parent_turn_id,prompt,response,outcome,created_at,character_count)
                VALUES($id,$conversation,$parent,$prompt,$response,$outcome,$created,$characters)
                """, ("$id", Id(turn.Id)), ("$conversation", Id(conversationId)), ("$parent", OptionalId(parentTurnId)),
                ("$prompt", prompt), ("$response", response), ("$outcome", (int)outcome), ("$created", Time(now)),
                ("$characters", prompt.Length + response.Length));
            Execute(connection, "UPDATE conversations SET leaf_turn_id=$leaf,updated_at=$updated WHERE id=$id",
                ("$leaf", Id(turn.Id)), ("$updated", Time(now)), ("$id", Id(conversationId)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new ConversationSnapshot(previous.Id, previous.Title, now, turn.Id, previous.Turns.Append(turn));
        }, cancellationToken);
    }

    public Task<ConversationSnapshot> CompleteTurnAsync(Guid conversationId, Guid turnId, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (outcome == TurnOutcome.InProgress || !Enum.IsDefined(outcome))
            throw new ArgumentException("A turn must finish with a terminal outcome.", nameof(outcome));
        ConversationHistory.ValidateTurn("Pending turn", response, outcome);
        return RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var previous = ReadSnapshot(connection, conversationId, cancellationToken) ?? throw NotFound();
            var turn = previous.Turns.SingleOrDefault(item => item.Id == turnId) ?? throw NotFound();
            if (turn.Outcome != TurnOutcome.InProgress) throw new ConversationStoreException(ConversationStoreFailure.Conflict);
            ConversationHistory.ValidateTurn(turn.Prompt, response, outcome);
            if (ScalarLong(connection, "SELECT coalesce(sum(character_count),0) FROM turns") + response.Length - turn.Response.Length >
                ConversationHistory.MaximumStoredCharacters)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var now = DateTimeOffset.UtcNow;
            Execute(connection, "UPDATE turns SET response=$response,outcome=$outcome,character_count=$characters WHERE id=$id AND conversation_id=$conversation",
                ("$response", response), ("$outcome", (int)outcome), ("$characters", turn.Prompt.Length + response.Length),
                ("$id", Id(turnId)), ("$conversation", Id(conversationId)));
            Execute(connection, "UPDATE conversations SET updated_at=$updated WHERE id=$id", ("$updated", Time(now)), ("$id", Id(conversationId)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new ConversationSnapshot(previous.Id, previous.Title, now, previous.LeafTurnId,
                previous.Turns.Select(item => item.Id == turnId ? item with { Response = response, Outcome = outcome } : item));
        }, cancellationToken);
    }

    public Task<ConversationSnapshot> SelectBranchAsync(Guid conversationId, Guid? leafTurnId, CancellationToken cancellationToken = default) =>
        RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var previous = ReadSnapshot(connection, conversationId, cancellationToken) ?? throw NotFound();
            ValidateParent(previous, leafTurnId);
            var now = DateTimeOffset.UtcNow;
            Execute(connection, "UPDATE conversations SET leaf_turn_id=$leaf,updated_at=$updated WHERE id=$id",
                ("$leaf", OptionalId(leafTurnId)), ("$updated", Time(now)), ("$id", Id(conversationId)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new ConversationSnapshot(previous.Id, previous.Title, now, leafTurnId, previous.Turns);
        }, cancellationToken);

    public Task RenameAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTitle(title);
        return RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            if (ReadSnapshot(connection, conversationId, cancellationToken) is null) throw NotFound();
            Execute(connection, "UPDATE conversations SET title=$title,updated_at=$updated WHERE id=$id",
                ("$title", title), ("$updated", Time(DateTimeOffset.UtcNow)), ("$id", Id(conversationId)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return true;
        }, cancellationToken);
    }

    public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        RunAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            Execute(connection, "DELETE FROM conversations WHERE id=$id", ("$id", Id(conversationId)));
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return true;
        }, cancellationToken);

    private async Task<T> RunAsync<T>(Func<SqliteConnection, T> operation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var connection = OpenValidated(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    // The operation checks cancellation immediately before commit. Never report cancellation after a commit.
                    return operation(connection);
                }
                catch (SqliteException error)
                {
                    throw new ConversationStoreException(error.SqliteErrorCode is 1 or 11 or 26 or 19
                        ? ConversationStoreFailure.Corrupt : error.SqliteErrorCode == 13
                        ? ConversationStoreFailure.LimitReached : ConversationStoreFailure.Unavailable);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { throw new ConversationStoreException(ConversationStoreFailure.Unavailable); }
                catch (Exception error) when (error is FormatException or OverflowException or ArgumentException or InvalidCastException)
                { throw Corrupt(); }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private SqliteConnection OpenValidated(CancellationToken cancellationToken)
    {
        EnsureLocalPath();
        var existed = File.Exists(path);
        RecoveryIdentity? recovery = null;
        if (existed)
        {
            EnsureFileBounds();
            try
            {
                using var inspection = Open(SqliteOpenMode.ReadOnly);
                ValidateSchema(inspection);
            }
            catch (SqliteException error) when (error.SqliteExtendedErrorCode == 776) // SQLITE_READONLY_ROLLBACK
            {
                // SQLite must write to roll back a hot journal before it can even read the schema.
                // Admit that write only after SQLite has recovered and validated an isolated bounded copy.
                recovery = ValidateRecoveryCopy(cancellationToken);
            }
        }
        else Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        cancellationToken.ThrowIfCancellationRequested();
        if (recovery is not null) VerifyRecoveryIdentity(recovery, cancellationToken);
        var connection = Open(existed ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate);
        try
        {
            if (!existed && ScalarLong(connection, "PRAGMA user_version") == 0 &&
                ScalarLong(connection, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'") == 0)
                CreateSchema(connection);
            else ValidateSchema(connection);
            if (recovery is not null) ValidateEveryConversation(connection, cancellationToken);
            // Physical growth is bounded even when deleted pages are retained for later reuse.
            var pageSize = ScalarLong(connection, "PRAGMA page_size");
            Execute(connection, "PRAGMA max_page_count=" + (maximumDatabaseBytes / pageSize).ToString(CultureInfo.InvariantCulture));
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private SqliteConnection Open(SqliteOpenMode mode, string? databasePath = null)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? path, Mode = mode, Cache = SqliteCacheMode.Private, Pooling = false,
            ForeignKeys = true, DefaultTimeout = 2
        }.ToString());
        try
        {
            connection.Open();
            connection.CreateFunction<string, string, bool>("literal_contains",
                (value, query) => value.Contains(query, StringComparison.OrdinalIgnoreCase), isDeterministic: true);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private sealed record RecoveryIdentity(byte[] DatabaseHash, byte[] JournalHash);

    private RecoveryIdentity ValidateRecoveryCopy(CancellationToken cancellationToken)
    {
        EnsureLocalPath();
        EnsureFileBounds();
        if (File.Exists(path + "-wal") || File.Exists(path + "-shm"))
            throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        var scratch = Path.Combine(Path.GetDirectoryName(path)!, "conversations.recovery.db");
        var owner = Path.Combine(Path.GetDirectoryName(path)!, "conversations.recovery.owner");
        PrepareRecoveryScratch(scratch, owner);
        try
        {
            // These Windows read leases deny writes and deletion while the pair is copied and validated.
            // A live SQLite writer prevents admission; it is never mistaken for a corrupt database.
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var journal = new FileStream(path + "-journal", FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateRecoveryJournal(journal);
            var databaseHash = CopyBounded(source, scratch, cancellationToken);
            var journalHash = CopyBounded(journal, scratch + "-journal", cancellationToken);
            using (var recovered = Open(SqliteOpenMode.ReadWrite, scratch))
            {
                ValidateSchema(recovered);
                ValidateEveryConversation(recovered, cancellationToken);
            }
            if (new FileInfo(scratch).Length > maximumDatabaseBytes)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            cancellationToken.ThrowIfCancellationRequested();
            return new(databaseHash, journalHash);
        }
        finally { CleanupRecoveryScratch(scratch, owner); }
    }

    private void VerifyRecoveryIdentity(RecoveryIdentity expected, CancellationToken cancellationToken)
    {
        EnsureLocalPath();
        EnsureFileBounds();
        if (File.Exists(path + "-wal") || File.Exists(path + "-shm"))
            throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var journal = new FileStream(path + "-journal", FileMode.Open, FileAccess.Read, FileShare.Read);
        ValidateRecoveryJournal(journal);
        if (!CryptographicOperations.FixedTimeEquals(expected.DatabaseHash, HashBounded(source, cancellationToken)) ||
            !CryptographicOperations.FixedTimeEquals(expected.JournalHash, HashBounded(journal, cancellationToken)))
            throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        // The app's profile-instance lock excludes another app writer. This recheck detects changed sources;
        // it does not claim an atomic exclusion of arbitrary same-user writers after these leases close.
    }

    private void ValidateRecoveryJournal(FileStream journal)
    {
        if (journal.Length is < 512 || journal.Length > maximumDatabaseBytes) throw Corrupt();
        ReadOnlySpan<byte> magic = [0xd9, 0xd5, 0x05, 0xf9, 0x20, 0xa1, 0x63, 0xd7];
        Span<byte> header = stackalloc byte[28];
        journal.Position = 0;
        journal.ReadExactly(header);
        if (!header[..8].SequenceEqual(magic)) throw Corrupt();
        var pages = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4));
        var sectorSize = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4));
        var pageSize = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(24, 4));
        if (pageSize is < 512 or > 65536 || (pageSize & (pageSize - 1)) != 0 ||
            sectorSize is < 512 or > 65536 || (sectorSize & (sectorSize - 1)) != 0 || pages == 0 ||
            (long)pages * pageSize > maximumDatabaseBytes)
            throw Corrupt();
        // SQLite can follow a super-journal filename encoded at the end of a rollback journal.
        // This app never ATTACHes databases, so refuse that trailer rather than follow any external path.
        Span<byte> tail = stackalloc byte[8];
        journal.Position = journal.Length - tail.Length;
        journal.ReadExactly(tail);
        if (tail.SequenceEqual(magic)) throw Corrupt();
        journal.Position = 0;
    }

    private byte[] CopyBounded(FileStream source, string destination, CancellationToken cancellationToken)
    {
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var hash = ReadBounded(source, output, cancellationToken);
        output.Flush(true);
        return hash;
    }

    private byte[] HashBounded(FileStream source, CancellationToken cancellationToken) => ReadBounded(source, null, cancellationToken);

    private byte[] ReadBounded(FileStream source, FileStream? output, CancellationToken cancellationToken)
    {
        if (source.Length > maximumDatabaseBytes) throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
        source.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long bytes = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer);
            if (read == 0) break;
            bytes += read;
            if (bytes > maximumDatabaseBytes) throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            hash.AppendData(buffer, 0, read);
            output?.Write(buffer, 0, read);
        }
        return hash.GetHashAndReset();
    }

    private static void ValidateEveryConversation(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        using (var command = Command(connection, "SELECT id FROM conversations LIMIT 101"))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) ids.Add(ReadGuid(reader.GetString(0)));
        if (ids.Count > ConversationHistory.MaximumConversations) throw Corrupt();
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadSnapshot(connection, id, cancellationToken) is null) throw Corrupt();
        }
    }

    private static readonly byte[] RecoveryOwner = "DesktopCompanion bounded SQLite recovery v1\n"u8.ToArray();

    private static void PrepareRecoveryScratch(string scratch, string owner)
    {
        foreach (var file in RecoveryFiles(scratch).Append(owner)) EnsureRegularFile(file);
        if (File.Exists(owner))
        {
            using var marker = new FileStream(owner, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (marker.Length != RecoveryOwner.Length) throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
            var bytes = new byte[RecoveryOwner.Length];
            marker.ReadExactly(bytes);
            if (!bytes.AsSpan().SequenceEqual(RecoveryOwner)) throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        }
        else
        {
            // Unknown preexisting scratch content is never deleted or treated as ours.
            if (RecoveryFiles(scratch).Any(File.Exists)) throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
            using var marker = new FileStream(owner, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            marker.Write(RecoveryOwner);
            marker.Flush(true);
        }
        foreach (var file in RecoveryFiles(scratch)) File.Delete(file);
    }

    private static void CleanupRecoveryScratch(string scratch, string owner)
    {
        foreach (var file in RecoveryFiles(scratch).Append(owner)) EnsureRegularFile(file);
        foreach (var file in RecoveryFiles(scratch)) File.Delete(file);
        File.Delete(owner);
    }

    private static string[] RecoveryFiles(string scratch) => [scratch, scratch + "-journal", scratch + "-wal", scratch + "-shm"];

    private static void EnsureRegularFile(string file)
    {
        try
        {
            if ((File.GetAttributes(file) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, """
            CREATE TABLE conversations(
              id TEXT PRIMARY KEY NOT NULL,
              title TEXT NOT NULL CHECK(length(title) BETWEEN 1 AND 80),
              updated_at INTEGER NOT NULL,
              leaf_turn_id TEXT,
              FOREIGN KEY(leaf_turn_id,id) REFERENCES turns(id,conversation_id) DEFERRABLE INITIALLY DEFERRED);
            CREATE TABLE turns(
              id TEXT PRIMARY KEY NOT NULL,
              conversation_id TEXT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
              parent_turn_id TEXT,
              prompt TEXT NOT NULL CHECK(length(prompt) BETWEEN 1 AND 2000),
              response TEXT NOT NULL CHECK(length(response)<=8192),
              outcome INTEGER NOT NULL CHECK(outcome BETWEEN 0 AND 3),
              created_at INTEGER NOT NULL,
              character_count INTEGER NOT NULL CHECK(character_count BETWEEN 1 AND 10192),
              UNIQUE(id,conversation_id),
              FOREIGN KEY(parent_turn_id,conversation_id) REFERENCES turns(id,conversation_id));
            CREATE INDEX turns_conversation ON turns(conversation_id);
            CREATE INDEX conversations_updated ON conversations(updated_at DESC);
            """);
        Execute(connection, "PRAGMA application_id=" + ApplicationId.ToString(CultureInfo.InvariantCulture));
        Execute(connection, "PRAGMA user_version=" + SchemaVersion.ToString(CultureInfo.InvariantCulture));
        transaction.Commit();
    }

    private static void ValidateSchema(SqliteConnection connection)
    {
        var version = ScalarLong(connection, "PRAGMA user_version");
        if (version != SchemaVersion) throw new ConversationStoreException(ConversationStoreFailure.UnsupportedSchema);
        if (ScalarLong(connection, "PRAGMA application_id") != ApplicationId) throw Corrupt();
        using (var check = Command(connection, "PRAGMA quick_check(1)"))
            if (check.ExecuteScalar() is not string text || text != "ok") throw Corrupt();
        using (var foreignKeys = Command(connection, "PRAGMA foreign_key_check"))
        using (var reader = foreignKeys.ExecuteReader())
            if (reader.Read()) throw Corrupt();
        if (ScalarLong(connection, "SELECT count(*) FROM conversations") > ConversationHistory.MaximumConversations ||
            ScalarLong(connection, "SELECT count(*) FROM turns") > ConversationHistory.MaximumConversations * ConversationHistory.MaximumTurns ||
            ScalarLong(connection, "SELECT coalesce(sum(character_count),0) FROM turns") > ConversationHistory.MaximumStoredCharacters ||
            ScalarLong(connection, "SELECT count(*) FROM (SELECT conversation_id FROM turns GROUP BY conversation_id HAVING count(*)>256)") != 0 ||
            ScalarLong(connection, "SELECT EXISTS(SELECT 1 FROM conversations WHERE length(CAST(title AS BLOB))>320)") != 0 ||
            ScalarLong(connection, "SELECT EXISTS(SELECT 1 FROM turns WHERE length(CAST(prompt AS BLOB))>8000 OR length(CAST(response AS BLOB))>32768)") != 0)
            throw Corrupt();
    }

    private static ConversationSnapshot? ReadSnapshot(SqliteConnection connection, Guid id, CancellationToken cancellationToken)
    {
        string title;
        DateTimeOffset updated;
        Guid? leaf;
        using (var command = Command(connection, "SELECT title,updated_at,leaf_turn_id,length(title) FROM conversations WHERE id=$id", ("$id", Id(id))))
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            if (reader.GetInt64(3) > ConversationHistory.MaximumTitleCharacters) throw Corrupt();
            title = reader.GetString(0);
            ConversationHistory.ValidateTitle(title);
            updated = ReadTime(reader.GetInt64(1));
            leaf = reader.IsDBNull(2) ? null : ReadGuid(reader.GetString(2));
        }
        var turns = new List<ConversationTurn>();
        using (var command = Command(connection, """
            SELECT id,parent_turn_id,prompt,response,outcome,created_at,character_count,length(prompt),length(response)
            FROM turns WHERE conversation_id=$id ORDER BY rowid LIMIT 257
            """, ("$id", Id(id))))
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.GetInt64(7) > ConversationHistory.MaximumInputCharacters ||
                    reader.GetInt64(8) > ConversationHistory.MaximumReplyCharacters) throw Corrupt();
                var prompt = reader.GetString(2);
                var response = reader.GetString(3);
                var outcome = (TurnOutcome)reader.GetInt32(4);
                ConversationHistory.ValidateTurn(prompt, response, outcome);
                if (reader.GetInt32(6) != prompt.Length + response.Length) throw Corrupt();
                turns.Add(new(ReadGuid(reader.GetString(0)), reader.IsDBNull(1) ? null : ReadGuid(reader.GetString(1)),
                    prompt, response, outcome, ReadTime(reader.GetInt64(5))));
            }
        if (turns.Count > ConversationHistory.MaximumTurns) throw Corrupt();
        var snapshot = new ConversationSnapshot(id, title, updated, leaf, turns);
        // Validate all branches, including unselected ones, before exposing any saved content.
        var seen = new HashSet<Guid>();
        foreach (var turn in turns)
        {
            if (!seen.Add(turn.Id) || (turn.ParentTurnId is not null && !seen.Contains(turn.ParentTurnId.Value)) ||
                turn.Id == turn.ParentTurnId) throw Corrupt();
        }
        _ = ConversationHistory.GetActiveBranch(snapshot);
        return snapshot;
    }

    private void EnsureLocalPath()
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
        foreach (var file in new[] { path, path + "-wal", path + "-journal", path + "-shm" })
        {
            try
            {
                if ((File.GetAttributes(file) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new ConversationStoreException(ConversationStoreFailure.Unavailable);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private void EnsureFileBounds()
    {
        foreach (var file in new[] { path, path + "-wal", path + "-journal" })
            if (File.Exists(file) && new FileInfo(file).Length > maximumDatabaseBytes)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
    }

    private static void ValidateParent(ConversationSnapshot snapshot, Guid? parent)
    {
        if (parent is not null && snapshot.Turns.All(turn => turn.Id != parent)) throw NotFound();
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    { using var command = Command(connection, sql, parameters); command.ExecuteNonQuery(); }

    private static long ScalarLong(SqliteConnection connection, string sql)
    { using var command = Command(connection, sql); return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture); }

    private static string Id(Guid id) => id.ToString("D");
    private static object OptionalId(Guid? id) => id is null ? DBNull.Value : Id(id.Value);
    private static long Time(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
    private static DateTimeOffset ReadTime(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);
    private static Guid ReadGuid(string value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty ? id : throw Corrupt();
    private static ConversationStoreException Corrupt() => new(ConversationStoreFailure.Corrupt);
    private static ConversationStoreException NotFound() => new(ConversationStoreFailure.NotFound);
}
