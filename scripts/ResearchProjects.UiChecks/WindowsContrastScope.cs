using System.Runtime.InteropServices;

namespace ResearchProjects.UiChecks;

/// <summary>Dev-only, temporary OS contrast change on the disposable Windows test desktop.</summary>
internal sealed class WindowsContrastScope : IDisposable
{
    private HighContrast original;
    private bool changed;
    internal bool Enabled { get; }
    internal int NativeError { get; }

    internal WindowsContrastScope()
    {
        original.Size = (uint)Marshal.SizeOf<HighContrast>();
        if (!SystemParametersInfo(0x0042, original.Size, ref original, 0))
        { NativeError = Marshal.GetLastWin32Error(); return; }
        if ((original.Flags & 1) != 0) { Enabled = true; return; }
        var requested = original;
        requested.Flags |= 1;
        requested.DefaultScheme = IntPtr.Zero;
        if (!SystemParametersInfo(0x0043, requested.Size, ref requested, 2))
        { NativeError = Marshal.GetLastWin32Error(); return; }
        changed = true;
        var observed = new HighContrast { Size = original.Size };
        Enabled = SystemParametersInfo(0x0042, observed.Size, ref observed, 0) && (observed.Flags & 1) != 0;
    }

    public void Dispose()
    {
        if (!changed) return;
        var restored = original;
        // The scheme string is OS-owned; restoring flags does not dereference its pointer.
        restored.DefaultScheme = IntPtr.Zero;
        if (!SystemParametersInfo(0x0043, restored.Size, ref restored, 2))
            throw new InvalidOperationException("The disposable test desktop contrast state could not be restored.");
        changed = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast { internal uint Size; internal uint Flags; internal IntPtr DefaultScheme; }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint size, ref HighContrast value, uint flags);
}
