using System.Diagnostics;
using Companion.Infrastructure.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class ProcessResourceSamplerTests
{
    [TestMethod]
    public void CurrentProcessSnapshotHasNonnegativeCpuAndNonzeroMemory()
    {
        var snapshot = ProcessResourceSampler.Capture();
        Assert.IsTrue(snapshot.CpuTime >= TimeSpan.Zero);
        Assert.IsTrue(snapshot.PrivateBytes > 0);
        Assert.IsTrue(snapshot.WorkingSetBytes > 0);
    }

    [TestMethod]
    public void CumulativeCpuDoesNotDecreaseAcrossSamples()
    {
        var previous = ProcessResourceSampler.Capture();
        for (var index = 0; index < 10; index++)
        {
            var current = ProcessResourceSampler.Capture();
            Assert.IsTrue(current.CpuTime >= previous.CpuTime);
            previous = current;
        }
    }

    [TestMethod]
    public void FrameworkCpuMeasurementFallsBetweenSurroundingNativeSamples()
    {
        using var process = Process.GetCurrentProcess();
        var before = ProcessResourceSampler.Capture();
        process.Refresh();
        var frameworkCpu = process.TotalProcessorTime;
        var after = ProcessResourceSampler.Capture();
        Assert.IsTrue(frameworkCpu >= before.CpuTime);
        Assert.IsTrue(frameworkCpu <= after.CpuTime);
    }

    [TestMethod]
    public void NativeMemoryCountersAgreeWithFrameworkWithinSamplingTolerance()
    {
        using var process = Process.GetCurrentProcess();
        var before = ProcessResourceSampler.Capture();
        process.Refresh();
        var frameworkPrivate = process.PrivateMemorySize64;
        var frameworkWorkingSet = process.WorkingSet64;
        var after = ProcessResourceSampler.Capture();
        Assert.IsTrue(frameworkPrivate > 0);
        Assert.IsTrue(frameworkWorkingSet > 0);
        // These APIs do not provide an atomic shared snapshot. Allow runtime/GC allocations and
        // working-set changes between calls while still detecting a wrong field or unit conversion.
        AssertNearSamples(frameworkPrivate, before.PrivateBytes, after.PrivateBytes);
        AssertNearSamples(frameworkWorkingSet, before.WorkingSetBytes, after.WorkingSetBytes);
    }

    private static void AssertNearSamples(long frameworkValue, long before, long after)
    {
        var tolerance = Math.Max(16L * 1024 * 1024, frameworkValue / 4);
        Assert.IsTrue(frameworkValue >= Math.Min(before, after) - tolerance &&
            frameworkValue <= Math.Max(before, after) + tolerance);
    }
}
