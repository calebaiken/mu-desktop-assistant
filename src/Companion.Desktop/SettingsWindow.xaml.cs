using System.IO;
using System.Windows;
using Companion.Application.Configuration;

namespace Companion.Desktop;

public partial class SettingsWindow : Window
{
    private readonly CompanionRuntime runtime;
    private readonly CompanionWindow companion;
    private bool saving;

    internal SettingsWindow(CompanionRuntime runtime, CompanionWindow companion)
    {
        InitializeComponent();
        this.runtime = runtime;
        this.companion = companion;
        NameInput.Text = runtime.Settings.CompanionName;
        TopmostInput.IsChecked = runtime.Settings.AlwaysOnTop;
        DiagnosticsInput.IsChecked = runtime.Settings.DiagnosticsEnabled;
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
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (saving) return;
        var settings = runtime.Settings with
        {
            CompanionName = NameInput.Text.Trim(),
            AlwaysOnTop = TopmostInput.IsChecked == true,
            DiagnosticsEnabled = DiagnosticsInput.IsChecked == true
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
        }
        catch (OperationCanceledException) { StatusLabel.Text = "Save cancelled before completion."; }
        catch (IOException) { StatusLabel.Text = "Could not save. Check available disk space and try again. Your previous settings are preserved."; }
        catch (UnauthorizedAccessException) { StatusLabel.Text = "Could not save. Check access to your companion data folder."; }
        catch (InvalidOperationException) { StatusLabel.Text = "The settings file changed outside this window. Restart the companion before saving."; }
        finally { saving = false; SaveButton.IsEnabled = runtime.LoadResult.CanSave; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
