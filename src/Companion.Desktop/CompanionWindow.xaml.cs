using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

public partial class CompanionWindow : Window
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? PlacementChanged;
    public event EventHandler? SleepChanged;
    private readonly AvatarStateMachine states = new();
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private AvatarState? preview;
    private CompanionSettings settings = new();
    private HwndSource? source;
    private Point? dragStart;
    private bool placementQueued;
    private bool closed;
    private string desktopMessage = string.Empty;
    private string shortcutMessage = string.Empty;
    private bool changingMonitor;
    internal bool IsIdleAnimationRunning => AvatarRenderer.IsAnimating;
    internal AvatarState CurrentState => states.Current;

    public CompanionWindow()
    {
        InitializeComponent();
        states.Changed += (_, _) => UpdateMotion();
        previewTimer.Tick += (_, _) => EndPreview();
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessage);
        };
        Loaded += (_, _) => { QueuePlacement(); UpdateMotion(); };
        SizeChanged += (_, _) => QueuePlacement();
        DpiChanged += (_, _) => QueuePlacement();
        IsVisibleChanged += (_, _) => UpdateMotion();
        IsEnabledChanged += (_, _) => UpdateMotion();
        StateChanged += (_, _) => { UpdateMotion(); QueuePlacement(); };
        SystemParameters.StaticPropertyChanged += SystemPreferencesChanged;
        Closed += (_, _) =>
        {
            closed = true;
            previewTimer.Stop();
            SystemParameters.StaticPropertyChanged -= SystemPreferencesChanged;
            if (source is { IsDisposed: false }) source.RemoveHook(WindowMessage);
            UpdateMotion();
            AvatarRenderer.Dispose();
        };
    }

    public void Apply(CompanionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Validate().Count != 0) throw new ArgumentException("Invalid companion settings.", nameof(settings));
        this.settings = settings;
        NameLabel.Text = settings.CompanionName;
        Title = settings.CompanionName + " · Desktop Companion";
        Topmost = settings.AlwaysOnTop;
        AvatarVisual.Width = 140 * settings.AvatarScale;
        AvatarVisual.Height = 110 * settings.AvatarScale;
        // Scale the character while keeping keyboard/button targets at their normal size.
        Width = Math.Max(260, AvatarVisual.Width + 104);
        UpdateHeight();
        Opacity = SystemParameters.HighContrast ? 1 : settings.AvatarOpacity;
        UpdateMotion();
        QueuePlacement();
    }

    private void UpdateMotion()
    {
        if (closed) { AvatarRenderer.Stop(); return; }
        var animate = !closed && IsLoaded && IsVisible && IsEnabled && WindowState != WindowState.Minimized &&
            !settings.ReducedMotion && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        var state = preview ?? states.Current;
        StateLabel.Text = preview.HasValue ? "Preview · " + state : state == AvatarState.Idle ? "Ready" : state.ToString();
        AvatarRenderer.Render(state, animate, SystemParameters.HighContrast);
    }

    internal void Preview(AvatarState state)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        preview = state;
        previewTimer.Stop();
        previewTimer.Start();
        UpdateMotion();
    }

    internal void EndPreview() { previewTimer.Stop(); preview = null; UpdateMotion(); }
    internal void CommitPlacement() => PlacementChanged?.Invoke(this, EventArgs.Empty);

    internal void ToggleSleep()
    {
        EndPreview();
        states.SetSleeping(states.Current != AvatarState.Sleeping);
        SleepMenuItem.Header = states.Current == AvatarState.Sleeping ? "_Wake" : "_Sleep";
        SleepChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateHeight()
    {
        DesktopStatus.Measure(new Size(Math.Max(1, Width - 66), double.PositiveInfinity));
        Height = 352 + 110 * (settings.AvatarScale - 1) +
            (string.IsNullOrEmpty(DesktopStatus.Text) ? 0 : DesktopStatus.DesiredSize.Height + 8);
    }

    internal void ReportDesktopStatus(string message)
    {
        desktopMessage = message;
        RefreshDesktopStatus();
    }

    internal void ReportShortcutStatus(string? message)
    {
        shortcutMessage = message ?? string.Empty;
        RefreshDesktopStatus();
    }

    private void RefreshDesktopStatus()
    {
        DesktopStatus.Text = string.Join(Environment.NewLine,
            new[] { desktopMessage, shortcutMessage }.Where(message => !string.IsNullOrEmpty(message)));
        UpdateHeight();
    }

    internal async Task MoveToNextMonitorAsync()
    {
        if (closed || changingMonitor || !IsLoaded || !IsVisible || !IsEnabled ||
            Content is UIElement { IsEnabled: false } || WindowState != WindowState.Normal) return;
        changingMonitor = true;
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var next = DesktopWindowPlacement.NextMonitorPlacement(handle);
            if (next is null) { MovementStatus.Text = "Only one display is available."; return; }
            DesktopWindowPlacement.BeginRestore(handle, next);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (closed || !IsVisible || !IsEnabled || WindowState != WindowState.Normal) return;
            DesktopWindowPlacement.CompleteRestore(handle, next);
            CommitPlacement();
            MovementStatus.Text = "Moved to the next display.";
        }
        catch (Win32Exception) { MovementStatus.Text = "The display changed. Try moving to the next display again."; }
        finally { changingMonitor = false; }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsEnabled || Content is UIElement { IsEnabled: false }) return;
        if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            await MoveToNextMonitorAsync();
        }
    }

    private async void NextMonitor_Click(object sender, RoutedEventArgs e) => await MoveToNextMonitorAsync();

    private void SystemPreferencesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (closed) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (closed) return;
            Opacity = SystemParameters.HighContrast ? 1 : settings.AvatarOpacity;
            UpdateMotion();
        }));
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Display topology/work-area changes; WPF handles its own DPI message first.
        if (message is 0x007E or 0x001A) QueuePlacement();
        return 0;
    }

    private void QueuePlacement()
    {
        if (closed || !IsLoaded || WindowState != WindowState.Normal || placementQueued) return;
        placementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            placementQueued = false;
            if (!closed && IsVisible && WindowState == WindowState.Normal) ConstrainToWorkArea(false);
        }));
    }

    internal void ConstrainToWorkArea(bool snapToEdges)
    {
        if (closed || WindowState != WindowState.Normal) return;
        try
        {
            DesktopWindowPlacement.Constrain(new WindowInteropHelper(this).Handle, snapToEdges);
            MovementStatus.Text = "Drag to move · Arrow keys when focused";
        }
        catch (Win32Exception)
        {
            MovementStatus.Text = "Could not adjust to this display. Try moving the companion again.";
        }
    }

    private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragSurface.Focus();
        dragStart = e.GetPosition(this);
        DragSurface.CaptureMouse();
        e.Handled = true;
    }

    private void DragSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragStart = null;
        DragSurface.ReleaseMouseCapture();
        try { DragMove(); }
        catch (InvalidOperationException) when (Mouse.LeftButton != MouseButtonState.Pressed) { }
        finally
        {
            if (!closed) { ConstrainToWorkArea(true); CommitPlacement(); }
        }
        e.Handled = true;
    }

    private void DragSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        dragStart = null;
        DragSurface.ReleaseMouseCapture();
    }

    private void DragSurface_LostMouseCapture(object sender, MouseEventArgs e) => dragStart = null;

    private void DragSurface_KeyDown(object sender, KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 40 : 10;
        var (dx, dy) = e.Key switch
        {
            Key.Left => (-step, 0), Key.Right => (step, 0),
            Key.Up => (0, -step), Key.Down => (0, step), _ => (0, 0)
        };
        if (dx == 0 && dy == 0) return;
        try
        {
            DesktopWindowPlacement.MoveBy(new WindowInteropHelper(this).Handle, dx, dy);
            CommitPlacement();
        }
        catch (Win32Exception) { MovementStatus.Text = "Could not move the companion on this display."; }
        e.Handled = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void Hide_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);
    private void Sleep_Click(object sender, RoutedEventArgs e) => ToggleSleep();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
