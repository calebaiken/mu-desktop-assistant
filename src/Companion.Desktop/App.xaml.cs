using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.Configuration;
using Companion.Application.Diagnostics;
using Companion.Infrastructure.Lifetime;

namespace Companion.Desktop;

public partial class App : System.Windows.Application
{
    private CompanionRuntime? runtime;
    private CompanionWindow? companion;
    private SettingsWindow? settingsWindow;
    private StartupOptions? options;
    private bool stopping;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { options = StartupOptions.Parse(e.Args); }
        catch (ArgumentException) { Shutdown(2); return; }
        DispatcherUnhandledException += OnUnhandledException;
        try
        {
            var instance = SingleInstanceCoordinator.TryAcquire(options.DataDirectory);
            if (instance is null)
            {
                var activated = await SingleInstanceCoordinator.ActivateExistingAsync(options.DataDirectory, CancellationToken.None);
                if (!activated && !options.Smoke && !options.UiSmoke)
                    MessageBox.Show("The companion is already running but could not be reached. Try again shortly.", "Desktop Companion");
                Shutdown(activated ? 0 : 3);
                return;
            }
            runtime = new(options.DataDirectory, instance);
            await runtime.InitializeAsync();
            companion = new();
            companion.Apply(runtime.Settings);
            MainWindow = companion;
            companion.SettingsRequested += (_, _) => OpenSettings();
            companion.Closing += (_, closing) =>
            {
                if (stopping) return;
                closing.Cancel = true;
                _ = StopAsync(0);
            };
            instance.StartListening(() => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (stopping) return;
                companion.Show();
                companion.WindowState = WindowState.Normal;
                companion.Activate();
            })));
            _ = ObserveListenerAsync(instance);
            if (options.Smoke)
            {
                var content = (FrameworkElement)companion.Content;
                content.Measure(new Size(companion.Width, companion.Height));
                content.Arrange(new Rect(0, 0, companion.Width, companion.Height));
                await StopAsync(content.DesiredSize.Width > 0 && content.DesiredSize.Height > 0 ? 0 : 1);
                return;
            }
            companion.Show();
            if (options.UiSmoke)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (new WindowInteropHelper(companion).Handle == IntPtr.Zero || !companion.IsVisible)
                    throw new InvalidOperationException("Presentation source unavailable.");
                OpenSettings();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (settingsWindow is null || new WindowInteropHelper(settingsWindow).Handle == IntPtr.Zero)
                    throw new InvalidOperationException("Settings presentation source unavailable.");
                settingsWindow.Close();
                companion.Close();
            }
            else if (runtime.LoadResult.State is not (SettingsLoadState.New or SettingsLoadState.Loaded) || runtime.Log.RecoveredCorruptLogs > 0)
                OpenSettings();
        }
        catch (Exception)
        {
            if (options is { Smoke: false, UiSmoke: false })
                MessageBox.Show("The companion could not start. Check access and available space in its data folder. No settings were overwritten.",
                    "Desktop Companion", MessageBoxButton.OK, MessageBoxImage.Error);
            await StopAsync(1);
        }
    }

    private void OpenSettings()
    {
        if (stopping || runtime is null || companion is null) return;
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        settingsWindow = new(runtime, companion) { Owner = companion };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    private async Task ObserveListenerAsync(SingleInstanceCoordinator instance)
    {
        try { await instance.Completion; }
        catch (Exception)
        {
            if (!stopping)
            {
                if (options is { Smoke: false, UiSmoke: false })
                    MessageBox.Show("The companion's activation service stopped. Please restart the companion.", "Desktop Companion");
                await StopAsync(1);
            }
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (options is { Smoke: false, UiSmoke: false })
            MessageBox.Show("An unexpected error occurred. The companion will close; saved settings are preserved.", "Desktop Companion");
        _ = StopAsync(1);
    }

    private async Task StopAsync(int exitCode)
    {
        if (stopping) return;
        stopping = true;
        if (companion is not null) companion.IsEnabled = false;
        if (settingsWindow is not null) settingsWindow.IsEnabled = false;
        if (runtime is not null)
        {
            try
            {
                await runtime.Log.WriteAsync(new(EventArea.Application, EventCode.Stopping, Guid.NewGuid()), CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception) { exitCode = 1; }
            try { await runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { exitCode = 1; }
        }
        Shutdown(exitCode);
    }
}
