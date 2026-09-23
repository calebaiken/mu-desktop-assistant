using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Real placement storage and HWNDs with a controlled pre-load pause; no physical input is simulated.</summary>
internal static class PlacementInitializationSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        List<string> passed = [];
        List<string> profiles = [];
        string? failure = null;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Placement initialization smoke failed: " + name);
            passed.Add(name);
        }

        try
        {
            foreach (var scenario in new[] { "normal", "already-disabled", "hidden", "minimized", "closed", "stopped", "io-failure", "future-schema" })
            {
                var profileName = "placement-initialization-" + Guid.NewGuid().ToString("N");
                profiles.Add(profileName);
                await CheckScenarioAsync(Path.Combine(dataDirectory, profileName), scenario, Check);
            }
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "placement-initialization-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, profiles, failure }));
        }
    }

    private static async Task CheckScenarioAsync(string directory, string scenario, Action<string, bool> check)
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false };
        PlacementSession? session = null;
        FileStream? locked = null;
        Task? initialization = null;
        Task? disposal = null;
        var closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            window.Apply(new CompanionSettings { ReducedMotion = true });
            window.Opacity = 0;
            window.Show();
            await SettleAsync(window);
            var handle = new WindowInteropHelper(window).Handle;
            var saved = DesktopWindowPlacement.Capture(handle) with { HorizontalFraction = .25, VerticalFraction = .65 };
            using (var seed = new JsonPlacementStore(directory))
            {
                await seed.LoadAsync(CancellationToken.None);
                await seed.SaveAsync(saved, CancellationToken.None);
            }
            var path = Path.Combine(directory, "placement.json");
            if (scenario == "future-schema") await File.WriteAllTextAsync(path, "{\"SchemaVersion\":99}");
            var originalBytes = await File.ReadAllBytesAsync(path);
            DesktopWindowPlacement.MoveBy(handle, int.MaxValue, int.MinValue);
            await SettleAsync(window);
            var before = DesktopWindowPlacement.ReadBounds(handle).Window;
            window.MovementStatus.Text = "Pending placement fixture";
            if (scenario == "already-disabled") window.IsEnabled = false;
            var loadGateEntered = false;
            session = new(window, directory, beforeLoadAsync: async () =>
            {
                loadGateEntered = true;
                await release.Task;
            });
            initialization = session.InitializeAsync();
            check(scenario + "-input-disabled-before-load-await", loadGateEntered && !initialization.IsCompleted && !window.IsEnabled);
            await window.MoveToNextMonitorAsync();
            window.CommitPlacement();
            check(scenario + "-pending-input-command-is-ignored",
                DesktopWindowPlacement.ReadBounds(handle).Window == before && window.MovementStatus.Text == "Pending placement fixture");
            check(scenario + "-reduced-motion-fixture-is-idle", !window.IsIdleAnimationRunning);

            var restoreBounds = window.RestoreBounds;
            switch (scenario)
            {
                case "hidden": window.Hide(); break;
                case "minimized": window.WindowState = WindowState.Minimized; break;
                case "closed": window.Close(); break;
                case "stopped":
                    disposal = session.DisposeAsync().AsTask();
                    check("stopped-disposal-waits-for-initialization", !disposal.IsCompleted);
                    break;
                case "io-failure": locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None); break;
            }
            release.SetResult(true);
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            check(scenario + "-initialization-completes", initialization.IsCompletedSuccessfully);

            switch (scenario)
            {
                case "normal":
                case "already-disabled":
                    var actual = DesktopWindowPlacement.Capture(handle);
                    check(scenario + "-saved-position-restored", Near(actual, saved));
                    check(scenario + "-prior-enabled-state-preserved", window.IsEnabled == (scenario == "normal"));
                    if (scenario == "normal")
                    {
                        window.MovementStatus.Text = "Post-load command fixture";
                        await window.MoveToNextMonitorAsync();
                        check("normal-input-command-available-after-restore", window.MovementStatus.Text != "Post-load command fixture");
                    }
                    break;
                case "hidden":
                    check("hidden-window-remains-hidden", !window.IsVisible && window.IsEnabled);
                    check("hidden-window-position-not-restored", DesktopWindowPlacement.ReadBounds(handle).Window == before);
                    break;
                case "minimized":
                    check("minimized-window-remains-minimized", window.WindowState == WindowState.Minimized && window.IsEnabled);
                    check("minimized-restore-position-not-overwritten", window.RestoreBounds == restoreBounds);
                    break;
                case "closed":
                    check("closed-window-is-not-reenabled", closed && !window.IsEnabled);
                    break;
                case "stopped":
                    check("stopped-window-is-not-reenabled", !window.IsEnabled);
                    check("stopped-window-position-not-restored", DesktopWindowPlacement.ReadBounds(handle).Window == before);
                    break;
                case "io-failure":
                    check("io-failure-restores-input-with-warning", window.IsEnabled &&
                        window.DesktopStatus.Text.Contains("could not be loaded", StringComparison.Ordinal));
                    // Reporting a warning must not move the companion while initialization is pending.
                    // Compare position, not dimensions, to detect an unwanted saved-position restore.
                    var failureBounds = DesktopWindowPlacement.ReadBounds(handle).Window;
                    check("io-failure-position-not-restored", failureBounds.Left == before.Left && failureBounds.Top == before.Top);
                    break;
                case "future-schema":
                    check("future-schema-restores-input-with-warning", window.IsEnabled &&
                        window.DesktopStatus.Text.Contains("has been preserved", StringComparison.Ordinal));
                    window.CommitPlacement();
                    await session.DisposeAsync();
                    var preservedBytes = await File.ReadAllBytesAsync(path);
                    check("future-schema-is-never-overwritten", originalBytes.SequenceEqual(preservedBytes));
                    break;
            }
        }
        finally
        {
            locked?.Dispose();
            release.TrySetResult(true);
            try
            {
                if (initialization is not null) await initialization.WaitAsync(TimeSpan.FromSeconds(5));
                if (session is not null) await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                if (disposal is not null) await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { if (!closed) window.Close(); }
        }
    }

    private static bool Near(SavedPlacement actual, SavedPlacement expected) =>
        string.Equals(actual.MonitorDeviceId, expected.MonitorDeviceId, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(actual.HorizontalFraction - expected.HorizontalFraction) < .002 &&
        Math.Abs(actual.VerticalFraction - expected.VerticalFraction) < .002;

    private static async Task SettleAsync(CompanionWindow window) =>
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
}
