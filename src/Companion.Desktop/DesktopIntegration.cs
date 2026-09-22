using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Companion.Core.Desktop;
using Forms = System.Windows.Forms;

namespace Companion.Desktop;

/// <summary>Owns the companion's tray, recovery shortcut, and native click-through state.</summary>
public sealed class DesktopIntegration : IDisposable
{
    private const int ExtendedStyle = -20;
    private const long TransparentStyle = 0x00000020;
    private const long LayeredStyle = 0x00080000;
    private const int HotkeyMessage = 0x0312;
    private readonly Window window;
    private readonly Action<string> reportError;
    private readonly Action? recovered;
    private readonly bool enabled;
    private readonly nint handle;
    private readonly HwndSource source;
    private Forms.NotifyIcon? tray;
    private Forms.ContextMenuStrip? menu;
    private Forms.ToolStripMenuItem? sleepItem;
    private System.Drawing.Icon? icon;
    private int? hotkeyId;
    private readonly HashSet<int> ownedHotkeyIds = [];
    private bool disposed;

    public DesktopIntegration(Window window, Action openSettings, Action toggleSleep, Action exit,
        Action<string> reportError, Action? recovered = null, bool enableSystemIntegration = true)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(toggleSleep);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(reportError);
        window.Dispatcher.VerifyAccess();
        this.window = window;
        this.reportError = reportError;
        this.recovered = recovered;
        enabled = enableSystemIntegration;
        handle = new WindowInteropHelper(window).EnsureHandle();
        source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("The companion window is unavailable.");
        source.AddHook(WindowMessage);
        window.Closed += WindowClosed;
        if (!enabled) return;
        try
        {
            menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show / recover companion", null, (_, _) => Invoke(Recover));
            menu.Items.Add("Hide companion", null, (_, _) => Invoke(Hide));
            menu.Items.Add("Settings", null, (_, _) => Invoke(() => { Recover(); openSettings(); }));
            sleepItem = new Forms.ToolStripMenuItem("Sleep", null, (_, _) => Invoke(toggleSleep));
            menu.Items.Add(sleepItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Invoke(exit));
            icon = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
            tray = new Forms.NotifyIcon { Text = "Desktop Companion", Icon = icon, ContextMenuStrip = menu };
            tray.DoubleClick += (_, _) => Invoke(Recover);
            tray.Visible = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            DisposeTray();
            reportError("The companion tray icon could not be created. Restart the companion to try again.");
        }
    }

    public bool RecoveryAvailable => !disposed && (hotkeyId.HasValue || tray is { Visible: true });
    public bool IsClickThrough { get; private set; }
    public SummonHotkey? RegisteredHotkey { get; private set; }

    public static string HotkeyLabel(SummonHotkey choice) => choice switch
    {
        SummonHotkey.CtrlAltSpace => "Ctrl + Alt + Space",
        SummonHotkey.CtrlShiftSpace => "Ctrl + Shift + Space",
        SummonHotkey.AltShiftSpace => "Alt + Shift + Space",
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    public bool TryRegisterHotkey(SummonHotkey choice, out string? error)
    {
        VerifyUsable();
        var modifiers = choice switch
        {
            SummonHotkey.CtrlAltSpace => 0x0002u | 0x0001u,
            SummonHotkey.CtrlShiftSpace => 0x0002u | 0x0004u,
            SummonHotkey.AltShiftSpace => 0x0001u | 0x0004u,
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };
        error = null;
        if (!enabled)
        {
            error = "Desktop integration is disabled for this session.";
            return false;
        }
        if (RegisteredHotkey == choice) return true;
        // Reserve a separate ID so a conflicting replacement cannot discard a working shortcut.
        var replacementId = 0x4340;
        while (ownedHotkeyIds.Contains(replacementId)) replacementId++;
        if (!RegisterHotKey(handle, replacementId, modifiers | 0x4000u, 0x20u))
        {
            var nativeError = Marshal.GetLastWin32Error();
            error = $"Could not register {HotkeyLabel(choice)} (Windows error {nativeError}). Choose another shortcut.";
            if (RegisteredHotkey is { } prior) error += $" {HotkeyLabel(prior)} remains active.";
            return false;
        }
        ownedHotkeyIds.Add(replacementId);
        if (hotkeyId is { } previousId && !UnregisterHotKey(handle, previousId))
        {
            var nativeError = Marshal.GetLastWin32Error();
            if (!UnregisterHotKey(handle, replacementId))
                reportError("Windows could not release an extra companion shortcut. It will be released when the companion closes.");
            else ownedHotkeyIds.Remove(replacementId);
            error = $"Could not change the companion shortcut (Windows error {nativeError}). The previous shortcut remains active.";
            return false;
        }
        if (hotkeyId is { } retiredId) ownedHotkeyIds.Remove(retiredId);
        hotkeyId = replacementId;
        RegisteredHotkey = choice;
        return true;
    }

    public void SetClickThrough(bool value)
    {
        VerifyUsable();
        if (value && !RecoveryAvailable)
            throw new InvalidOperationException("Click-through needs an available tray icon or summon shortcut first.");
        if (value == IsClickThrough) return;
        var oldStyle = ReadExtendedStyle();
        if (value && (oldStyle & LayeredStyle) == 0)
            throw new InvalidOperationException("Click-through requires the companion's transparent window.");
        var newStyle = value ? oldStyle | TransparentStyle : oldStyle & ~TransparentStyle;
        Marshal.SetLastPInvokeError(0);
        var previous = IntPtr.Size == 8
            ? SetWindowLongPtr(handle, ExtendedStyle, (nint)newStyle)
            : (nint)SetWindowLong(handle, ExtendedStyle, checked((int)newStyle));
        var error = Marshal.GetLastWin32Error();
        if (previous == 0 && error != 0) throw new Win32Exception(error);
        IsClickThrough = value;
    }

    public void Recover()
    {
        VerifyUsable();
        SetClickThrough(false);
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
        recovered?.Invoke();
    }

    public void Hide()
    {
        VerifyUsable();
        if (!RecoveryAvailable) throw new InvalidOperationException("Hiding needs an available tray icon or summon shortcut first.");
        window.Hide();
    }

    public void SetSleeping(bool sleeping)
    {
        VerifyUsable();
        if (sleepItem is not null) sleepItem.Text = sleeping ? "Wake" : "Sleep";
        if (tray is not null) tray.Text = sleeping ? "Desktop Companion (sleeping)" : "Desktop Companion";
    }

    private long ReadExtendedStyle()
    {
        Marshal.SetLastPInvokeError(0);
        var style = IntPtr.Size == 8 ? GetWindowLongPtr(handle, ExtendedStyle) : (nint)GetWindowLong(handle, ExtendedStyle);
        var error = Marshal.GetLastWin32Error();
        if (style == 0 && error != 0) throw new Win32Exception(error);
        return style.ToInt64();
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (!disposed && message == HotkeyMessage && hotkeyId is { } id && wParam == id)
        {
            handled = true;
            Invoke(Recover);
        }
        return 0;
    }

    private void Invoke(Action action)
    {
        if (disposed || window.Dispatcher.HasShutdownStarted) return;
        // Defer callbacks until outside the native message/menu dispatch.
        window.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            try { action(); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            { reportError("The desktop action could not finish. Try again or restart the companion."); }
        }));
    }

    private void VerifyUsable()
    {
        window.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (source.IsDisposed) throw new InvalidOperationException("The companion window has closed.");
    }

    private void WindowClosed(object? sender, EventArgs e) => Dispose();

    private void DisposeTray()
    {
        if (tray is not null) { tray.Visible = false; tray.Dispose(); tray = null; }
        menu?.Dispose();
        menu = null;
        sleepItem = null;
        icon?.Dispose();
        icon = null;
    }

    public void Dispose()
    {
        window.Dispatcher.VerifyAccess();
        if (disposed) return;
        // Restore interaction before removing the routes that can recover this window.
        if (IsClickThrough && !source.IsDisposed)
        {
            try { SetClickThrough(false); }
            catch (Win32Exception) { reportError("Could not restore companion interaction before closing."); }
        }
        disposed = true;
        window.Closed -= WindowClosed;
        if (!source.IsDisposed) source.RemoveHook(WindowMessage);
        foreach (var id in ownedHotkeyIds)
            if (!UnregisterHotKey(handle, id) && !source.IsDisposed)
                reportError("Windows could not release a companion shortcut. It will be released when the companion closes.");
        ownedHotkeyIds.Clear();
        hotkeyId = null;
        RegisteredHotkey = null;
        DisposeTray();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint windowHandle, int index, int value);
}
