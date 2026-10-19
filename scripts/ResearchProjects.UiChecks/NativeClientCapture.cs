using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ResearchProjects.UiChecks;

/// <summary>Copies actual visible native client pixels, including inherited background and margins.</summary>
internal static class NativeClientCapture
{
    internal static BitmapSource Capture(Window window)
    {
        window.Activate();
        window.UpdateLayout();
        _ = DwmFlush();
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetClientRect(handle, out var rectangle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Bottom - rectangle.Top;
        if (width is <= 0 or > 8192 || height is <= 0 or > 8192) throw new InvalidOperationException("Invalid native client dimensions.");
        var source = GetDC(handle);
        if (source == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var destination = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            destination = CreateCompatibleDC(source);
            bitmap = CreateCompatibleBitmap(source, width, height);
            if (destination == IntPtr.Zero || bitmap == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(destination, bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!BitBlt(destination, 0, 0, width, height, source, 0, 0, 0x00CC0020)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _ = SelectObject(destination, previous);
            previous = IntPtr.Zero;
            var stride = checked(width * 4);
            var pixels = new byte[checked(stride * height)];
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = width, Height = -height, Planes = 1, BitCount = 32 };
            if (GetDIBits(destination, bitmap, 0, (uint)height, pixels, ref info, 0) != height)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            // The chosen views have an empty 8-DIP margin. Reject unavailable/black
            // desktop captures instead of treating them as native visual evidence.
            if (window.Background is SolidColorBrush { Color.A: 255 } background)
            {
                var offset = (8 * width + 8) * 4;
                if (pixels[offset] != background.Color.B || pixels[offset + 1] != background.Color.G || pixels[offset + 2] != background.Color.R)
                    throw new InvalidOperationException("Native client pixels did not match the actual window background.");
            }
            var dpi = VisualTreeHelper.GetDpi(window);
            return BitmapSource.Create(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Bgr32, null, pixels, stride);
        }
        finally
        {
            if (previous != IntPtr.Zero) _ = SelectObject(destination, previous);
            if (bitmap != IntPtr.Zero) _ = DeleteObject(bitmap);
            if (destination != IntPtr.Zero) _ = DeleteDC(destination);
            _ = ReleaseDC(handle, source);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal uint Size;
        internal int Width, Height;
        internal ushort Planes, BitCount;
        internal uint Compression, SizeImage;
        internal int XPelsPerMeter, YPelsPerMeter;
        internal uint ClrUsed, ClrImportant;
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr source);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] pixels, ref BitmapInfo info, uint usage);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
