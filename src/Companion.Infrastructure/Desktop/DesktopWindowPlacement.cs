using System.ComponentModel;
using System.Runtime.InteropServices;
using Companion.Core.Desktop;

namespace Companion.Infrastructure.Desktop;

/// <summary>Physical-pixel placement for an owned per-monitor-aware window.</summary>
public static class DesktopWindowPlacement
{
    public static SavedPlacement Capture(nint windowHandle)
    {
        var (window, workArea) = ReadBounds(windowHandle);
        var monitor = ReadMonitor(MonitorFromWindow(windowHandle, 2));
        return SavedPlacement.Capture(monitor.DeviceName, window, workArea);
    }

    /// <summary>Reads the current display snapshot without moving or resizing any window.</summary>
    public static IReadOnlyList<DesktopMonitor> EnumerateMonitors()
    {
        var monitors = new List<DesktopMonitor>();
        // Never unwind a managed exception through native display enumeration.
        Exception? readFailure = null;
        MonitorEnum callback = (monitor, _, _, _) =>
        {
            try
            {
                var candidate = ReadMonitor(monitor);
                monitors.Add(new(candidate.DeviceName, ToRect(candidate.Work)));
                return true;
            }
            catch (Exception error) { readFailure = error; return false; }
        };
        var enumerated = EnumDisplayMonitors(0, 0, callback, 0);
        if (readFailure is not null) throw readFailure;
        if (!enumerated) throw new Win32Exception(Marshal.GetLastWin32Error());
        return monitors.AsReadOnly();
    }

    /// <summary>
    /// Prepares the next display's relative placement. Returns null with fewer than two displays.
    /// The caller applies BeginRestore then CompleteRestore after WPF processes the DPI resize.
    /// </summary>
    public static SavedPlacement? NextMonitorPlacement(nint windowHandle)
    {
        var window = ReadWindow(windowHandle);
        var monitor = ReadMonitor(MonitorFromWindow(windowHandle, 2));
        return MonitorSelection.NextPlacement(EnumerateMonitors(), monitor.DeviceName, window, ToRect(monitor.Work));
    }

    /// <summary>
    /// Move to the saved monitor (or nearest surviving monitor). Queue CompleteRestore
    /// after WPF processes its DPI resize. Returns false when the saved monitor is absent.
    /// </summary>
    public static bool BeginRestore(nint windowHandle, SavedPlacement placement) => Restore(windowHandle, placement);

    /// <summary>Reapply relative placement with the actual size after WPF's DPI resize.</summary>
    public static bool CompleteRestore(nint windowHandle, SavedPlacement placement) => Restore(windowHandle, placement);

    private static bool Restore(nint windowHandle, SavedPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Validate().Count != 0) throw new ArgumentException("Invalid saved placement.", nameof(placement));
        var (window, _) = ReadBounds(windowHandle);
        DesktopMonitor? matching = null;
        foreach (var monitor in EnumerateMonitors())
            if (string.Equals(monitor.DeviceId, placement.MonitorDeviceId, StringComparison.OrdinalIgnoreCase))
                matching = monitor;
        var targetWorkArea = matching?.WorkArea ?? ToRect(ReadMonitor(MonitorFromWindow(windowHandle, 2)).Work);
        Move(windowHandle, window, placement.Restore(window, targetWorkArea));
        return matching.HasValue;
    }

    public static void Constrain(nint windowHandle, bool snapToEdges)
    {
        var (window, workArea) = ReadBounds(windowHandle);
        var dpi = GetDpiForWindow(windowHandle);
        if (dpi == 0) throw new Win32Exception("Could not read the companion's display scale.");
        var target = WindowPlacement.Constrain(window, workArea,
            snapToEdges ? WindowPlacement.DipsToPixels(12, dpi) : 0);
        Move(windowHandle, window, target);
    }

    public static void MoveBy(nint windowHandle, int horizontalDips, int verticalDips)
    {
        var (window, workArea) = ReadBounds(windowHandle);
        var dpi = GetDpiForWindow(windowHandle);
        if (dpi == 0) throw new Win32Exception("Could not read the companion's display scale.");
        var requested = window with
        {
            Left = window.Left + horizontalDips * dpi / 96d,
            Top = window.Top + verticalDips * dpi / 96d
        };
        Move(windowHandle, window, WindowPlacement.Constrain(requested, workArea));
    }

    private static void Move(nint windowHandle, DesktopRect window, DesktopRect target)
    {
        if (target.Left == window.Left && target.Top == window.Top) return;
        // Preserve size, z-order and keyboard focus; WPF owns DPI-driven resizing.
        if (!SetWindowPos(windowHandle, 0, checked((int)Math.Round(target.Left)),
            checked((int)Math.Round(target.Top)), 0, 0, 0x0001 | 0x0004 | 0x0010))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static (DesktopRect Window, DesktopRect WorkArea) ReadBounds(nint windowHandle)
    {
        var window = ReadWindow(windowHandle);
        var monitor = MonitorFromWindow(windowHandle, 2); // nearest surviving monitor
        var info = ReadMonitor(monitor);
        return (window, ToRect(info.Work));
    }

    private static DesktopRect ReadWindow(nint windowHandle)
    {
        if (windowHandle == 0) throw new ArgumentException("A window handle is required.", nameof(windowHandle));
        if (!GetWindowRect(windowHandle, out var window)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ToRect(window);
    }

    private static MonitorInfo ReadMonitor(nint monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), DeviceName = string.Empty };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return info;
    }

    private static DesktopRect ToRect(NativeRect rect) =>
        new(rect.Left, rect.Top, (double)rect.Right - rect.Left, (double)rect.Bottom - rect.Top);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    private delegate bool MonitorEnum(nint monitor, nint deviceContext, nint monitorRectangle, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint deviceContext, nint clipRectangle, MonitorEnum callback, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint windowHandle, nint insertAfter, int x, int y,
        int width, int height, uint flags);
}
