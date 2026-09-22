using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Debounces explicit moves; preferences have an entirely separate save path.</summary>
internal sealed class PlacementSession : IAsyncDisposable
{
    private readonly CompanionWindow window;
    private readonly JsonPlacementStore store;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly SemaphoreSlim saving = new(1);
    private bool writable;
    private bool stopped;
    private SavedPlacement? pending;
    private Task? initialization;

    internal PlacementSession(CompanionWindow window, string directory)
    {
        this.window = window;
        store = new(directory);
        timer.Tick += async (_, _) => { timer.Stop(); await SaveAsync(); };
        window.PlacementChanged += OnMoved;
    }

    internal Task InitializeAsync() => initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        try
        {
            var loaded = await store.LoadAsync(CancellationToken.None);
            if (stopped) return;
            writable = loaded.CanSave;
            if (loaded.Placement is { } placement)
            {
                var handle = new WindowInteropHelper(window).Handle;
                DesktopWindowPlacement.BeginRestore(handle, placement);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (stopped) return;
                DesktopWindowPlacement.CompleteRestore(handle, placement);
            }
            if (loaded.State is not (PlacementLoadState.New or PlacementLoadState.Loaded))
                window.ReportDesktopStatus(writable ? "The saved position was damaged. Move the companion to save a new position." :
                    "The saved position is from another version or is too large. It has been preserved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        { window.ReportDesktopStatus("The saved position could not be loaded. You can still move the companion."); }
    }

    private void OnMoved(object? sender, EventArgs e)
    {
        if (stopped || !writable) return;
        if (!window.IsVisible || window.WindowState != WindowState.Normal) return;
        try { pending = DesktopWindowPlacement.Capture(new WindowInteropHelper(window).Handle); }
        catch (Win32Exception)
        {
            window.ReportDesktopStatus("The position could not be read. Try moving the companion again.");
            return;
        }
        timer.Stop();
        timer.Start();
    }

    private async Task SaveAsync()
    {
        if (!writable || pending is not { } placement) return;
        // Persist the last normal-state move even if Hide/minimize happens before the timer.
        try
        {
            await saving.WaitAsync();
            try
            {
                await store.SaveAsync(placement, CancellationToken.None);
                if (ReferenceEquals(pending, placement)) pending = null;
            }
            finally { saving.Release(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { window.ReportDesktopStatus("The position could not be saved. Your previous saved position is preserved."); }
    }

    public async ValueTask DisposeAsync()
    {
        if (stopped) return;
        stopped = true;
        timer.Stop();
        window.PlacementChanged -= OnMoved;
        if (initialization is not null) await initialization;
        await SaveAsync();
        await saving.WaitAsync();
        store.Dispose();
        saving.Release();
        saving.Dispose();
    }
}
