using System.Windows;
using Companion.Core.Configuration;

namespace Companion.Desktop;

public partial class CompanionWindow : Window
{
    public event EventHandler? SettingsRequested;
    public CompanionWindow() => InitializeComponent();
    public void Apply(CompanionSettings settings)
    {
        NameLabel.Text = settings.CompanionName;
        Title = settings.CompanionName + " · Desktop Companion";
        Topmost = settings.AlwaysOnTop;
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
