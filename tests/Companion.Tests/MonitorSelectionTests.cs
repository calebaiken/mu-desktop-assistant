using Companion.Core.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class MonitorSelectionTests
{
    [TestMethod]
    public void NoDisplaysOrOneDisplayLeavePlacementUnchanged()
    {
        Assert.IsNull(MonitorSelection.Next([], "current"));
        DesktopMonitor[] only = [new("current", new(0, 40, 1920, 1040))];
        Assert.IsNull(MonitorSelection.Next(only, "current"));
        Assert.IsNull(MonitorSelection.Next(only, "removed"));
        Assert.IsNull(MonitorSelection.NextPlacement(only, "current", new(100, 100, 160, 180), only[0].WorkArea));
    }

    [TestMethod]
    public void NegativeCoordinatesOrderByTopThenLeftAndWrapAtEnd()
    {
        DesktopMonitor[] monitors =
        [
            new("right", new(1920, 40, 1920, 1040)),
            new("primary", new(0, 40, 1920, 1040)),
            new("above", new(0, -1040, 1920, 1040)),
            new("left", new(-1920, 40, 1920, 1040))
        ];
        Assert.AreEqual("left", MonitorSelection.Next(monitors, "above")?.DeviceId);
        Assert.AreEqual("primary", MonitorSelection.Next(monitors, "left")?.DeviceId);
        Assert.AreEqual("right", MonitorSelection.Next(monitors, "primary")?.DeviceId);
        Assert.AreEqual("above", MonitorSelection.Next(monitors, "right")?.DeviceId);
    }

    [TestMethod]
    public void EnumerationOrderCannotChangeCycleAndInputIsNotReordered()
    {
        DesktopMonitor[] monitors =
        [
            new("c", new(1000, 0, 1000, 800)),
            new("a", new(-1000, 0, 1000, 800)),
            new("b", new(0, 0, 1000, 800))
        ];
        var original = monitors.ToArray();
        DesktopMonitor[][] orderings = [monitors, monitors.Reverse().ToArray(), [monitors[1], monitors[0], monitors[2]]];
        foreach (var ordering in orderings)
        {
            Assert.AreEqual("b", MonitorSelection.Next(ordering, "a")?.DeviceId);
            Assert.AreEqual("c", MonitorSelection.Next(ordering, "b")?.DeviceId);
            Assert.AreEqual("a", MonitorSelection.Next(ordering, "c")?.DeviceId);
        }
        Assert.IsTrue(original.SequenceEqual(monitors));
    }

    [TestMethod]
    public void CoincidentDisplaysUseOrdinalIdentityTieBreakAndCaseInsensitiveMatch()
    {
        var area = new DesktopRect(0, 40, 1920, 1040);
        DesktopMonitor[] monitors = [new("DISPLAY2", area), new("display10", area), new("Display1", area)];
        Assert.AreEqual("display10", MonitorSelection.Next(monitors, "DISPLAY1")?.DeviceId);
        Assert.AreEqual("DISPLAY2", MonitorSelection.Next(monitors, "DISPLAY10")?.DeviceId);
        Assert.AreEqual("Display1", MonitorSelection.Next(monitors, "display2")?.DeviceId);
    }

    [TestMethod]
    public void MissingCurrentDisplaySelectsFirstSurvivingDisplayDeterministically()
    {
        DesktopMonitor[] monitors = [new("right", new(0, 0, 1000, 800)), new("left", new(-1000, 0, 1000, 800))];
        Assert.AreEqual("left", MonitorSelection.Next(monitors, "unplugged")?.DeviceId);
    }

    [TestMethod]
    public void NextPlacementPreservesRelativePositionAfterDifferentResolutionAndDpiSize()
    {
        DesktopMonitor[] monitors = [new("source", new(-1000, 40, 1000, 800)), new("target", new(0, 40, 1920, 1320))];
        var placement = MonitorSelection.NextPlacement(monitors, "source", new(-325, 390, 100, 100), monitors[0].WorkArea);
        Assert.IsNotNull(placement);
        Assert.AreEqual("target", placement.MonitorDeviceId);
        Assert.AreEqual(.75, placement.HorizontalFraction);
        Assert.AreEqual(.5, placement.VerticalFraction);
        Assert.AreEqual(new DesktopRect(1290, 600, 200, 200),
            placement.Restore(new(-325, 390, 200, 200), monitors[1].WorkArea));
        Assert.IsEmpty(placement.Validate());
    }

    [TestMethod]
    public void OffscreenAndOversizedSourcePositionsStayRecoverableOnTarget()
    {
        DesktopMonitor[] monitors = [new("source", new(0, 0, 1000, 800)), new("target", new(1000, 40, 600, 440))];
        var placement = MonitorSelection.NextPlacement(monitors, "source", new(-5000, 5000, 100, 100), monitors[0].WorkArea);
        Assert.IsNotNull(placement);
        Assert.AreEqual(new DesktopRect(1000, 380, 100, 100), placement.Restore(new(-5000, 5000, 100, 100), monitors[1].WorkArea));
        var oversized = MonitorSelection.NextPlacement(monitors, "source", new(50, 70, 1500, 1000), monitors[0].WorkArea);
        Assert.IsNotNull(oversized);
        Assert.AreEqual(new DesktopRect(1000, 40, 1500, 1000), oversized.Restore(new(50, 70, 1500, 1000), monitors[1].WorkArea));
    }

    [TestMethod]
    public void DuplicateDeviceIdentityIsRejectedEvenWithDifferentCaseOrGeometry()
    {
        DesktopMonitor[] monitors = [new("display1", new(0, 0, 1000, 800)), new("DISPLAY1", new(1000, 0, 1000, 800))];
        Assert.ThrowsExactly<ArgumentException>(() => MonitorSelection.Next(monitors, "display1"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("display\nidentity")]
    public void InvalidDisplayAndCurrentIdentityAreRejected(string? identity)
    {
        DesktopMonitor[] valid = [new("display", new(0, 0, 1000, 800))];
        DesktopMonitor[] invalid = [new(identity!, valid[0].WorkArea)];
        Assert.ThrowsExactly<ArgumentException>(() => MonitorSelection.Next(valid, identity!));
        Assert.ThrowsExactly<ArgumentException>(() => MonitorSelection.Next(invalid, "current"));
    }

    [TestMethod]
    public void InvalidGeometryAndOversizedIdentityAreRejectedBeforeSingleDisplayNoOp()
    {
        DesktopRect[] invalidAreas =
        [
            new(double.NaN, 0, 1000, 800), new(0, double.PositiveInfinity, 1000, 800),
            new(0, 0, 0, 800), new(0, 0, 1000, -1), new(double.MaxValue, 0, double.MaxValue, 800)
        ];
        foreach (var area in invalidAreas)
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MonitorSelection.Next([new("display", area)], "display"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            MonitorSelection.Next([new(new string('x', 129), new(0, 0, 1000, 800))], "current"));
        Assert.ThrowsExactly<ArgumentNullException>(() => MonitorSelection.Next(null!, "current"));
    }
}
