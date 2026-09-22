using System.IO;
using System.Text.Json;
using Companion.Core.Configuration;
using Companion.Infrastructure.Lifetime;

namespace Companion.Desktop;

/// <summary>Controlled save completion after shutdown, using isolated profiles and no startup writes.</summary>
internal static class SettingsLifecycleSmokeChecks
{
    internal static async Task RunAsync(CompanionRuntime runtime, string dataDirectory)
    {
        List<string> passed = [];
        List<string> profiles = [];
        string? failure = null;
        var originalSettings = runtime.Settings;
        var parentSettingsPath = Path.Combine(dataDirectory, "settings.json");
        var originalFile = File.Exists(parentSettingsPath) ? await File.ReadAllBytesAsync(parentSettingsPath) : null;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Settings lifecycle smoke failed: " + name);
            passed.Add(name);
        }

        try
        {
            foreach (var completion in new[] { "committed", "cancelled", "io-failure" })
            {
                var profileName = "settings-lifecycle-" + Guid.NewGuid().ToString("N");
                var profilePath = Path.Combine(dataDirectory, profileName);
                profiles.Add(profileName);
                var instance = SingleInstanceCoordinator.TryAcquire(profilePath)
                    ?? throw new InvalidOperationException("The isolated lifecycle profile is already running.");
                await using var fixtureRuntime = new CompanionRuntime(profilePath, instance);
                await fixtureRuntime.InitializeAsync();
                await CheckShutdownAsync(fixtureRuntime, profilePath, completion, Check);
            }
            Check("parent-runtime-preferences-unchanged", runtime.Settings == originalSettings);
            var afterFile = File.Exists(parentSettingsPath) ? await File.ReadAllBytesAsync(parentSettingsPath) : null;
            Check("parent-settings-file-unchanged", originalFile is null ? afterFile is null :
                afterFile is not null && originalFile.SequenceEqual(afterFile));
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "settings-lifecycle-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, profiles, failure }));
        }
    }

    private static async Task CheckShutdownAsync(CompanionRuntime runtime, string profilePath, string completion,
        Action<string, bool> check)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CompanionSettings? requestedSettings = null;
        var companion = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false };
        List<string> desktopErrors = [];
        DesktopIntegration? desktop = null;
        SettingsWindow? settings = null;
        var closed = false;
        Task? save = null;
        try
        {
            companion.Apply(runtime.Settings with { CompanionName = "Lifecycle original", ReducedMotion = true });
            companion.Opacity = 0;
            companion.Show(); // WPF requires an owner to have been shown before assigning Owner.
            var originalName = companion.NameLabel.Text;
            desktop = new DesktopIntegration(companion, () => { }, () => { }, () => { },
                desktopErrors.Add, enableSystemIntegration: false);
            settings = new SettingsWindow(runtime, companion, desktop, allowStartup: false, saveOperation: async value =>
            {
                calls++;
                requestedSettings = value;
                try
                {
                    var logged = completion == "committed" ? await runtime.SaveAsync(value) : true;
                    ready.TrySetResult(logged);
                    await pending.Task;
                    return logged;
                }
                catch (Exception error)
                {
                    ready.TrySetException(error);
                    throw;
                }
            })
            {
                Owner = companion, ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            settings.Closed += (_, _) => closed = true;
            settings.Show();
            settings.NameInput.Text = "Must not apply after shutdown";
            var originalStatus = settings.StatusLabel.Text;
            var originalStartupStatus = settings.StartupStatus.Text;
            save = settings.SaveChangesAsync();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(3));
            check(completion + "-save-awaits-controlled-completion", calls == 1 && !save.IsCompleted &&
                requestedSettings?.CompanionName == "Must not apply after shutdown");
            settings.BeginShutdown();
            check(completion + "-shutdown-disables-window-and-save", !settings.IsEnabled && !settings.SaveButton.IsEnabled &&
                settings.SaveButton.ReadLocalValue(System.Windows.UIElement.IsEnabledProperty) is false);
            // This is the real disposed adapter path that the old post-save continuation reached.
            desktop.Dispose();
            switch (completion)
            {
                case "committed": pending.SetResult(true); break;
                case "cancelled": pending.SetCanceled(); break;
                default: pending.SetException(new IOException("Injected save failure.")); break;
            }
            await save.WaitAsync(TimeSpan.FromSeconds(3));
            check(completion + "-save-continuation-finishes", save.IsCompletedSuccessfully);
            check(completion + "-companion-is-not-reapplied", companion.NameLabel.Text == originalName);
            check(completion + "-no-late-error-or-startup-status", settings.StatusLabel.Text == originalStatus &&
                settings.StartupStatus.Text == originalStartupStatus && desktopErrors.Count == 0);
            check(completion + "-controls-remain-disabled", !settings.IsEnabled && !settings.SaveButton.IsEnabled &&
                settings.SaveButton.ReadLocalValue(System.Windows.UIElement.IsEnabledProperty) is false);
            var fixtureSettingsPath = Path.Combine(profilePath, "settings.json");
            if (completion == "committed")
            {
                var persisted = JsonSerializer.Deserialize<CompanionSettings>(await File.ReadAllTextAsync(fixtureSettingsPath));
                check("committed-data-preserved-after-shutdown", persisted == requestedSettings && runtime.Settings == requestedSettings);
            }
            else check(completion + "-does-not-commit-fixture-settings", !File.Exists(fixtureSettingsPath));
            await settings.SaveChangesAsync();
            check(completion + "-new-save-after-shutdown-is-ignored", calls == 1);
            settings.Close();
            check(completion + "-window-closes-after-save-completion", closed);
        }
        finally
        {
            settings?.BeginShutdown();
            pending.TrySetCanceled();
            try { if (save is not null) await save.WaitAsync(TimeSpan.FromSeconds(3)); }
            finally
            {
                if (!closed) settings?.Close();
                desktop?.Dispose();
                companion.Close();
            }
        }
    }
}
