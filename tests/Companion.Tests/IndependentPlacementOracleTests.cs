using Companion.Core.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentPlacementOracleTests
{
    [TestMethod]
    public void UnsnappedPlacementMatchesExhaustiveNearestFeasibleCoordinateOracle()
    {
        // Enumerate physically feasible integer positions and minimize displacement.
        // This oracle does not reuse Math.Clamp or the production axis implementation.
        const int left = -4;
        for (var workWidth = 2; workWidth <= 10; workWidth++)
        for (var windowWidth = 1; windowWidth <= 12; windowWidth++)
        for (var requested = -12; requested <= 14; requested++)
        {
            var feasible = windowWidth >= workWidth
                ? new[] { left }
                : Enumerable.Range(left, workWidth - windowWidth + 1).ToArray();
            var expected = feasible.OrderBy(candidate => Math.Abs(candidate - requested)).First();

            var actual = WindowPlacement.Constrain(new(requested, 0, windowWidth, 3), new(left, -3, workWidth, 14));

            Assert.AreEqual((double)expected, actual.Left,
                $"workWidth={workWidth}, windowWidth={windowWidth}, requested={requested}");
            Assert.AreEqual(0d, actual.Top);
            Assert.AreEqual((double)windowWidth, actual.Width);
            Assert.AreEqual(3d, actual.Height);
        }
    }

    [TestMethod]
    public void RelativePlacementIsInvariantUnderNegativeAndPositiveMonitorTranslation()
    {
        var workArea = new DesktopRect(0, 0, 400, 300);
        var offsets = new[] { (-3840d, -2160d), (-1920d, 0d), (0d, -1080d), (2560d, 1440d) };
        foreach (var x in new[] { -50d, 0d, 11.5d, 12d, 12.5d, 199d, 320d, 500d })
        foreach (var y in new[] { -20d, 0d, 12d, 12.5d, 150d, 210d, 400d })
        {
            var window = new DesktopRect(x, y, 80, 90);
            var baseline = WindowPlacement.Constrain(window, workArea, 12);
            foreach (var (dx, dy) in offsets)
            {
                var translated = WindowPlacement.Constrain(
                    window with { Left = window.Left + dx, Top = window.Top + dy },
                    workArea with { Left = dx, Top = dy }, 12);

                Assert.AreEqual(baseline with { Left = baseline.Left + dx, Top = baseline.Top + dy }, translated);
            }
        }
    }

    [TestMethod]
    public void SnappedConstraintIsIdempotentAcrossOverlappingAndDisjointSnapZones()
    {
        foreach (var snap in new[] { 0d, 12d, 80d, 200d })
        foreach (var x in Enumerable.Range(-20, 70).Select(value => value * 5.5))
        foreach (var y in new[] { -150d, -12d, 0d, 19.5, 49.5, 175d })
        {
            var work = new DesktopRect(-40, -20, 300, 200);
            var first = WindowPlacement.Constrain(new(x, y, 80, 60), work, snap);
            var repeated = WindowPlacement.Constrain(first, work, snap);

            Assert.AreEqual(first, repeated);
            Assert.IsTrue(first.Left >= -40 && first.Left + first.Width <= 260);
            Assert.IsTrue(first.Top >= -20 && first.Top + first.Height <= 180);
        }
    }

    [TestMethod]
    public void SupportedDisplayConversionsMatchIndependentDecimalArithmetic()
    {
        foreach (var dpi in new[] { 96, 120, 144, 168, 192, 240, 288, 384, 480 })
        foreach (var distance in new[] { 0m, 0.5m, 1m, 12m, 40m, 260m })
        {
            // Decimal arithmetic provides the expected physical distance independently
            // of the production double multiplication/division order.
            var expected = (double)(distance * dpi / 96m);
            var actual = WindowPlacement.DipsToPixels((double)distance, dpi);
            Assert.AreEqual(expected, actual, 1e-9, $"distance={distance}, dpi={dpi}");
        }
    }
}
