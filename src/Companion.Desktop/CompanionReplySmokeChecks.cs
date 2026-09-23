using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Owned WPF popup/layout/lifecycle checks. No physical keyboard, pointer or focus claim.</summary>
internal static class CompanionReplySmokeChecks
{
    internal static async Task RunAsync(CompanionWindow owner, CompanionSettings original, string dataDirectory)
    {
        var passed = new List<string>();
        string? failure = null;
        CompanionWindow? probe = null;
        var probeClosed = false;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Companion reply smoke failed: " + name);
            passed.Add(name);
        }
        try
        {
            Check("caller-native-window-present", new WindowInteropHelper(owner).Handle != 0);
            probe = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false };
            probe.Closed += (_, _) => probeClosed = true;
            probe.Apply(original with { ReducedMotion = true, AvatarScale = 1 });
            probe.Opacity = 0;
            var popup = probe.CompanionRoot.Children.OfType<Popup>().Single();
            popup.Opacity = 0; // Preserve native geometry while keeping this isolated fixture invisible.
            probe.ShowReplyPreview("Not loaded yet");
            Check("preview-rejected-before-load", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.Show();
            await SettleAsync(probe);
            var bounds = DesktopWindowPlacement.ReadBounds(new WindowInteropHelper(probe).Handle).Window;
            Check("single-timer-default-twenty-seconds", probe.ReplyPreviewTimer.Interval == TimeSpan.FromSeconds(20));
            probe.ShowReplyPreview(" \r\n\t ");
            Check("empty-preview-remains-hidden", !probe.ReplyPreviewVisible && probe.ReplyPreviewText.Length == 0);
            probe.ShowReplyPreview("A useful completed reply.");
            await SettleAsync(probe);
            Check("completed-preview-opens-with-timer", probe.ReplyPreviewVisible && probe.ReplyPreviewTimer.IsEnabled);
            Check("preview-never-resizes-owner", DesktopWindowPlacement.ReadBounds(new WindowInteropHelper(probe).Handle).Window == bounds);
            Check("preview-anchored-above-character", popup.Placement == PlacementMode.Top && ReferenceEquals(popup.PlacementTarget, probe.AvatarVisual));
            Check("popup-no-focus-request-or-animation", !popup.Focusable && popup.StaysOpen && popup.PopupAnimation == PopupAnimation.None);
            var border = (Border)popup.Child;
            var controls = Descendants(border).ToArray();
            var body = controls.OfType<TextBlock>().Single(item => AutomationProperties.GetAutomationId(item) == "ReplyPreviewText");
            var close = controls.OfType<Button>().Single(item => AutomationProperties.GetAutomationId(item) == "DismissReplyPreview");
            var open = controls.OfType<Button>().Single(item => AutomationProperties.GetAutomationId(item) == "ReadCompleteReply");
            Check("reply-bubble-fixed-compact-width", border.ActualWidth == 272 && border.ActualHeight <= 160);
            Check("reply-body-limited-three-lines", body.LineHeight == 18 && body.MaxHeight == 54 && body.ActualHeight <= 54 && body.TextTrimming == TextTrimming.CharacterEllipsis);
            Check("reply-body-live-description", AutomationProperties.GetLiveSetting(body) == AutomationLiveSetting.Polite && AutomationProperties.GetName(body).Length != 0);
            Check("close-button-minimum-target", close.ActualWidth >= 32 && close.ActualHeight >= 32 && NamedInvokable(close));
            Check("open-button-minimum-target", open.ActualWidth >= 32 && open.ActualHeight >= 32 && NamedInvokable(open));
            var popupSource = PresentationSource.FromVisual(border) as HwndSource;
            Check("reply-has-separate-native-surface", popupSource is { Handle: not 0 } && popupSource.Handle != new WindowInteropHelper(probe).Handle);
            if (popupSource is not null)
            {
                var placement = DesktopWindowPlacement.ReadBounds(popupSource.Handle);
                Check("reply-stays-in-display-work-area", placement.Window.Left >= placement.WorkArea.Left && placement.Window.Top >= placement.WorkArea.Top &&
                    placement.Window.Left + placement.Window.Width <= placement.WorkArea.Left + placement.WorkArea.Width &&
                    placement.Window.Top + placement.Window.Height <= placement.WorkArea.Top + placement.WorkArea.Height);
            }
            else Check("reply-stays-in-display-work-area", false);

            probe.ShowReplyPreview(string.Concat(Enumerable.Repeat("😀", 200)));
            Check("preview-unicode-limit-preserves-pairs", probe.ReplyPreviewText.Length <= 240 && probe.ReplyPreviewText.EndsWith('…') &&
                !probe.ReplyPreviewText.Contains('\ufffd') && probe.ReplyPreviewText.EnumerateRunes().Count() == 120);
            probe.ShowReplyPreview("first\n\nsecond\t third");
            Check("preview-whitespace-collapses", probe.ReplyPreviewText == "first second third");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("close-dismisses-and-clears", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled && probe.ReplyPreviewText.Length == 0);
            probe.ShowReplyPreview("First reply");
            probe.ShowReplyPreview("Replacement reply");
            Check("new-reply-replaces-one-popup", probe.ReplyPreviewText == "Replacement reply" && probe.CompanionRoot.Children.OfType<Popup>().Count() == 1);
            var chatRequests = 0;
            var dismissedBeforeRequest = false;
            probe.ChatRequested += (_, _) => { chatRequests++; dismissedBeforeRequest = !probe.ReplyPreviewVisible; };
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("open-chat-dismisses-before-dispatch", chatRequests == 1 && dismissedBeforeRequest && !probe.ReplyPreviewTimer.IsEnabled);

            probe.ShowReplyPreview("Hide fixture");
            probe.Hide();
            Check("hide-dismisses-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.ShowReplyPreview("Hidden fixture");
            Check("hidden-owner-cannot-publish", !probe.ReplyPreviewVisible);
            probe.Show();
            await SettleAsync(probe);
            Check("show-does-not-revive-preview", !probe.ReplyPreviewVisible && probe.ReplyPreviewText.Length == 0);
            probe.ShowReplyPreview("Disable fixture");
            probe.IsEnabled = false;
            Check("disabled-owner-dismisses-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.IsEnabled = true;
            probe.ShowReplyPreview("Content disable fixture");
            probe.CompanionRoot.IsEnabled = false;
            Check("disabled-content-dismisses-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.CompanionRoot.IsEnabled = true;
            probe.ShowReplyPreview("Minimize fixture");
            probe.WindowState = WindowState.Minimized;
            Check("minimize-dismisses-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.WindowState = WindowState.Normal;
            await SettleAsync(probe);
            probe.ShowReplyPreview("Sleep fixture");
            probe.ToggleSleep();
            Check("sleep-dismisses-preview", probe.CurrentState == AvatarState.Sleeping && !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.ShowReplyPreview("Sleeping fixture");
            Check("sleeping-owner-cannot-publish", !probe.ReplyPreviewVisible);
            probe.ToggleSleep();
            Check("wake-does-not-revive-preview", !probe.ReplyPreviewVisible);
            probe.ShowReplyPreview("Movement fixture");
            probe.Left += 1;
            await SettleAsync(probe);
            Check("movement-dismisses-detached-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);
            probe.ShowReplyPreview("Escape fixture");
            probe.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(probe), 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check("routed-escape-dismisses-preview", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled);

            // Check the real Tick subscription with a shorter diagnostic interval. The production
            // 20-second value is checked separately; this is not a claim of a timed human session.
            probe.ReplyPreviewTimer.Interval = TimeSpan.FromMilliseconds(30);
            probe.ShowReplyPreview("Expiry fixture");
            var expires = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler ticked = (_, _) => expires.TrySetResult();
            probe.ReplyPreviewTimer.Tick += ticked;
            try { await expires.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
            finally { probe.ReplyPreviewTimer.Tick -= ticked; probe.ReplyPreviewTimer.Interval = TimeSpan.FromSeconds(20); }
            Check("shortened-timer-runs-production-dismissal", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled && probe.ReplyPreviewText.Length == 0);
            probe.ShowReplyPreview("Close fixture");
            probe.Close();
            Check("close-stops-preview-and-timer", !probe.ReplyPreviewVisible && !probe.ReplyPreviewTimer.IsEnabled && probe.ReplyPreviewText.Length == 0);
            probe.ShowReplyPreview("Closed fixture");
            Check("closed-owner-cannot-publish", !probe.ReplyPreviewVisible);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            if (probe is not null)
            {
                probe.DismissReplyPreview();
                if (!probeClosed) probe.Close();
            }
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "companion-reply-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure }));
        }
    }

    private static async Task SettleAsync(CompanionWindow window) => await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    private static bool NamedInvokable(Button button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        return button.Focusable && button.IsTabStop && peer is not null && peer.GetName().Length != 0 && peer.GetPattern(PatternInterface.Invoke) is not null;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
