using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Programmatic real-window checks; these do not simulate or certify user input.</summary>
internal static class AppearanceSmokeChecks
{
    internal static void CheckSettingsLayout(SettingsWindow window)
    {
        CheckSettingsBounds(window);
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.UpdateLayout();
        CheckSettingsBounds(window);
    }

    private static void CheckSettingsBounds(SettingsWindow window)
    {
        var (bounds, workArea) = DesktopWindowPlacement.ReadBounds(new WindowInteropHelper(window).Handle);
        var saveBottom = window.SaveButton.TransformToAncestor(window)
            .Transform(new Point(window.SaveButton.ActualWidth, window.SaveButton.ActualHeight));
        // Read the physical HWND including its non-client area, not just XAML DesiredSize.
        if (bounds.Width > workArea.Width + 1 || bounds.Height > workArea.Height + 1 ||
            window.SaveButton.ActualHeight < 32 || !window.SaveButton.IsVisible ||
            saveBottom.X > window.ActualWidth || saveBottom.Y > window.ActualHeight)
            throw new InvalidOperationException("Settings must fit the current work area with Save visible.");
    }

    internal static async Task RunAsync(CompanionWindow window, CompanionSettings original, string dataDirectory)
    {
        List<string> passed = [];
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Appearance smoke failed: " + name);
            passed.Add(name);
        }

        async Task SettleAsync() => await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        var handle = new WindowInteropHelper(window).Handle;
        Check("real-window-handle", handle != 0);
        foreach (var scale in new[] { 0.75, 1d, 1.5 })
        {
            window.Apply(original with { AvatarScale = scale, AvatarOpacity = 0.4, ReducedMotion = true });
            await SettleAsync();
            Check($"avatar-size-{scale}", Math.Abs(window.AvatarVisual.ActualWidth - 140 * scale) < 0.01 &&
                Math.Abs(window.AvatarVisual.ActualHeight - 110 * scale) < 0.01 &&
                window.DragSurface.ActualHeight >= window.AvatarVisual.ActualHeight);
            Check($"opacity-and-reduced-motion-{scale}",
                window.Opacity == (SystemParameters.HighContrast ? 1 : 0.4) && !window.IsIdleAnimationRunning);
        }

        var movementPeer = UIElementAutomationPeer.CreatePeerForElement(window.DragSurface);
        Check("keyboard-movement-surface", window.DragSurface.Focusable &&
            window.DragSurface.FocusVisualStyle is not null &&
            movementPeer is not null && movementPeer.IsKeyboardFocusable() &&
            movementPeer.GetName() == "Move companion" && movementPeer.GetAutomationId() == "MoveCompanion" &&
            movementPeer.GetHelpText().Contains("arrow keys", StringComparison.Ordinal));
        DesktopWindowPlacement.MoveBy(handle, int.MaxValue, int.MinValue);
        await SettleAsync();
        var bounds = DesktopWindowPlacement.ReadBounds(handle);
        Check("real-work-area-top-right", bounds.Window.Top == bounds.WorkArea.Top &&
            bounds.Window.Left == bounds.WorkArea.Left + Math.Max(0, bounds.WorkArea.Width - bounds.Window.Width));
        DesktopWindowPlacement.MoveBy(handle, int.MinValue, int.MaxValue);
        await SettleAsync();
        bounds = DesktopWindowPlacement.ReadBounds(handle);
        Check("real-work-area-bottom-left", bounds.Window.Left == bounds.WorkArea.Left &&
            bounds.Window.Top == bounds.WorkArea.Top + Math.Max(0, bounds.WorkArea.Height - bounds.Window.Height));
        DesktopWindowPlacement.MoveBy(handle, 1, -1);
        DesktopWindowPlacement.Constrain(handle, true);
        bounds = DesktopWindowPlacement.ReadBounds(handle);
        Check("real-edge-snap", bounds.Window.Left == bounds.WorkArea.Left &&
            bounds.Window.Top == bounds.WorkArea.Top + Math.Max(0, bounds.WorkArea.Height - bounds.Window.Height));

        window.Apply(original with { ReducedMotion = false });
        await SettleAsync();
        Check("idle-motion-follows-system-preference", window.IsIdleAnimationRunning ==
            (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast));
        var blinkChecksEnabled = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        if (blinkChecksEnabled)
        {
            var renderer = window.AvatarRenderer;
            var completedBefore = renderer.CompletedBlinkCount;
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!renderer.IsBlinkClosed && deadline.Elapsed < TimeSpan.FromSeconds(13))
                await Task.Delay(15);
            Check("idle-blink-closes-without-animation-clocks", renderer.IsBlinkClosed &&
                renderer.IsIdleBlinkTimerEnabled && renderer.OwnedAnimationClockCount == 0);
            window.Apply(original with { ReducedMotion = true });
            Check("interrupted-blink-does-not-count", !renderer.IsBlinkClosed &&
                !renderer.IsIdleBlinkTimerEnabled && renderer.CompletedBlinkCount == completedBefore);
            window.Apply(original with { ReducedMotion = false });
            deadline.Restart();
            while (renderer.CompletedBlinkCount == completedBefore && deadline.Elapsed < TimeSpan.FromSeconds(13))
                await Task.Delay(50);
            Check("idle-blink-completes", renderer.CompletedBlinkCount > completedBefore &&
                renderer.OwnedAnimationClockCount == 0);
            window.Preview(AvatarState.Speaking);
            Check("speaking-owns-two-clocks", renderer.OwnedAnimationClockCount == 2 &&
                !renderer.IsIdleBlinkTimerEnabled);
            window.EndPreview();
            Check("idle-releases-speaking-clocks", renderer.OwnedAnimationClockCount == 0 &&
                renderer.IsIdleBlinkTimerEnabled);
        }
        window.Hide();
        Check("hidden-motion-stopped", !window.IsIdleAnimationRunning);
        Check("hidden-releases-animation-resources", !window.AvatarRenderer.IsIdleBlinkTimerEnabled &&
            window.AvatarRenderer.OwnedAnimationClockCount == 0);
        window.Show();
        window.IsEnabled = false;
        Check("disabled-motion-stopped", !window.IsIdleAnimationRunning);
        window.IsEnabled = true;
        var restoreBounds = window.RestoreBounds;
        window.WindowState = WindowState.Minimized;
        window.Apply(original with { ReducedMotion = false });
        await SettleAsync();
        Check("minimized-motion-stopped", !window.IsIdleAnimationRunning);
        Check("minimized-placement-preserves-restore-position", window.RestoreBounds.Left == restoreBounds.Left &&
            window.RestoreBounds.Top == restoreBounds.Top);
        window.WindowState = WindowState.Normal;
        window.Apply(original);
        await SettleAsync();
        foreach (var state in Enum.GetValues<AvatarState>())
        {
            window.Preview(state);
            await SettleAsync();
            Check("state-preview-" + state, window.StateLabel.Text == "Preview · " + state &&
                (state != AvatarState.Sleeping || !window.IsIdleAnimationRunning));
        }
        window.EndPreview();
        Check("preview-returns-to-real-state", window.StateLabel.Text == "Ready" && window.CurrentState == AvatarState.Idle);
        window.ToggleSleep();
        Check("sleep-stops-motion", window.CurrentState == AvatarState.Sleeping && !window.IsIdleAnimationRunning);
        window.ToggleSleep();
        Check("wake-restores-idle", window.CurrentState == AvatarState.Idle && window.StateLabel.Text == "Ready");
        var saved = DesktopWindowPlacement.Capture(handle);
        DesktopWindowPlacement.MoveBy(handle, int.MaxValue, int.MinValue);
        Check("saved-monitor-found", DesktopWindowPlacement.BeginRestore(handle, saved));
        await SettleAsync();
        DesktopWindowPlacement.CompleteRestore(handle, saved);
        var restored = DesktopWindowPlacement.Capture(handle);
        Check("native-placement-round-trip", restored.MonitorDeviceId == saved.MonitorDeviceId &&
            Math.Abs(restored.HorizontalFraction - saved.HorizontalFraction) < 0.002 &&
            Math.Abs(restored.VerticalFraction - saved.VerticalFraction) < 0.002);
        Check("missing-monitor-detected", !DesktopWindowPlacement.BeginRestore(handle, saved with { MonitorDeviceId = "missing-test-monitor" }));
        await SettleAsync();
        bounds = DesktopWindowPlacement.ReadBounds(handle);
        Check("missing-monitor-fallback-contained", WindowPlacement.Constrain(bounds.Window, bounds.WorkArea) == bounds.Window);
        var placementDirectory = Path.Combine(dataDirectory, "placement-session-check");
        await using (var session = new PlacementSession(window, placementDirectory))
        {
            await session.InitializeAsync();
            DesktopWindowPlacement.MoveBy(handle, 40, -40);
            window.CommitPlacement();
            var expected = DesktopWindowPlacement.Capture(handle);
            window.Hide();
            await session.DisposeAsync();
            using var savedStore = new JsonPlacementStore(placementDirectory);
            var loaded = await savedStore.LoadAsync(CancellationToken.None);
            Check("move-hide-exit-preserves-pending-position", loaded.Placement == expected);
            window.Show();
        }
        using (var savedStore = new JsonPlacementStore(placementDirectory))
        {
            var loaded = await savedStore.LoadAsync(CancellationToken.None);
            await using var restoredSession = new PlacementSession(window, placementDirectory);
            DesktopWindowPlacement.MoveBy(handle, int.MaxValue, int.MinValue);
            await restoredSession.InitializeAsync();
            var afterRestore = DesktopWindowPlacement.Capture(handle);
            Check("session-restores-last-move", loaded.Placement is { } previous &&
                Math.Abs(afterRestore.HorizontalFraction - previous.HorizontalFraction) < 0.002 &&
                Math.Abs(afterRestore.VerticalFraction - previous.VerticalFraction) < 0.002);
        }
        DesktopWindowPlacement.CompleteRestore(handle, saved);
        window.CommitPlacement();
        var monitors = DesktopWindowPlacement.EnumerateMonitors();
        Check("native-monitor-snapshot", monitors.Count > 0 &&
            monitors.Select(monitor => monitor.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == monitors.Count);
        var beforeNext = DesktopWindowPlacement.Capture(handle);
        var nextPlacement = DesktopWindowPlacement.NextMonitorPlacement(handle);
        await window.MoveToNextMonitorAsync();
        var afterNext = DesktopWindowPlacement.Capture(handle);
        Check("native-next-display-action", nextPlacement is null ? beforeNext == afterNext :
            afterNext.MonitorDeviceId == nextPlacement.MonitorDeviceId &&
            Math.Abs(afterNext.HorizontalFraction - nextPlacement.HorizontalFraction) < 0.002 &&
            Math.Abs(afterNext.VerticalFraction - nextPlacement.VerticalFraction) < 0.002);
        DesktopWindowPlacement.BeginRestore(handle, saved);
        await SettleAsync();
        DesktopWindowPlacement.CompleteRestore(handle, saved);
        window.ReportDesktopStatus("Storage warning test");
        window.ReportShortcutStatus("Shortcut warning test");
        window.ConstrainToWorkArea(false);
        Check("geometry-preserves-desktop-errors", window.DesktopStatus.Text.Contains("Storage warning test", StringComparison.Ordinal) &&
            window.DesktopStatus.Text.Contains("Shortcut warning test", StringComparison.Ordinal));
        window.ReportShortcutStatus(null);
        Check("shortcut-success-preserves-other-errors", window.DesktopStatus.Text == "Storage warning test");
        window.ReportDesktopStatus(string.Empty);
        await SettleAsync();
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "appearance-smoke.json"),
            JsonSerializer.Serialize(new { passed, count = passed.Count, monitors, blinkChecksEnabled,
                skipped = blinkChecksEnabled ? Array.Empty<string>() : new[] {
                    "Five animated blink/clock checks skipped because Windows motion is disabled or high contrast is enabled." } }));
    }
}
