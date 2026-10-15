namespace Companion.Application.Voice;

public sealed record VoiceInputPreferences(bool MicrophoneMuted = true, string? DeviceId = null);
public sealed record VoiceInputPreferencesResult(VoiceInputPreferences Preferences, bool CanSave);

public interface IVoiceInputPreferencesStore
{
    Task<VoiceInputPreferencesResult> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(VoiceInputPreferences preferences, CancellationToken cancellationToken = default);
}
