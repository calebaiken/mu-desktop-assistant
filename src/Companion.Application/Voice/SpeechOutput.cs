namespace Companion.Application.Voice;

public sealed record SpeechVoice(string Id, string Name, string Culture);
public sealed record SpeechRequest(string Text, string VoiceId, int Rate = 0);

public enum SpeechOutputState { Speaking, Stopped }
public enum SpeechFailure { InvalidRequest, Unavailable, Busy, Timeout, ProtocolError, CleanupFailed }

/// <summary>A fixed, content-free failure. Native exception details are never exposed.</summary>
public sealed class SpeechException(SpeechFailure failure) : Exception($"Local speech failed: {failure}.")
{
    public SpeechFailure Failure { get; } = failure;
}

public interface ISpeechOutput : IAsyncDisposable
{
    Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default);
    Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress,
        CancellationToken cancellationToken = default);
}

public static class SpeechLimits
{
    public const int MaximumTextCharacters = 2_000;
    public const int MinimumRate = -5;
    public const int MaximumRate = 5;
    public const int MaximumVoiceIdCharacters = 512;
    public const int MaximumVoices = 32;
    public static readonly TimeSpan OperationDeadline = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(3);

    public static void Validate(SpeechRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text) ||
            request.Text.Length > MaximumTextCharacters || !IsValidText(request.Text) ||
            string.IsNullOrWhiteSpace(request.VoiceId) || request.VoiceId.Length > MaximumVoiceIdCharacters ||
            !IsValidText(request.VoiceId) || request.Rate is < MinimumRate or > MaximumRate)
            throw new SpeechException(SpeechFailure.InvalidRequest);
    }

    public static bool IsValidText(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var item = value[index];
            if (item == '\0') return false;
            if (!char.IsSurrogate(item)) continue;
            if (!char.IsHighSurrogate(item) || index + 1 == value.Length || !char.IsLowSurrogate(value[++index]))
                return false;
        }
        return true;
    }
}
