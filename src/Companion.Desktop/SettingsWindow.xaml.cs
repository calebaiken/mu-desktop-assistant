using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

public partial class SettingsWindow : Window
{
    private readonly CompanionRuntime runtime;
    private readonly CompanionWindow companion;
    private readonly DesktopIntegration? desktop;
    private readonly Func<CompanionSettings, Task<bool>> saveOperation;
    private bool startupWasEnabled;
    private bool saving;
    private bool displayFitQueued;
    private bool closed;
    private bool shuttingDown;
    private bool appearanceReady;
    private HwndSource? source;

    internal SettingsWindow(CompanionRuntime runtime, CompanionWindow companion, DesktopIntegration? desktop = null,
        bool allowStartup = false, Func<CompanionSettings, Task<bool>>? saveOperation = null)
    {
        InitializeComponent();
        this.runtime = runtime;
        this.companion = companion;
        this.desktop = desktop;
        this.saveOperation = saveOperation ?? runtime.SaveAsync;
        AvatarPresetInput.ItemsSource = new[] { new PresetOption(AvatarPreset.Mu, "Muon"), new PresetOption(AvatarPreset.Classic, "Classic") };
        AvatarAccentInput.ItemsSource = Enum.GetValues<AvatarAccent>().Select(value => new AccentOption(value, value.ToString()));
        AvatarDecorationInput.ItemsSource = new[]
        {
            new DecorationOption(AvatarDecoration.HaloAndTrail, "Halo and trail"),
            new DecorationOption(AvatarDecoration.TrailOnly, "Trail only"),
            new DecorationOption(AvatarDecoration.None, "None")
        };
        AvatarFaceInput.ItemsSource = Enum.GetValues<AvatarFace>().Select(value => new FaceOption(value, value.ToString()));
        AvatarPresetInput.SelectedValue = runtime.Settings.AvatarPreset;
        AvatarAccentInput.SelectedValue = runtime.Settings.AvatarAccent;
        AvatarDecorationInput.SelectedValue = runtime.Settings.AvatarDecoration;
        AvatarFaceInput.SelectedValue = runtime.Settings.AvatarFace;
        appearanceReady = true;
        UpdateAppearancePreview();
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
        SystemParameters.StaticPropertyChanged += AppearanceSystemChanged;
        Closed += (_, _) =>
        {
            closed = true;
            SystemParameters.StaticPropertyChanged -= AppearanceSystemChanged;
            AppearancePreview.Dispose();
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

    internal void BeginShutdown()
    {
        Dispatcher.VerifyAccess();
        shuttingDown = true;
        if (closed) return;
        IsEnabled = false;
        SaveButton.IsEnabled = false;
        AppearancePreview.Stop();
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveChangesAsync();

    internal async Task SaveChangesAsync()
    {
        Dispatcher.VerifyAccess();
        if (saving || shuttingDown || closed || !runtime.LoadResult.CanSave) return;
        var settings = runtime.Settings with
        {
            CompanionName = NameInput.Text.Trim(),
            AlwaysOnTop = TopmostInput.IsChecked == true,
            AvatarScale = ScaleInput.Value,
            AvatarOpacity = OpacityInput.Value,
            AvatarPreset = AvatarPresetInput.SelectedValue is AvatarPreset preset ? preset : runtime.Settings.AvatarPreset,
            AvatarAccent = AvatarAccentInput.SelectedValue is AvatarAccent accent ? accent : runtime.Settings.AvatarAccent,
            AvatarDecoration = AvatarDecorationInput.SelectedValue is AvatarDecoration decoration ? decoration : runtime.Settings.AvatarDecoration,
            AvatarFace = AvatarFaceInput.SelectedValue is AvatarFace face ? face : runtime.Settings.AvatarFace,
            ReducedMotion = ReducedMotionInput.IsChecked == true,
            DiagnosticsEnabled = DiagnosticsInput.IsChecked == true,
            SummonShortcut = ShortcutInput.SelectedValue is SummonHotkey shortcut ? shortcut : runtime.Settings.SummonShortcut
        };
        var errors = settings.Validate();
        if (errors.Count != 0) { StatusLabel.Text = errors[0]; NameInput.Focus(); return; }
        // Native preference requests belong to the same Save action as the captured settings.
        var requestedClickThrough = ClickThroughInput.IsChecked == true;
        var requestedStartup = StartupInput.IsChecked == true;
        var changeStartup = StartupInput.IsEnabled && requestedStartup != startupWasEnabled;
        saving = true;
        SaveButton.IsEnabled = false;
        try
        {
            var logged = await saveOperation(settings);
            // Runtime serialization owns the file commit. This window separately owns the later
            // UI/native effects, which must not run after App begins disposing their resources.
            if (shuttingDown || closed) return;
            companion.Apply(settings);
            StatusLabel.Text = logged ? "Saved." : "Settings saved, but the diagnostic log could not be written.";
            if (desktop is not null)
            {
                if (!desktop.TryRegisterHotkey(settings.SummonShortcut, out var shortcutError))
                    StatusLabel.Text += " " + shortcutError;
                companion.ReportShortcutStatus(shortcutError);
                ClickThroughInput.IsEnabled = desktop.RecoveryAvailable;
                try { desktop.SetClickThrough(requestedClickThrough); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    ClickThroughInput.IsChecked = desktop.IsClickThrough;
                    StatusLabel.Text += " Mouse interaction could not be changed. Use your summon shortcut to recover.";
                }
            }
            if (changeStartup)
            {
                try
                {
                    StartupRegistration.SetEnabled(requestedStartup, Environment.ProcessPath!);
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
        catch (OperationCanceledException)
        { if (!shuttingDown && !closed) StatusLabel.Text = "Save cancelled before completion."; }
        catch (IOException)
        { if (!shuttingDown && !closed) StatusLabel.Text = "Could not save. Check available disk space and try again. Your previous settings are preserved."; }
        catch (UnauthorizedAccessException)
        { if (!shuttingDown && !closed) StatusLabel.Text = "Could not save. Check access to your companion data folder."; }
        catch (InvalidOperationException)
        { if (!shuttingDown && !closed) StatusLabel.Text = "The settings file changed outside this window. Restart the companion before saving."; }
        finally
        {
            saving = false;
            if (!shuttingDown && !closed) SaveButton.IsEnabled = runtime.LoadResult.CanSave;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    internal void RefreshInteraction()
    {
        if (shuttingDown || closed) return;
        ClickThroughInput.IsEnabled = desktop?.RecoveryAvailable == true;
        ClickThroughInput.IsChecked = desktop?.IsClickThrough == true;
    }
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (PreviewInput.SelectedItem is AvatarState state) companion.Preview(state);
    }
    private void AvatarAppearance_Changed(object sender, SelectionChangedEventArgs e) => UpdateAppearancePreview();
    internal void AppearanceSystemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.HighContrast) || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(UpdateAppearancePreview));
    }
    private void UpdateAppearancePreview()
    {
        if (!appearanceReady || closed || shuttingDown) return;
        if (AvatarPresetInput.SelectedValue is not AvatarPreset preset || AvatarAccentInput.SelectedValue is not AvatarAccent accent ||
            AvatarDecorationInput.SelectedValue is not AvatarDecoration decoration || AvatarFaceInput.SelectedValue is not AvatarFace face) return;
        AvatarDecorationInput.IsEnabled = preset == AvatarPreset.Mu;
        AppearancePreview.ApplyAppearance(preset, accent, decoration, face);
        AppearancePreview.Render(AvatarState.Idle, false, SystemParameters.HighContrast);
    }
    private sealed record PresetOption(AvatarPreset Value, string Label);
    private sealed record AccentOption(AvatarAccent Value, string Label);
    private sealed record DecorationOption(AvatarDecoration Value, string Label);
    private sealed record FaceOption(AvatarFace Value, string Label);
    private sealed record ShortcutOption(SummonHotkey Value, string Label);
}
