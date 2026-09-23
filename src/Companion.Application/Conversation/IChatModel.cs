namespace Companion.Application.Conversation;

public enum ChatRole { System, User, Assistant }
public enum ChatFinishReason { Stop, Length }
public enum ChatFailure { InvalidRequest, Unavailable, Timeout, Protocol, OutputLimit, Busy }

public sealed record ChatMessage
{
    public const int MaximumCharacters = 8192;

    public ChatMessage(ChatRole role, string content)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length is 0 or > MaximumCharacters)
            throw new ArgumentException("A chat message must contain between 1 and 8192 characters.", nameof(content));
        Role = role;
        Content = content;
    }

    public ChatRole Role { get; }
    public string Content { get; }
}

public abstract record ChatStreamEvent;
public sealed record ChatTextDelta(string Text) : ChatStreamEvent;
public sealed record ChatCompleted(ChatFinishReason Reason) : ChatStreamEvent;

// These fixed messages deliberately contain neither model output nor request details.
public sealed class ChatModelException(ChatFailure failure) : Exception(failure switch
{
    ChatFailure.InvalidRequest => "The local model request is invalid or exceeds its context limit.",
    ChatFailure.Unavailable => "The local model is unavailable.",
    ChatFailure.Timeout => "The local model response timed out.",
    ChatFailure.Protocol => "The local model returned an invalid or incomplete response.",
    ChatFailure.OutputLimit => "The local model response exceeded its size limit.",
    ChatFailure.Busy => "The local model is already answering a request.",
    _ => "The local model request failed."
})
{
    public ChatFailure Failure { get; } = failure;
}

public interface IChatModel
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
