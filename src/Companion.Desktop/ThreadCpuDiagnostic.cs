using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Companion.Desktop;

internal sealed record ThreadCpuReading(int Id, long CpuTicks, string StartAddress);

/// <summary>On-demand performance-harness diagnostic; never schedules or polls process information.</summary>
internal static class ThreadCpuDiagnostic
{
    internal static List<ThreadCpuReading> Capture()
    {
        List<ThreadCpuReading> readings = [];
        using var process = Process.GetCurrentProcess();
        ProcessThreadCollection threads;
        try { threads = process.Threads; }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            // Diagnostics must tolerate the process snapshot becoming unavailable.
            return readings;
        }

        try
        {
            foreach (ProcessThread thread in threads)
            {
                try
                {
                    var id = thread.Id;
                    var cpuTicks = thread.TotalProcessorTime.Ticks;
                    var startAddress = unchecked((nuint)thread.StartAddress);
                    readings.Add(new(id, cpuTicks, "0x" + startAddress.ToString("X", CultureInfo.InvariantCulture)));
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception)
                {
                    // A thread can exit or become unreadable after the collection was captured.
                }
            }
        }
        finally
        {
            // Dispose the entire snapshot even if an unexpected error interrupts collection.
            foreach (ProcessThread thread in threads) thread.Dispose();
        }
        readings.Sort((left, right) => left.Id.CompareTo(right.Id));
        return readings;
    }
}
