using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

internal sealed record SpeechVoiceRegistration(string? Id, string? Name, string? Culture, string? Vendor, bool Enabled);

/// <summary>Metadata admission happens before any API that may initialize a default engine.</summary>
internal static class SpeechVoicePolicy
{
    internal const int MaximumRegistrations = 256;

    internal static IReadOnlyList<SpeechVoice> GetEligible(IReadOnlyList<SpeechVoiceRegistration> registrations)
    {
        if (registrations.Count > MaximumRegistrations) throw new SpeechException(SpeechFailure.Unavailable);
        var voices = registrations.Where(item => item.Enabled && IsMicrosoft(item))
            .Select(item => new SpeechVoice(item.Id!, item.Name!, item.Culture!)).ToArray();
        if (voices.Length > SpeechLimits.MaximumVoices) throw new SpeechException(SpeechFailure.Unavailable);
        // SAPI selection uses names. Duplicate names or IDs are not an unambiguous selection.
        var uniqueNames = voices.GroupBy(item => item.Name, StringComparer.Ordinal).Where(group => group.Count() == 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var uniqueIds = voices.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() == 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        return voices.Where(item => uniqueNames.Contains(item.Name) && uniqueIds.Contains(item.Id))
            .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
    }

    internal static SpeechVoice AdmitSelection(IReadOnlyList<SpeechVoiceRegistration> registrations, string voiceId)
    {
        var eligible = GetEligible(registrations);
        // SelectVoice can first initialize the default engine. Even Enabled=false calls CurrentVoice
        // in System.Speech 10.0.12. Refuse mixed enabled engines before either API is invoked.
        if (registrations.Any(item => item.Enabled && !IsMicrosoft(item)))
            throw new SpeechException(SpeechFailure.Unavailable);
        return eligible.SingleOrDefault(item => string.Equals(item.Id, voiceId, StringComparison.Ordinal))
            ?? throw new SpeechException(SpeechFailure.Unavailable);
    }

    private static bool IsMicrosoft(SpeechVoiceRegistration item)
    {
        if (item.Id is not { } id || item.Name is not { } name || item.Culture is not { } culture ||
            !string.Equals(item.Vendor, "Microsoft", StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith("Microsoft ", StringComparison.Ordinal) ||
            !(id.StartsWith("TTS_MS_", StringComparison.Ordinal) || id.StartsWith("MSTTS_V", StringComparison.Ordinal)))
            return false;
        try { SpeechProtocol.ValidateVoice(new SpeechVoice(id, name, culture)); return true; }
        catch (SpeechException) { return false; }
    }
}
