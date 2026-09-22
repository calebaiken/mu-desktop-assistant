using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Companion.Application.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

public partial class SettingsWindow : Window
{
    private readonly CompanionRuntime runtime;
    private readonly CompanionWindow companion;
    private readonly DesktopIntegration? desktop;
    private bool startupWasEnabled;
    private bool saving;
    private bool displayFitQueued;
    private bool closed;
    private HwndSource? source;

    internal SettingsWindow(CompanionRuntime runtime, CompanionWindow companion, DesktopIntegration? desktop = null, bool allowStartup = false)
    {
        InitializeComponent();
        this.runtime = runtime;
        this.companion = companion;
        this.desktop = desktop;
        NameInput.Text = runtime.Settings.CompanionName;
        TopmostInput.IsChecked = runtime.Settings.AlwaysOnTop;
        ScaleInput.Value = runtime.Settings.AvatarScale;
        OpacityInput.Value = runtime.Settings.AvatarOpacity;
        ReducedMotionInput.IsChecked = runtime.Settings.ReducedMotion;
        DiagnosticsInput.IsChecked = runtime.Settings.DiagnosticsEnabled;
        ShortcutInput.ItemsSource = Enum.GetValues<SummonHotkey>().Select(value => new ShortcutOption(value, DesktopIntegration.HotkeyLabel(value)));
        ShortcutInput.DisplayMemberPath = nameof(ShortcutOption.Label);
        ShortcutInput.SelectedValuePath = nameof(ShortcutOption.Value);
        ShortcutInput.SelectedValue = runtime.Settings.SummonShortcut;
        ClickThroughInput.IsEnabled = desktop?.RecoveryAvailable == true;
        ClickThroughInput.IsChecked = desktop?.IsClickThrough == true;
        PreviewInput.ItemsSource = Enum.GetValues<AvatarState>();
        PreviewInput.SelectedItem = AvatarState.Idle;
        StartupInput.IsEnabled = allowStartup;
        StartupStatus.Text = allowStartup ? "Uses this application's current location. Save after changing this option." :
            "Startup is available when using the default profile outside a test session.";
        if (allowStartup)
        {
            try { StartupInput.IsChecked = startupWasEnabled = StartupRegistration.IsEnabled(Environment.ProcessPath!); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                StartupInput.IsEnabled = false;
                StartupStatus.Text = "Windows startup settings could not be read. Other preferences can still be saved.";
            }
        }
        SaveButton.IsEnabled = runtime.LoadResult.CanSave;
        StatusLabel.Text = runtime.LoadResult.State switch
        {
            SettingsLoadState.RecoveredBackup => "Your settings file was damaged. The last valid backup is loaded. Save to repair it.",
            SettingsLoadState.ResetInvalid => "Settings could not be read. Safe defaults are loaded; saving preserves the damaged file for recovery.",
            SettingsLoadState.UnsupportedVersion => "These settings belong to a different application version. Saving is disabled to protect them.",
            _ when runtime.Log.RecoveredCorruptLogs > 0 => "A damaged diagnostic log was reset. Its previous file is preserved for recovery.",
            _ => "Changes take effect when you save."
        };
        Closing += (_, e) => { if (saving) e.Cancel = true; };
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessage);
        };
        Loaded += (_, _) => QueueDisplayFit();
        DpiChanged += (_, _) => QueueDisplayFit();
        StateChanged += (_, _) => QueueDisplayFit();
        Closed += (_, _) =>
        {
            closed = true;
            companion.EndPreview();
            if (source is { IsDisposed: false }) source.RemoveHook(WindowMessage);
        };
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x001A) QueueDisplayFit();
        return 0;
    }

    private void QueueDisplayFit()
    {
        if (closed || !IsLoaded || WindowState != WindowState.Normal || displayFitQueued) return;
        displayFitQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            displayFitQueued = false;
            if (closed || WindowState != WindowState.Normal) return;
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                var (_, workArea) = DesktopWindowPlacement.ReadBounds(handle);
                var dpi = VisualTreeHelper.GetDpi(this);
                MaxWidth = workArea.Width / dpi.DpiScaleX;
                MaxHeight = workArea.Height / dpi.DpiScaleY;
                MinWidth = Math.Min(440, MaxWidth);
                MinHeight = Math.Min(420, MaxHeight);
                Width = Math.Min(Width, MaxWidth);
                Height = Math.Min(Height, MaxHeight);
                UpdateLayout();
                DesktopWindowPlacement.Constrain(handle, false);
            }
            catch (Win32Exception)
            {
                StatusLabel.Text = "Could not fit settings to this display. Move or resize this window to reach its controls.";
            }
        }));
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (saving) return;
        var settings = runtime.Settings with
        {
            CompanionName = NameInput.Text.Trim(),
            AlwaysOnTop = TopmostInput.IsChecked == true,
            AvatarScale = ScaleInput.Value,
            AvatarOpacity = OpacityInput.Value,
            ReducedMotion = ReducedMotionInput.IsChecked == true,
            DiagnosticsEnabled = DiagnosticsInput.IsChecked == true,
            SummonShortcut = ShortcutInput.SelectedValue is SummonHotkey shortcut ? shortcut : runtime.Settings.SummonShortcut
        };
        var errors = settings.Validate();
        if (errors.Count != 0) { StatusLabel.Text = errors[0]; NameInput.Focus(); return; }
        saving = true;
        SaveButton.IsEnabled = false;
        try
        {
            var logged = await runtime.SaveAsync(settings);
            companion.Apply(settings);
            StatusLabel.Text = logged ? "Saved." : "Settings saved, but the diagnostic log could not be written.";
            if (desktop is not null)
            {
                if (!desktop.TryRegisterHotkey(settings.SummonShortcut, out var shortcutError))
                    StatusLabel.Text += " " + shortcutError;
                companion.ReportShortcutStatus(shortcutError);
                try { desktop.SetClickThrough(ClickThroughInput.IsChecked == true); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    ClickThroughInput.IsChecked = desktop.IsClickThrough;
                    StatusLabel.Text += " Mouse interaction could not be changed. Use Show in the tray to recover.";
                }
            }
            if (StartupInput.IsEnabled && StartupInput.IsChecked != startupWasEnabled)
            {
                try
                {
                    StartupRegistration.SetEnabled(StartupInput.IsChecked == true, Environment.ProcessPath!);
                    startupWasEnabled = StartupRegistration.IsEnabled(Environment.ProcessPath!);
                    StartupInput.IsChecked = startupWasEnabled;
                    StartupStatus.Text = startupWasEnabled ? "Startup is enabled for this application location." : "Startup is off.";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                {
                    StatusLabel.Text += " Other settings saved, but Windows startup could not be changed. Check the option and try again.";
                }
            }
        }
        catch (OperationCanceledException) { StatusLabel.Text = "Save cancelled before completion."; }
        catch (IOException) { StatusLabel.Text = "Could not save. Check available disk space and try again. Your previous settings are preserved."; }
        catch (UnauthorizedAccessException) { StatusLabel.Text = "Could not save. Check access to your companion data folder."; }
        catch (InvalidOperationException) { StatusLabel.Text = "The settings file changed outside this window. Restart the companion before saving."; }
        finally { saving = false; SaveButton.IsEnabled = runtime.LoadResult.CanSave; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    internal void RefreshInteraction() => ClickThroughInput.IsChecked = desktop?.IsClickThrough == true;
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (PreviewInput.SelectedItem is AvatarState state) companion.Preview(state);
    }
    private sealed record ShortcutOption(SummonHotkey Value, string Label);
}
