using System.Text.Json;

namespace Companion.Application.Conversation;

/// <summary>Optional constrained JSON generation; callers still validate the returned data.</summary>
public interface IStructuredChatModel : IChatModel
{
    IAsyncEnumerable<ChatStreamEvent> StreamJsonAsync(IReadOnlyList<ChatMessage> messages,
        JsonElement schema, CancellationToken cancellationToken = default);
}
