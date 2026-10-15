using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Companion.Infrastructure.MachineLearning;

/// <summary>One owned, CPU-only Python process; committed-memory and parent-death bounds.</summary>
internal sealed class MlWorkerJob : SafeHandleZeroOrMinusOneIsInvalid
{
    private MlWorkerJob() : base(true) { }
    internal static MlWorkerJob Create(int memoryMiB)
    {
        var job = CreateJobObject(0, null);
        if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        var info = new ExtendedLimits
        {
            Basic = new() { Flags = 0x2000 | 0x200 | 0x8, ActiveProcesses = 1 },
            JobMemory = checked((nuint)((ulong)memoryMiB * 1024 * 1024))
        };
        if (!SetInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf<ExtendedLimits>()))
        { var code = Marshal.GetLastWin32Error(); job.Dispose(); throw new Win32Exception(code); }
        return job;
    }
    internal void Assign(Process process)
    {
        if (!AssignProcessToJobObject(this, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal void Terminate() { if (!IsClosed && !IsInvalid) _ = TerminateJobObject(this, 1); }
    protected override bool ReleaseHandle() => CloseHandle(handle);
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern MlWorkerJob CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(MlWorkerJob job, int informationClass, ref ExtendedLimits info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(MlWorkerJob job, nint process);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(MlWorkerJob job, uint exitCode);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
