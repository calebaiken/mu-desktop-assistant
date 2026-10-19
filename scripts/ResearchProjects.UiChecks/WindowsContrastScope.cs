using System.Runtime.InteropServices;

namespace ResearchProjects.UiChecks;

/// <summary>Dev-only, temporary OS contrast change on the disposable Windows test desktop.</summary>
internal sealed class WindowsContrastScope : IDisposable
{
    private HighContrast original;
    private bool changed;
    private bool originalRead;
    internal bool Enabled { get; }
    internal int NativeError { get; }
    internal bool OriginalEnabled => originalRead && (original.Flags & 1) != 0;
    internal bool? Restored { get; private set; }

    internal WindowsContrastScope()
    {
        original.Size = (uint)Marshal.SizeOf<HighContrast>();
        if (!SystemParametersInfo(0x0042, original.Size, ref original, 0))
        { NativeError = Marshal.GetLastWin32Error(); return; }
        originalRead = true;
        if ((original.Flags & 1) != 0) { Enabled = true; return; }
        var requested = original;
        requested.Flags |= 1;
        requested.DefaultScheme = IntPtr.Zero;
        if (!SystemParametersInfo(0x0043, requested.Size, ref requested, 2))
        { NativeError = Marshal.GetLastWin32Error(); return; }
        changed = true;
        NotifyContrastChange();
        var observed = new HighContrast { Size = original.Size };
        Enabled = SystemParametersInfo(0x0042, observed.Size, ref observed, 0) && (observed.Flags & 1) != 0;
    }

    public void Dispose()
    {
        if (Restored.HasValue || !originalRead) return;
        if (changed)
        {
            var restored = original;
            // The scheme string is OS-owned; restoring flags does not dereference its pointer.
            restored.DefaultScheme = IntPtr.Zero;
            if (!SystemParametersInfo(0x0043, restored.Size, ref restored, 2))
                throw new InvalidOperationException("The disposable test desktop contrast state could not be restored.");
            changed = false;
            NotifyContrastChange();
        }
        var observed = new HighContrast { Size = original.Size };
        Restored = SystemParametersInfo(0x0042, observed.Size, ref observed, 0) && (observed.Flags & 1) == (original.Flags & 1);
        if (Restored != true) throw new InvalidOperationException("Test desktop contrast restoration was not confirmed.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast { internal uint Size; internal uint Flags; internal IntPtr DefaultScheme; }

    // No UPDATEINIFILE: the dev test does not persist an OS preference. Explicitly
    // notify real WPF listeners after changing the actual transient native flag.
    private static void NotifyContrastChange() => _ = SendMessageTimeout(new IntPtr(0xffff),
        0x001a, new UIntPtr(0x0043), IntPtr.Zero, 0x0002, 100, out _);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr parameter,
        IntPtr value, uint flags, uint timeout, out UIntPtr result);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint size, ref HighContrast value, uint flags);
}
