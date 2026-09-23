namespace Companion.Application.Conversation;

/// <summary>Temporary conversations never receive a profile path and never access durable storage.</summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, ConversationSnapshot> conversations = [];

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateSearch(search);
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ConversationSummary> result = conversations.Values
                .Where(item => string.IsNullOrEmpty(search) || item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Turns.Any(turn => turn.Prompt.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                           turn.Response.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id)
                .Select(item => new ConversationSummary(item.Id, item.Title, item.UpdatedAt, item.Turns.Count)).ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<ConversationSnapshot> CreateAsync(string title, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTitle(title);
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (conversations.Count >= ConversationHistory.MaximumConversations)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var snapshot = new ConversationSnapshot(Guid.NewGuid(), title, DateTimeOffset.UtcNow, null, []);
            conversations.Add(snapshot.Id, snapshot);
            return Task.FromResult(snapshot);
        }
    }

    public Task<ConversationSnapshot?> LoadAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = conversations.GetValueOrDefault(conversationId);
            if (previous is not null && previous.Turns.Any(turn => turn.Outcome == TurnOutcome.InProgress))
            {
                previous = new(previous.Id, previous.Title, DateTimeOffset.UtcNow, previous.LeafTurnId,
                    previous.Turns.Select(turn => turn.Outcome == TurnOutcome.InProgress ? turn with { Outcome = TurnOutcome.Interrupted } : turn));
                conversations[conversationId] = previous;
            }
            return Task.FromResult(previous);
        }
    }

    public Task<ConversationSnapshot> AppendTurnAsync(Guid conversationId, Guid? parentTurnId, string prompt, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTurn(prompt, response, outcome);
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = Find(conversationId);
            ValidateParent(previous, parentTurnId);
            if (previous.Turns.Any(turn => turn.Outcome == TurnOutcome.InProgress))
                throw new ConversationStoreException(ConversationStoreFailure.Conflict);
            if (previous.Turns.Count >= ConversationHistory.MaximumTurns ||
                conversations.Values.Sum(item => item.Turns.Sum(turn => turn.Prompt.Length + turn.Response.Length)) +
                prompt.Length + response.Length > ConversationHistory.MaximumStoredCharacters)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var now = DateTimeOffset.UtcNow;
            var turn = new ConversationTurn(Guid.NewGuid(), parentTurnId, prompt, response, outcome, now);
            var next = new ConversationSnapshot(previous.Id, previous.Title, now, turn.Id, previous.Turns.Append(turn));
            conversations[conversationId] = next;
            return Task.FromResult(next);
        }
    }

    public Task<ConversationSnapshot> CompleteTurnAsync(Guid conversationId, Guid turnId, string response,
        TurnOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (outcome == TurnOutcome.InProgress) throw new ArgumentException("A turn must finish with a terminal outcome.", nameof(outcome));
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = Find(conversationId);
            var turn = previous.Turns.SingleOrDefault(item => item.Id == turnId) ??
                throw new ConversationStoreException(ConversationStoreFailure.NotFound);
            if (turn.Outcome != TurnOutcome.InProgress) throw new ConversationStoreException(ConversationStoreFailure.Conflict);
            ConversationHistory.ValidateTurn(turn.Prompt, response, outcome);
            if (conversations.Values.Sum(item => item.Turns.Sum(itemTurn => itemTurn.Prompt.Length + itemTurn.Response.Length)) +
                response.Length - turn.Response.Length > ConversationHistory.MaximumStoredCharacters)
                throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var finished = turn with { Response = response, Outcome = outcome };
            var next = new ConversationSnapshot(previous.Id, previous.Title, DateTimeOffset.UtcNow, previous.LeafTurnId,
                previous.Turns.Select(item => item.Id == turnId ? finished : item));
            conversations[conversationId] = next;
            return Task.FromResult(next);
        }
    }

    public Task<ConversationSnapshot> SelectBranchAsync(Guid conversationId, Guid? leafTurnId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = Find(conversationId);
            ValidateParent(previous, leafTurnId);
            var next = new ConversationSnapshot(previous.Id, previous.Title, DateTimeOffset.UtcNow, leafTurnId, previous.Turns);
            conversations[conversationId] = next;
            return Task.FromResult(next);
        }
    }

    public Task RenameAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ConversationHistory.ValidateTitle(title);
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = Find(conversationId);
            conversations[conversationId] = new(previous.Id, title, DateTimeOffset.UtcNow, previous.LeafTurnId, previous.Turns);
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            conversations.Remove(conversationId);
            return Task.CompletedTask;
        }
    }

    private ConversationSnapshot Find(Guid id) => conversations.GetValueOrDefault(id) ??
        throw new ConversationStoreException(ConversationStoreFailure.NotFound);

    private static void ValidateParent(ConversationSnapshot snapshot, Guid? parent)
    {
        if (parent is not null && snapshot.Turns.All(turn => turn.Id != parent))
            throw new ConversationStoreException(ConversationStoreFailure.NotFound);
    }
}
