using Companion.Core.Configuration;

namespace Companion.Application.Configuration;

public enum SettingsLoadState { New, Loaded, RecoveredBackup, ResetInvalid, UnsupportedVersion }

public sealed record SettingsLoadResult(CompanionSettings Settings, SettingsLoadState State, bool CanSave);

public interface ISettingsStore
{
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CompanionSettings settings, CancellationToken cancellationToken);
}
