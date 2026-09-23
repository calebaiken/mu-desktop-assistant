using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.Configuration;
using Companion.Application.Diagnostics;
using Companion.Core.Desktop;
using Companion.Infrastructure.Lifetime;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;

namespace Companion.Desktop;

public partial class App : System.Windows.Application
{
    private CompanionRuntime? runtime;
    private CompanionWindow? companion;
    private SettingsWindow? settingsWindow;
    private ChatWindow? chatWindow;
    private LocalModelHost? localModelHost;
    private StartupOptions? options;
    private DesktopIntegration? desktop;
    private PlacementSession? placement;
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
                if (!activated && !options.IsTestSession)
                    MessageBox.Show("The companion is already running but could not be reached. Try again shortly.", "Desktop Companion");
                Shutdown(activated ? 0 : 3);
                return;
            }
            runtime = new(options.DataDirectory, instance);
            await runtime.InitializeAsync();
            companion = new();
            companion.Apply(runtime.Settings);
            if (options.IsTestSession) companion.ShowActivated = false;
            MainWindow = companion;
            companion.SettingsRequested += (_, _) => OpenSettings();
            companion.ChatRequested += (_, _) => OpenChat();
            companion.SleepChanged += (_, _) =>
            {
                if (companion.CurrentState == AvatarState.Sleeping) chatWindow?.StopGeneration();
            };
            companion.Closing += (_, closing) =>
            {
                if (stopping) return;
                closing.Cancel = true;
                _ = StopAsync(0);
            };
            instance.StartListening(() => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (stopping) return;
                if (desktop is not null) desktop.Recover();
                else { companion.Show(); companion.WindowState = WindowState.Normal; companion.Activate(); }
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
            if (options.LocalModelSmoke)
            {
                await LocalModelSmokeChecks.RunAsync(options.LocalModelDirectory!, options.DataDirectory);
                await StopAsync(0);
                return;
            }
            if (options.PrivateHistorySmoke)
            {
                await TemporaryPrivacySmokeChecks.WaitForPrivacyTerminationAsync(options.DataDirectory);
                return;
            }
            companion.Show();
            placement = new(companion, options.DataDirectory);
            await placement.InitializeAsync();
            if (stopping) return;
            // Install tray actions only after initial placement is settled and interaction resumes.
            if (!options.IsTestSession)
            {
                desktop = new(companion, OpenSettings, companion.ToggleSleep, () => companion.Close(),
                    companion.ReportDesktopStatus, () =>
                    {
                        companion.ConstrainToWorkArea(false);
                        settingsWindow?.RefreshInteraction();
                    }, openChat: OpenChat);
                if (!desktop.TryRegisterHotkey(runtime.Settings.SummonShortcut, out var error))
                    companion.ReportShortcutStatus(error);
                companion.HideRequested += (_, _) =>
                {
                    if (desktop.RecoveryAvailable) desktop.Hide();
                    else companion.ReportDesktopStatus("Choose an available summon shortcut in Settings before hiding the companion.");
                };
                companion.SleepChanged += (_, _) => desktop.SetSleeping(companion.CurrentState == AvatarState.Sleeping);
            }
            if (options.PerformanceSmoke)
            {
                await PerformanceSmokeChecks.RunAsync(companion, runtime.Settings, options.DataDirectory, options.Observer, runtime.Lifetime.Token);
                await StopAsync(0);
                return;
            }
            if (options.UiSmoke)
            {
                var smokeTime = Stopwatch.StartNew();
                async Task RecordStage(string stage) => await File.AppendAllTextAsync(
                    Path.Combine(options.DataDirectory, "ui-stages.txt"), $"{stage}: {smokeTime.Elapsed.TotalSeconds:F3}s{Environment.NewLine}");
                await RecordStage("start");
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (new WindowInteropHelper(companion).Handle == IntPtr.Zero || !companion.IsVisible)
                    throw new InvalidOperationException("Presentation source unavailable.");
                await CompactAvatarSmokeChecks.RunAsync(companion, runtime.Settings, options.DataDirectory);
                await RecordStage("compact-avatar-complete");
                await AppearanceSmokeChecks.RunAsync(companion, runtime.Settings, options.DataDirectory);
                await RecordStage("appearance-complete");
                await DesktopIntegrationSmokeChecks.RunAsync(options.DataDirectory);
                await RecordStage("integration-complete");
                await SettingsLifecycleSmokeChecks.RunAsync(runtime, options.DataDirectory);
                await RecordStage("settings-lifecycle-complete");
                await PlacementInitializationSmokeChecks.RunAsync(options.DataDirectory);
                await RecordStage("placement-initialization-complete");
                await ChatLifecycleSmokeChecks.RunAsync(options.DataDirectory);
                await RecordStage("chat-lifecycle-complete");
                await ChatHistorySmokeChecks.RunAsync(options.DataDirectory);
                await RecordStage("chat-history-complete");
                await SafeChatDocumentSmokeChecks.RunAsync(options.DataDirectory);
                await RecordStage("safe-chat-document-complete");
                await CompanionReplySmokeChecks.RunAsync(companion, runtime.Settings, options.DataDirectory);
                await RecordStage("companion-reply-complete");
                OpenSettings();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (settingsWindow is null || new WindowInteropHelper(settingsWindow).Handle == IntPtr.Zero)
                    throw new InvalidOperationException("Settings presentation source unavailable.");
                AppearanceSmokeChecks.CheckSettingsLayout(settingsWindow);
                var existingSettings = settingsWindow;
                existingSettings.Hide();
                OpenSettings();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (!ReferenceEquals(settingsWindow, existingSettings) || !existingSettings.IsVisible)
                    throw new InvalidOperationException("Hidden settings could not be recovered.");
                existingSettings.WindowState = WindowState.Minimized;
                OpenSettings();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (!existingSettings.IsVisible || existingSettings.WindowState != WindowState.Normal)
                    throw new InvalidOperationException("Minimized settings could not be recovered.");
                await RecordStage("settings-recovery-complete");
                await RecordStage("settings-complete");
                settingsWindow.Close();
                companion.Close();
            }
            else if (runtime.LoadResult.State is not (SettingsLoadState.New or SettingsLoadState.Loaded) || runtime.Log.RecoveredCorruptLogs > 0)
                OpenSettings();
        }
        catch (Exception)
        {
            if (options is { IsTestSession: false })
                MessageBox.Show("The companion could not start. Check access and available space in its data folder. No settings were overwritten.",
                    "Desktop Companion", MessageBoxButton.OK, MessageBoxImage.Error);
            await StopAsync(1);
        }
    }

    private void OpenSettings()
    {
        if (stopping || runtime is null || companion is null) return;
        if (options?.PerformanceSmoke == true) return;
        desktop?.Recover();
        if (settingsWindow is not null)
        {
            settingsWindow.Show();
            settingsWindow.WindowState = WindowState.Normal;
            settingsWindow.Activate();
            return;
        }
        settingsWindow = new(runtime, companion, desktop, options is { IsTestSession: false, IsDefaultProfile: true }) { Owner = companion };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    private void OpenChat()
    {
        if (stopping || companion is null || options is null || options.IsTestSession) return;
        companion.DismissReplyPreview();
        desktop?.Recover();
        if (chatWindow is not null)
        {
            chatWindow.Show();
            chatWindow.WindowState = WindowState.Normal;
            chatWindow.Activate();
            return;
        }
        LocalModelHost? host = null;
        try
        {
            if (options.LocalModelDirectory is { } directory) host = new LocalModelHost(directory);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Optional model setup must never prevent use of the companion itself.
        }
        localModelHost = host;
        chatWindow = new ChatWindow(token => host is null
            ? Task.FromException<IChatModel>(new ChatModelException(ChatFailure.Unavailable))
            : host.GetModelAsync(token), new SqliteConversationStore(options.DataDirectory)) { Owner = companion };
        var window = chatWindow;
        window.ReplyCompleted += reply =>
        {
            if (!stopping && !window.IsActive && companion.IsVisible)
                companion.ShowReplyPreview(reply);
        };
        window.Activated += (_, _) => companion.DismissReplyPreview();
        IDisposable? thinking = null;
        window.GenerationChanged += (_, _) =>
        {
            thinking?.Dispose();
            thinking = null;
            if (window.IsGenerating && !stopping)
            {
                if (companion.CurrentState == AvatarState.Sleeping) companion.ToggleSleep();
                companion.EndPreview();
                thinking = companion.BeginActivity(AvatarState.Thinking);
            }
        };
        chatWindow.Closed += (_, _) =>
        {
            companion.DismissReplyPreview();
            thinking?.Dispose();
            chatWindow = null;
            if (ReferenceEquals(localModelHost, host)) localModelHost = null;
            if (host is not null) _ = CloseModelAsync(host);
        };
        chatWindow.Show();
    }

    private static async Task CloseModelAsync(LocalModelHost host)
    {
        try { await host.DisposeAsync(); }
        catch (Exception) { /* Worker ownership cleanup is best effort; no content enters logs. */ }
    }

    private async Task ObserveListenerAsync(SingleInstanceCoordinator instance)
    {
        try { await instance.Completion; }
        catch (Exception)
        {
            if (!stopping)
            {
                if (options is { IsTestSession: false })
                    MessageBox.Show("The companion's activation service stopped. Please restart the companion.", "Desktop Companion");
                await StopAsync(1);
            }
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (options is { IsTestSession: false })
            MessageBox.Show("An unexpected error occurred. The companion will close; saved settings are preserved.", "Desktop Companion");
        _ = StopAsync(1);
    }

    private async Task StopAsync(int exitCode)
    {
        if (stopping) return;
        stopping = true;
        if (companion is not null) companion.IsEnabled = false;
        settingsWindow?.BeginShutdown();
        chatWindow?.BeginShutdown();
        if (localModelHost is not null)
        {
            try { await localModelHost.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { exitCode = 1; }
        }
        try { desktop?.Dispose(); }
        catch (Exception) { exitCode = 1; }
        if (placement is not null)
        {
            try { await placement.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception) { exitCode = 1; }
        }
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
