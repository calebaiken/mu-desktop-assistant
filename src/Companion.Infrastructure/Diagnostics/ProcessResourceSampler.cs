using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Companion.Infrastructure.Diagnostics;

/// <summary>Cumulative CPU time and current memory counters for this process.</summary>
public readonly record struct ProcessResourceSnapshot(TimeSpan CpuTime, long PrivateBytes, long WorkingSetBytes);

/// <summary>Samples the current process directly without enumerating system-wide process information.</summary>
public static class ProcessResourceSampler
{
    private static readonly uint MemoryCountersSize = checked((uint)Marshal.SizeOf<ProcessMemoryCountersEx>());

    public static ProcessResourceSnapshot Capture()
    {
        // This is a borrowed pseudo-handle. It must never be wrapped in an owning handle or closed.
        var process = GetCurrentProcess();
        if (!GetProcessTimes(process, out _, out _, out var kernelTime, out var userTime))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var counters = new ProcessMemoryCountersEx { Size = MemoryCountersSize };
        if (!K32GetProcessMemoryInfo(process, ref counters, counters.Size))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        // Windows process times and TimeSpan both use 100-nanosecond ticks.
        var ticks = checked((long)(ToTicks(kernelTime) + ToTicks(userTime)));
        return new(TimeSpan.FromTicks(ticks), checked((long)counters.PrivateUsage), checked((long)counters.WorkingSetSize));
    }

    private static ulong ToTicks(NativeFileTime time) => ((ulong)time.High << 32) | time.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low, High;
    }

    // Two DWORDs followed by nine SIZE_T values: 80 bytes on x64, 44 bytes on x86.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx
    {
        public uint Size;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(nint process, out NativeFileTime creationTime,
        out NativeFileTime exitTime, out NativeFileTime kernelTime, out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool K32GetProcessMemoryInfo(nint process, ref ProcessMemoryCountersEx counters, uint size);
}
