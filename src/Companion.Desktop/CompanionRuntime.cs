using System.IO;
using Companion.Application.Configuration;
using Companion.Application.Diagnostics;
using Companion.Core.Configuration;
using Companion.Infrastructure.Configuration;
using Companion.Infrastructure.Diagnostics;
using Companion.Infrastructure.Lifetime;

namespace Companion.Desktop;

internal sealed class CompanionRuntime : IAsyncDisposable
{
    public JsonSettingsStore SettingsStore { get; }
    public JsonEventLog Log { get; }
    public SingleInstanceCoordinator Instance { get; }
    public SettingsLoadResult LoadResult { get; private set; } = new(new(), SettingsLoadState.New, false);
    public CompanionSettings Settings { get; private set; } = new();
    public CancellationTokenSource Lifetime { get; } = new();
    private readonly SemaphoreSlim saves = new(1);

    public CompanionRuntime(string directory, SingleInstanceCoordinator instance)
    {
        Instance = instance;
        SettingsStore = new(directory);
        Log = new(Path.Combine(directory, "logs"));
    }

    public async Task InitializeAsync()
    {
        LoadResult = await SettingsStore.LoadAsync(Lifetime.Token);
        Settings = LoadResult.Settings;
        Log.DiagnosticsEnabled = Settings.DiagnosticsEnabled;
        await Log.WriteAsync(new(EventArea.Application, EventCode.Started, Guid.NewGuid()), Lifetime.Token);
        if (LoadResult.State is SettingsLoadState.RecoveredBackup or SettingsLoadState.ResetInvalid)
            await Log.WriteAsync(new(EventArea.Error, EventCode.SettingsRecovered, Guid.NewGuid()), Lifetime.Token);
        if (!LoadResult.CanSave)
            await Log.WriteAsync(new(EventArea.Error, EventCode.SettingsUnsupported, Guid.NewGuid()), Lifetime.Token);
    }

    public async Task<bool> SaveAsync(CompanionSettings settings)
    {
        await saves.WaitAsync(Lifetime.Token);
        try
        {
            await SettingsStore.SaveAsync(settings, Lifetime.Token);
            Settings = settings;
            Log.DiagnosticsEnabled = settings.DiagnosticsEnabled;
            try
            {
                await Log.WriteAsync(new(EventArea.Application, EventCode.SettingsSaved, Guid.NewGuid()), Lifetime.Token);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (OperationCanceledException) { return false; }
            // False means settings committed, but diagnostics could not be written.
        }
        finally { saves.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await Lifetime.CancelAsync();
        await saves.WaitAsync();
        try
        {
            await Instance.DisposeAsync();
            SettingsStore.Dispose();
            Log.Dispose();
            Lifetime.Dispose();
        }
        finally { saves.Release(); saves.Dispose(); }
    }
}
