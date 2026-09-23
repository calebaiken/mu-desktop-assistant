using System.Text;

namespace Companion.Application.Conversation;

public enum TurnOutcome { Completed, Interrupted, Failed, InProgress }
public enum ConversationStoreFailure { Unavailable, UnsupportedSchema, Corrupt, LimitReached, NotFound, Conflict }

public sealed record ConversationTurn(Guid Id, Guid? ParentTurnId, string Prompt, string Response,
    TurnOutcome Outcome, DateTimeOffset CreatedAt);

public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset UpdatedAt, int TurnCount);

/// <summary>A bounded immutable tree. LeafTurnId chooses the branch shown and sent to the model.</summary>
public sealed record ConversationSnapshot
{
    public ConversationSnapshot(Guid id, string title, DateTimeOffset updatedAt, Guid? leafTurnId,
        IEnumerable<ConversationTurn> turns)
    {
        Id = id;
        Title = title;
        UpdatedAt = updatedAt;
        LeafTurnId = leafTurnId;
        Turns = Array.AsReadOnly(turns.ToArray());
    }

    public Guid Id { get; }
    public string Title { get; }
    public DateTimeOffset UpdatedAt { get; }
    public Guid? LeafTurnId { get; }
    public IReadOnlyList<ConversationTurn> Turns { get; }
}

public sealed class ConversationStoreException(ConversationStoreFailure failure) : Exception(failure switch
{
    ConversationStoreFailure.UnsupportedSchema => "This conversation database uses an unsupported version. The file was preserved.",
    ConversationStoreFailure.Corrupt => "The conversation database could not be validated. The file was preserved.",
    ConversationStoreFailure.LimitReached => "The conversation storage limit was reached. Delete an old conversation or start a shorter one.",
    ConversationStoreFailure.NotFound => "The conversation or branch is no longer available.",
    ConversationStoreFailure.Conflict => "The conversation turn has already finished or another turn is in progress.",
    _ => "Conversation storage is unavailable."
})
{
    public ConversationStoreFailure Failure { get; } = failure;
}

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationSummary>> ListAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<ConversationSnapshot> CreateAsync(string title, CancellationToken cancellationToken = default);
    /// <summary>Open an idle conversation, recovering abandoned in-progress turns as interrupted.
    /// Do not use as a passive refresh while a generation owns this conversation.</summary>
    Task<ConversationSnapshot?> LoadAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationSnapshot> AppendTurnAsync(Guid conversationId, Guid? parentTurnId, string prompt, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default);
    Task<ConversationSnapshot> CompleteTurnAsync(Guid conversationId, Guid turnId, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default);
    Task<ConversationSnapshot> SelectBranchAsync(Guid conversationId, Guid? leafTurnId, CancellationToken cancellationToken = default);
    Task RenameAsync(Guid conversationId, string title, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public static class ConversationHistory
{
    public const int MaximumConversations = 100;
    public const int MaximumTurns = 256;
    public const int MaximumStoredCharacters = 4_000_000;
    public const int MaximumInputCharacters = 2000;
    public const int MaximumReplyCharacters = 8192;
    public const int MaximumTitleCharacters = 80;
    public const int MaximumSearchCharacters = 120;
    public const int MaximumContextMessages = 24;
    public const int MaximumContextBytes = 24 * 1024;

    public static string TitleFromPrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var title = string.Join(' ', prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (title.Length == 0) return "New conversation";
        if (title.Length <= MaximumTitleCharacters) return title;
        var length = MaximumTitleCharacters - 1;
        if (char.IsHighSurrogate(title[length - 1])) length--;
        return title[..length] + "…";
    }

    public static void ValidateTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaximumTitleCharacters || title.Any(char.IsControl))
            throw new ArgumentException("A title must contain 1 to 80 characters and no control characters.", nameof(title));
    }

    public static void ValidateSearch(string? search)
    {
        if (search?.Length > MaximumSearchCharacters)
            throw new ArgumentException("Search must contain no more than 120 characters.", nameof(search));
    }

    public static void ValidateTurn(string prompt, string response, TurnOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(response);
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > MaximumInputCharacters)
            throw new ArgumentException("A prompt must contain 1 to 2000 characters.", nameof(prompt));
        if (response.Length > MaximumReplyCharacters || (outcome == TurnOutcome.Completed && string.IsNullOrWhiteSpace(response)))
            throw new ArgumentException("A completed reply must be nonempty and replies cannot exceed 8192 characters.", nameof(response));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (outcome == TurnOutcome.InProgress && response.Length != 0)
            throw new ArgumentException("An in-progress turn starts without a reply.", nameof(response));
    }

    public static IReadOnlyList<ConversationTurn> GetActiveBranch(ConversationSnapshot conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (conversation.Turns.Count > MaximumTurns) throw new ConversationStoreException(ConversationStoreFailure.Corrupt);
        var byId = new Dictionary<Guid, ConversationTurn>();
        foreach (var turn in conversation.Turns)
            if (!byId.TryAdd(turn.Id, turn)) throw new ConversationStoreException(ConversationStoreFailure.Corrupt);
        var result = new List<ConversationTurn>();
        var seen = new HashSet<Guid>();
        for (var current = conversation.LeafTurnId; current is not null;)
        {
            if (!seen.Add(current.Value) || !byId.TryGetValue(current.Value, out var turn))
                throw new ConversationStoreException(ConversationStoreFailure.Corrupt);
            result.Add(turn);
            current = turn.ParentTurnId;
        }
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <summary>Interrupted output remains visible but never enters model context. Trim oldest complete pairs.</summary>
    public static IReadOnlyList<ChatMessage> BuildContext(ConversationSnapshot conversation, string prompt)
    {
        ValidateTurn(prompt, string.Empty, TurnOutcome.Interrupted);
        var messages = new List<ChatMessage>();
        foreach (var turn in GetActiveBranch(conversation).Where(turn => turn.Outcome == TurnOutcome.Completed))
        {
            messages.Add(new(ChatRole.User, turn.Prompt));
            messages.Add(new(ChatRole.Assistant, turn.Response));
        }
        messages.Add(new(ChatRole.User, prompt));
        while (messages.Count > 1 && (messages.Count > MaximumContextMessages ||
               messages.Sum(message => Encoding.UTF8.GetByteCount(message.Content)) > MaximumContextBytes))
            messages.RemoveRange(0, 2);
        return messages.AsReadOnly();
    }
}
