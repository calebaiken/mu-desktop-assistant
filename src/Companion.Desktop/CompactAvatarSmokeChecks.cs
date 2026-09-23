using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>
/// Reviewer-authored real-HWND layout and disclosure-policy checks. Direct policy calls
/// are not proof of physical pointer travel, keyboard navigation, or screen-reader behavior.
/// </summary>
internal static class CompactAvatarSmokeChecks
{
    internal static async Task RunAsync(CompanionWindow window, CompanionSettings original, string dataDirectory)
    {
        List<string> passed = [];
        string? failure = null;
        void Check(string name, bool condition, string? details = null)
        {
            if (!condition) throw new InvalidOperationException("Compact avatar smoke failed: " + name +
                (details is null ? string.Empty : " (" + details + ")"));
            passed.Add(name);
        }

        async Task SettleAsync() => await window.Dispatcher.InvokeAsync(
            () => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        void Disclose(bool pointer = false, bool focus = false, bool keyboard = false, bool menu = false) =>
            window.ApplyControlsVisibility(pointer, focus, keyboard, menu);

        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            Check("real-window-handle", handle != 0);
            Check("transparent-window-without-frame", window.AllowsTransparency && window.WindowStyle == WindowStyle.None &&
                window.Background is SolidColorBrush background && background.Color.A == 0);

            // Independent values calculated from the specified dimensions, not read from production constants.
            foreach (var example in new[] {
                (Scale: .75, AvatarWidth: 84d, AvatarHeight: 66d, Width: 136d, Height: 110d),
                (Scale: 1d, AvatarWidth: 112d, AvatarHeight: 88d, Width: 136d, Height: 132d),
                (Scale: 1.5, AvatarWidth: 168d, AvatarHeight: 132d, Width: 184d, Height: 176d) })
            {
                window.Apply(original with { AvatarScale = example.Scale, ReducedMotion = true });
                await SettleAsync();
                Disclose();
                var dpi = VisualTreeHelper.GetDpi(window);
                // HWND dimensions occupy whole device pixels: at 125%, 110 DIPs is
                // 137.5 pixels and rounds to 138 pixels / 110.4 DIPs after placement.
                // Keep avatar layout exact; admit only half a physical pixel for the HWND.
                Check($"compact-dimensions-{example.Scale}", WithinHalfPixel(window.ActualWidth, example.Width, dpi.DpiScaleX) &&
                    WithinHalfPixel(window.ActualHeight, example.Height, dpi.DpiScaleY) &&
                    Near(window.AvatarVisual.ActualWidth, example.AvatarWidth) &&
                    Near(window.AvatarVisual.ActualHeight, example.AvatarHeight),
                    $"requested={window.Width:R}x{window.Height:R}; actual={window.ActualWidth:R}x{window.ActualHeight:R}; " +
                    $"avatar={window.AvatarVisual.ActualWidth:R}x{window.AvatarVisual.ActualHeight:R}; " +
                    $"DPI={dpi.DpiScaleX:R}x{dpi.DpiScaleY:R}; expected={example.Width:R}x{example.Height:R}, " +
                    $"avatar={example.AvatarWidth:R}x{example.AvatarHeight:R}");
                Check($"resting-character-hides-card-text-{example.Scale}", !window.ControlsVisible &&
                    !window.NameLabel.IsVisible && !window.StateLabel.IsVisible &&
                    !window.MovementStatus.IsVisible && !window.DesktopStatus.IsVisible);
                var avatarBounds = window.AvatarVisual.TransformToAncestor(window).TransformBounds(
                    new Rect(0, 0, window.AvatarVisual.ActualWidth, window.AvatarVisual.ActualHeight));
                var restImage = RenderContent(window);
                var restPixels = Pixels(restImage);
                var blankRowsStart = Math.Min(restImage.PixelHeight, (int)Math.Ceiling(avatarBounds.Bottom) + 1);
                Check($"resting-lower-strip-is-alpha-transparent-{example.Scale}",
                    Enumerable.Range(blankRowsStart * restImage.PixelWidth, (restImage.PixelHeight - blankRowsStart) * restImage.PixelWidth)
                        .All(pixel => restPixels[pixel * 4 + 3] == 0));
                if (example.Scale == 1) SavePng(restImage, Path.Combine(dataDirectory, "compact-avatar-idle.png"));
                var before = DesktopWindowPlacement.ReadBounds(handle).Window;
                Disclose(pointer: true);
                window.UpdateLayout();
                Check($"disclosure-preserves-native-bounds-{example.Scale}", window.ControlsVisible &&
                    DesktopWindowPlacement.ReadBounds(handle).Window == before);
                var toolbarBounds = window.HoverControls.TransformToAncestor(window).TransformBounds(
                    new Rect(0, 0, window.HoverControls.ActualWidth, window.HoverControls.ActualHeight));
                Check($"toolbar-overlaps-avatar-region-{example.Scale}", avatarBounds.Bottom > toolbarBounds.Top &&
                    toolbarBounds.Left < avatarBounds.Left + avatarBounds.Width / 2 &&
                    toolbarBounds.Right > avatarBounds.Left + avatarBounds.Width / 2 &&
                    toolbarBounds.Left >= 0 && toolbarBounds.Right <= window.ActualWidth &&
                    toolbarBounds.Bottom <= window.ActualHeight);
                var hoverImage = RenderContent(window);
                var hoverPixels = Pixels(hoverImage);
                var bridgeX = (int)Math.Floor(avatarBounds.Left + avatarBounds.Width / 2);
                var bridgeTop = (int)Math.Ceiling(avatarBounds.Top + avatarBounds.Height / 2);
                var bridgeBottom = (int)Math.Floor(toolbarBounds.Top + toolbarBounds.Height / 2);
                Check($"painted-center-path-connects-avatar-to-toolbar-{example.Scale}",
                    Enumerable.Range(bridgeTop, bridgeBottom - bridgeTop + 1)
                        .All(row => hoverPixels[(row * hoverImage.PixelWidth + bridgeX) * 4 + 3] > 0));
                if (example.Scale == 1) SavePng(hoverImage, Path.Combine(dataDirectory, "compact-avatar-hover.png"));
                foreach (var button in new[] { window.ChatButton, window.SettingsButton, window.MoreButton })
                    Check($"fixed-button-target-{button.Name}-{example.Scale}", button.ActualWidth >= 32 &&
                        button.ActualHeight >= 32 && button.IsVisible && button.IsHitTestVisible);
            }

            window.Apply(original with { AvatarScale = 1, ReducedMotion = true });
            await SettleAsync();
            foreach (var button in new[] { window.ChatButton, window.SettingsButton, window.MoreButton })
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(button);
                Check("named-keyboard-button-" + button.Name, button.Focusable &&
                    button.IsTabStop && peer is not null &&
                    !string.IsNullOrWhiteSpace(peer.GetName()) &&
                    !string.IsNullOrWhiteSpace(peer.GetAutomationId()) &&
                    peer.GetPattern(PatternInterface.Invoke) is not null);
            }

            Disclose();
            var hiddenButtonCenter = window.ChatButton.TransformToAncestor(window).Transform(
                new Point(window.ChatButton.ActualWidth / 2, window.ChatButton.ActualHeight / 2));
            var hit = window.InputHitTest(hiddenButtonCenter) as DependencyObject;
            Check("resting-controls-not-hit-testable", !window.ControlsVisible &&
                window.HoverControls.Visibility == Visibility.Hidden &&
                !window.ChatButton.IsVisible && (hit is null ||
                    hit != window.HoverControls && !window.HoverControls.IsAncestorOf(hit)));
            Disclose(pointer: true);
            Check("pointer-enters-reveals-controls", window.ControlsVisible);
            Disclose(pointer: true, focus: true);
            Check("pointer-over-controls-keeps-controls-visible", window.ControlsVisible);
            Disclose(focus: true);
            Check("mouse-derived-focus-does-not-latch-controls", !window.ControlsVisible);
            Disclose(focus: true, keyboard: true);
            Check("keyboard-focus-reveals-controls", window.ControlsVisible);
            Disclose(focus: true, keyboard: false);
            Check("cleared-keyboard-mode-hides-controls", !window.ControlsVisible);
            Disclose(keyboard: true);
            Check("keyboard-mode-without-focus-does-not-latch-controls", !window.ControlsVisible);
            Disclose(menu: true);
            Check("open-menu-preserves-controls", window.ControlsVisible);
            Disclose();
            Check("menu-close-with-no-input-hides-controls", !window.ControlsVisible);

            var boundsBeforeWarning = DesktopWindowPlacement.ReadBounds(handle).Window;
            window.ReportDesktopStatus("Compact storage warning fixture");
            window.ReportShortcutStatus("Compact shortcut warning fixture");
            await SettleAsync();
            Check("warnings-retain-text-without-resizing", window.DesktopStatus.Text.Contains("Compact storage warning fixture", StringComparison.Ordinal) &&
                window.DesktopStatus.Text.Contains("Compact shortcut warning fixture", StringComparison.Ordinal) &&
                DesktopWindowPlacement.ReadBounds(handle).Window == boundsBeforeWarning);
            Check("compact-surface-retains-help", window.DragSurface.ToolTip is not null);
            window.ReportShortcutStatus(null);
            Check("clearing-shortcut-keeps-other-warning", window.DesktopStatus.Text == "Compact storage warning fixture");
            window.ReportDesktopStatus(string.Empty);

            window.IsEnabled = false;
            Disclose(pointer: true, focus: true, keyboard: true, menu: true);
            Check("disabled-window-blocks-disclosure", !window.ControlsVisible);
            window.IsEnabled = true;
            var content = (UIElement)window.Content;
            content.IsEnabled = false;
            Disclose(pointer: true, focus: true, keyboard: true, menu: true);
            Check("disabled-content-blocks-disclosure", !window.ControlsVisible);
            content.IsEnabled = true;
            window.WindowState = WindowState.Minimized;
            Disclose(pointer: true, focus: true, keyboard: true, menu: true);
            Check("minimized-window-blocks-disclosure", !window.ControlsVisible);
            window.WindowState = WindowState.Normal;
            window.Hide();
            Disclose(pointer: true, focus: true, keyboard: true, menu: true);
            Check("hidden-window-blocks-disclosure", !window.ControlsVisible);
            window.Show();
            await SettleAsync();
            Disclose(pointer: true);
            Check("restored-window-can-disclose-controls", window.ControlsVisible);

            var isolated = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
            try
            {
                isolated.Apply(original with { ReducedMotion = true });
                isolated.Opacity = 0;
                isolated.Show();
                await isolated.Dispatcher.InvokeAsync(() => isolated.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                var chatRequests = 0;
                var settingsRequests = 0;
                isolated.ChatRequested += (_, _) => chatRequests++;
                isolated.SettingsRequested += (_, _) => settingsRequests++;
                isolated.ApplyControlsVisibility(true, false, false, false);
                isolated.ChatButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                isolated.SettingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("button-routed-events-request-chat-and-settings", chatRequests == 1 && settingsRequests == 1);
                isolated.Close();
                isolated.ApplyControlsVisibility(true, true, true, true);
                Check("closed-window-cannot-redisclose-controls", !isolated.ControlsVisible);
            }
            finally { if (isolated.IsVisible) isolated.Close(); }
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            window.IsEnabled = true;
            ((UIElement)window.Content).IsEnabled = true;
            window.ReportDesktopStatus(string.Empty);
            window.ReportShortcutStatus(null);
            window.Apply(original);
            window.ApplyControlsVisibility(false, false, false, false);
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "compact-avatar-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure,
                    scope = "Real HWND layout and direct disclosure-policy/routed-event checks; not physical input or screen-reader qualification." }));
        }
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < .01;

    private static bool WithinHalfPixel(double actual, double expected, double dpiScale) =>
        Math.Abs(actual - expected) <= .5 / dpiScale + .000001;

    private static RenderTargetBitmap RenderContent(CompanionWindow window)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),
            (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
        return bitmap;
    }

    private static byte[] Pixels(RenderTargetBitmap bitmap)
    {
        var result = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(result, bitmap.PixelWidth * 4, 0);
        return result;
    }

    private static void SavePng(RenderTargetBitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
