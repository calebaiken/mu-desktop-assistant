using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Companion.Core.Desktop;

namespace Companion.Desktop;

public partial class CompanionWindow
{
    private readonly DispatcherTimer replyPreviewDismissTimer = new(DispatcherPriority.Background)
        { Interval = TimeSpan.FromSeconds(20) };
    private readonly Popup replyPreviewPopup = new();
    private readonly TextBlock replyPreviewBody = new();
    internal bool ReplyPreviewVisible => replyPreviewPopup.IsOpen;
    internal string ReplyPreviewText => replyPreviewBody.Text;
    // Native diagnostic fixtures can shorten the timer without waiting 20 seconds per case.
    internal DispatcherTimer ReplyPreviewTimer => replyPreviewDismissTimer;

    private void InitializeReplyPreview()
    {
        var close = new Button { Content = "×", Width = 32, Height = 32, Padding = new(0), ToolTip = "Dismiss reply preview", HorizontalAlignment = HorizontalAlignment.Right };
        AutomationProperties.SetName(close, "Dismiss reply preview");
        AutomationProperties.SetAutomationId(close, "DismissReplyPreview");
        close.Click += (_, _) => DismissReplyPreview();
        var open = new Button { Content = "Open chat", MinWidth = 88, Height = 32, Padding = new(10, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(open, "Open chat to read the complete reply");
        AutomationProperties.SetAutomationId(open, "ReadCompleteReply");
        open.Click += (_, _) =>
        {
            DismissReplyPreview();
            ChatRequested?.Invoke(this, EventArgs.Empty);
        };

        var title = new TextBlock { Text = "Reply ready", FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        replyPreviewBody.FontSize = 13;
        replyPreviewBody.LineHeight = 18;
        replyPreviewBody.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        replyPreviewBody.MaxHeight = 54;
        replyPreviewBody.TextWrapping = TextWrapping.Wrap;
        replyPreviewBody.TextTrimming = TextTrimming.CharacterEllipsis;
        replyPreviewBody.Margin = new(0, 3, 0, 8);
        AutomationProperties.SetName(replyPreviewBody, "Completed reply preview");
        AutomationProperties.SetAutomationId(replyPreviewBody, "ReplyPreviewText");
        AutomationProperties.SetLiveSetting(replyPreviewBody, AutomationLiveSetting.Polite);

        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(close, 1);
        Grid.SetRow(replyPreviewBody, 1);
        Grid.SetColumnSpan(replyPreviewBody, 2);
        Grid.SetRow(open, 2);
        Grid.SetColumnSpan(open, 2);
        grid.Children.Add(title);
        grid.Children.Add(close);
        grid.Children.Add(replyPreviewBody);
        grid.Children.Add(open);
        KeyboardNavigation.SetTabNavigation(grid, KeyboardNavigationMode.Cycle);
        grid.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            DismissReplyPreview();
            e.Handled = true;
        };
        var border = new Border { Width = 272, Padding = new(10), BorderThickness = new(1), CornerRadius = new(12), Child = grid };
        border.SetResourceReference(Border.BackgroundProperty, SystemColors.WindowBrushKey);
        border.SetResourceReference(Border.BorderBrushProperty, SystemColors.ActiveBorderBrushKey);
        title.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.WindowTextBrushKey);
        replyPreviewBody.SetResourceReference(TextBlock.ForegroundProperty, SystemColors.WindowTextBrushKey);
        replyPreviewPopup.Child = border;
        replyPreviewPopup.PlacementTarget = AvatarVisual;
        replyPreviewPopup.Placement = PlacementMode.Top;
        replyPreviewPopup.VerticalOffset = -6;
        replyPreviewPopup.AllowsTransparency = true;
        replyPreviewPopup.PopupAnimation = PopupAnimation.None;
        replyPreviewPopup.StaysOpen = true;
        replyPreviewPopup.Focusable = false;
        replyPreviewPopup.Closed += (_, _) =>
        {
            if (replyPreviewPopup.IsOpen) return;
            replyPreviewDismissTimer.Stop();
            replyPreviewBody.Text = string.Empty;
        };
        CompanionRoot.Children.Add(replyPreviewPopup);
        replyPreviewDismissTimer.Tick += (_, _) => DismissReplyPreview();
        IsVisibleChanged += (_, _) => DismissReplyPreviewIfUnavailable();
        IsEnabledChanged += (_, _) => DismissReplyPreviewIfUnavailable();
        CompanionRoot.IsEnabledChanged += (_, _) => DismissReplyPreviewIfUnavailable();
        StateChanged += (_, _) => DismissReplyPreviewIfUnavailable();
        states.Changed += (_, _) => DismissReplyPreviewIfUnavailable();
        LocationChanged += (_, _) => DismissReplyPreview();
        SizeChanged += (_, _) => DismissReplyPreview();
        Closed += (_, _) => DismissReplyPreview();
        // The existing Escape handler marks its routed event handled, so also listen to handled events.
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == Key.Escape) DismissReplyPreview();
        }), handledEventsToo: true);
    }

    internal void ShowReplyPreview(string reply)
    {
        Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(reply);
        if (!CanShowReplyPreview()) { DismissReplyPreview(); return; }
        var text = BoundedReplyPreview(reply);
        if (text.Length == 0) { DismissReplyPreview(); return; }
        replyPreviewDismissTimer.Stop();
        replyPreviewBody.Text = text;
        replyPreviewPopup.HorizontalOffset = (AvatarVisual.ActualWidth - 272) / 2;
        // WPF Popup positions this separate, non-activating surface within the available screen.
        // No Focus/Activate or keyboard selection is requested when publishing a reply.
        replyPreviewPopup.IsOpen = true;
        replyPreviewDismissTimer.Start();
    }

    internal void DismissReplyPreview()
    {
        Dispatcher.VerifyAccess();
        replyPreviewDismissTimer.Stop();
        replyPreviewPopup.IsOpen = false;
        replyPreviewBody.Text = string.Empty;
    }

    private bool CanShowReplyPreview() => !closed && IsLoaded && IsVisible && IsEnabled &&
        CompanionRoot.IsEnabled && WindowState == WindowState.Normal && states.Current != AvatarState.Sleeping;

    private void DismissReplyPreviewIfUnavailable()
    {
        if (!CanShowReplyPreview()) DismissReplyPreview();
    }

    private static string BoundedReplyPreview(string reply)
    {
        var result = new StringBuilder(240);
        var whitespace = false;
        var clipped = false;
        // Bound source inspection too: a huge whitespace prefix must not monopolize the UI thread.
        var inspected = 0;
        foreach (var rune in reply.EnumerateRunes())
        {
            if (++inspected > 8192) { clipped = true; break; }
            if (Rune.IsWhiteSpace(rune)) { whitespace = result.Length != 0; continue; }
            var required = rune.Utf16SequenceLength + (whitespace ? 1 : 0);
            if (result.Length + required > 239) { clipped = true; break; }
            if (whitespace) result.Append(' ');
            result.Append(rune.ToString());
            whitespace = false;
        }
        if (clipped && result.Length != 0) result.Append('…');
        return result.ToString();
    }
}
