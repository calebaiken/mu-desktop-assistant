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
    private readonly Func<Task>? beforeLoadAsync;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly SemaphoreSlim saving = new(1);
    private bool writable;
    private bool stopped;
    private bool closed;
    private SavedPlacement? pending;
    private Task? initialization;

    internal PlacementSession(CompanionWindow window, string directory, Func<Task>? beforeLoadAsync = null)
    {
        this.window = window;
        store = new(directory);
        this.beforeLoadAsync = beforeLoadAsync;
        timer.Tick += async (_, _) => { timer.Stop(); await SaveAsync(); };
        window.PlacementChanged += OnMoved;
        window.Closed += OnClosed;
    }

    internal Task InitializeAsync()
    {
        window.Dispatcher.VerifyAccess();
        return initialization ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        if (stopped || closed) return;
        var wasEnabled = window.IsEnabled;
        // Disable before the first await, so input cannot race loading an older position.
        // SetCurrentValue preserves a potential binding/style as well as prior disabled state.
        window.SetCurrentValue(UIElement.IsEnabledProperty, false);
        try
        {
            if (beforeLoadAsync is not null) await beforeLoadAsync();
            if (stopped || closed) return;
            var loaded = await store.LoadAsync(CancellationToken.None);
            if (stopped || closed) return;
            writable = loaded.CanSave;
            if (loaded.Placement is { } placement && CanRestore())
            {
                var handle = new WindowInteropHelper(window).Handle;
                DesktopWindowPlacement.BeginRestore(handle, placement);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (CanRestore()) DesktopWindowPlacement.CompleteRestore(handle, placement);
            }
            if (!stopped && !closed && loaded.State is not (PlacementLoadState.New or PlacementLoadState.Loaded))
                window.ReportDesktopStatus(writable ? "The saved position was damaged. Move the companion to save a new position." :
                    "The saved position is from another version or is too large. It has been preserved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            if (!stopped && !closed)
                window.ReportDesktopStatus("The saved position could not be loaded. You can still move the companion.");
        }
        finally
        {
            // Shutdown or Closed owns the final disabled state. A late load must not undo it.
            if (!stopped && !closed) window.SetCurrentValue(UIElement.IsEnabledProperty, wasEnabled);
        }
    }

    private bool CanRestore() => !stopped && !closed && window.IsVisible &&
        window.WindowState == WindowState.Normal && new WindowInteropHelper(window).Handle != 0;

    private void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        timer.Stop();
    }

    private void OnMoved(object? sender, EventArgs e)
    {
        if (stopped || closed || !writable) return;
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
        window.Closed -= OnClosed;
        if (initialization is not null) await initialization;
        await SaveAsync();
        await saving.WaitAsync();
        store.Dispose();
        saving.Release();
        saving.Dispose();
    }
}
