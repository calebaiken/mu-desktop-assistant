using Companion.Core.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class WindowPlacementTests
{
    [TestMethod]
    public void InteriorPositionAndSizeArePreservedWithoutSnapping()
    {
        var window = new DesktopRect(314.5, 207.25, 160, 180);

        Assert.AreEqual(window, WindowPlacement.Constrain(window, new(0, 40, 1920, 1040)));
    }

    [TestMethod]
    public void WorkAreaInsetsKeepWindowClearOfTaskbarAndReservedEdges()
    {
        var workArea = new DesktopRect(48, 32, 1840, 1000);

        Assert.AreEqual(new DesktopRect(48, 32, 160, 180),
            WindowPlacement.Constrain(new(-5, 0, 160, 180), workArea));
        Assert.AreEqual(new DesktopRect(1728, 852, 160, 180),
            WindowPlacement.Constrain(new(1900, 1050, 160, 180), workArea));
    }

    [TestMethod]
    public void MonitorsLeftOfAndAbovePrimaryUseTheirNegativeCoordinates()
    {
        Assert.AreEqual(new DesktopRect(-1920, 40, 160, 180),
            WindowPlacement.Constrain(new(-1940, 10, 160, 180), new(-1920, 40, 1920, 1040)));
        Assert.AreEqual(new DesktopRect(-160, 900, 160, 180),
            WindowPlacement.Constrain(new(20, 1100, 160, 180), new(-1920, 40, 1920, 1040)));
        Assert.AreEqual(new DesktopRect(100, -1080, 160, 180),
            WindowPlacement.Constrain(new(100, -1200, 160, 180), new(0, -1080, 1920, 1040)));
        Assert.AreEqual(new DesktopRect(100, -220, 160, 180),
            WindowPlacement.Constrain(new(100, -100, 160, 180), new(0, -1080, 1920, 1040)));
    }

    [TestMethod]
    public void OversizedDimensionsAnchorAtWorkAreaStartWithoutResizing()
    {
        Assert.AreEqual(new DesktopRect(-800, 40, 900, 1200),
            WindowPlacement.Constrain(new(100, -50, 900, 1200), new(-800, 40, 800, 600), 12));
        Assert.AreEqual(new DesktopRect(100, 145, 400, 50),
            WindowPlacement.Constrain(new(150, 145, 400, 50), new(100, 100, 200, 100)));
        Assert.AreEqual(new DesktopRect(145, 100, 50, 400),
            WindowPlacement.Constrain(new(145, 150, 50, 400), new(100, 100, 100, 200)));
    }

    [TestMethod]
    public void EqualAndTinyWorkAreasRemainUsableWithoutInvalidClampRanges()
    {
        Assert.AreEqual(new DesktopRect(-0.5, -0.25, 0.5, 0.25),
            WindowPlacement.Constrain(new(100, 100, 0.5, 0.25), new(-0.5, -0.25, 0.5, 0.25)));
        Assert.AreEqual(new DesktopRect(-0.5, -0.25, 160, 180),
            WindowPlacement.Constrain(new(100, 100, 160, 180), new(-0.5, -0.25, 0.5, 0.25), 12));
    }

    [TestMethod]
    public void ExactThresholdSnapsToAllWorkAreaEdges()
    {
        var workArea = new DesktopRect(-1920, -1040, 1920, 1000);

        Assert.AreEqual(new DesktopRect(-1920, -1040, 160, 180),
            WindowPlacement.Constrain(new(-1908, -1028, 160, 180), workArea, 12));
        Assert.AreEqual(new DesktopRect(-160, -220, 160, 180),
            WindowPlacement.Constrain(new(-172, -232, 160, 180), workArea, 12));
        var outsideThreshold = new DesktopRect(-1907.75, -1027.75, 160, 180);
        Assert.AreEqual(outsideThreshold, WindowPlacement.Constrain(outsideThreshold, workArea, 12));
    }

    [TestMethod]
    public void OverlappingSnapZonesChooseNearestEdgeAndMinimumOnTies()
    {
        var workArea = new DesktopRect(0, 0, 300, 200);

        Assert.AreEqual(new DesktopRect(0, 40, 260, 160),
            WindowPlacement.Constrain(new(15, 25, 260, 160), workArea, 30));
        Assert.AreEqual(new DesktopRect(0, 0, 260, 160),
            WindowPlacement.Constrain(new(20, 20, 260, 160), workArea, 30));
    }

    [TestMethod]
    [DataRow(96d, 12d)]
    [DataRow(144d, 18d)]
    [DataRow(192d, 24d)]
    public void SnapDistanceUsesTargetDpiAndIncludesExactPhysicalThreshold(double dpi, double expectedPixels)
    {
        double snapDistance = WindowPlacement.DipsToPixels(12, dpi);
        Assert.AreEqual(expectedPixels, snapDistance);
        var workArea = new DesktopRect(0, 0, 400, 400);

        Assert.AreEqual(new DesktopRect(0, 0, 100, 100),
            WindowPlacement.Constrain(new(expectedPixels, expectedPixels, 100, 100), workArea, snapDistance));
        Assert.AreEqual(new DesktopRect(300, 300, 100, 100),
            WindowPlacement.Constrain(new(300 - expectedPixels, 300 - expectedPixels, 100, 100), workArea, snapDistance));
        var outsideThreshold = new DesktopRect(expectedPixels + 0.25, expectedPixels + 0.25, 100, 100);
        Assert.AreEqual(outsideThreshold, WindowPlacement.Constrain(outsideThreshold, workArea, snapDistance));
    }

    [TestMethod]
    public void DipConversionPreservesZeroAndFractionalDistances()
    {
        Assert.AreEqual(0d, WindowPlacement.DipsToPixels(0, 192));
        Assert.AreEqual(1.5, WindowPlacement.DipsToPixels(1, 144));
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void NonfiniteRectangleComponentsAreRejectedForBothArguments(double invalid)
    {
        DesktopRect[] invalidRectangles =
        [
            new(invalid, 0, 100, 100), new(0, invalid, 100, 100),
            new(0, 0, invalid, 100), new(0, 0, 100, invalid)
        ];
        foreach (var rectangle in invalidRectangles)
        {
            Assert.AreEqual("window", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(rectangle, new(0, 0, 1000, 1000))).ParamName);
            Assert.AreEqual("workArea", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(new(0, 0, 10, 10), rectangle)).ParamName);
        }
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    public void NonpositiveRectangleDimensionsAreRejectedForBothArguments(double invalid)
    {
        DesktopRect[] invalidRectangles = [new(0, 0, invalid, 100), new(0, 0, 100, invalid)];
        foreach (var rectangle in invalidRectangles)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(rectangle, new(0, 0, 1000, 1000)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(new(0, 0, 10, 10), rectangle));
        }
    }

    [TestMethod]
    public void OverflowingRectangleEdgesAreRejected()
    {
        DesktopRect[] invalidRectangles =
        [
            new(double.MaxValue, 0, double.MaxValue, 100),
            new(0, double.MaxValue, 100, double.MaxValue)
        ];
        foreach (var rectangle in invalidRectangles)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(rectangle, new(0, 0, 1000, 1000)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                WindowPlacement.Constrain(new(0, 0, 10, 10), rectangle));
        }
    }

    [TestMethod]
    public void OversizedPlacementCannotCreateAnInfiniteEdgeFromFiniteInputEdges()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowPlacement.Constrain(
            new(0, 0, 1e308, 100), new(1e308, 0, 1e307, 1000)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowPlacement.Constrain(
            new(0, 0, 100, 1e308), new(0, 1e308, 1000, 1e307)));
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidSnapDistanceAndDipDistanceAreRejected(double invalid)
    {
        Assert.AreEqual("snapDistance", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WindowPlacement.Constrain(new(0, 0, 100, 100), new(0, 0, 1000, 1000), invalid)).ParamName);
        Assert.AreEqual("distanceInDips", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WindowPlacement.DipsToPixels(invalid, 96)).ParamName);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidDpiIsRejected(double invalid)
    {
        Assert.AreEqual("dpi", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WindowPlacement.DipsToPixels(12, invalid)).ParamName);
    }

    [TestMethod]
    public void OverflowingDipConversionIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowPlacement.DipsToPixels(double.MaxValue, 192));
    }
}
