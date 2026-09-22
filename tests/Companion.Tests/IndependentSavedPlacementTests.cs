using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentSavedPlacementTests
{
    [TestMethod]
    public void RationalGridUsesAvailableTravelRatherThanMonitorSize()
    {
        // Independent integer construction: each available travel is exactly eight steps.
        foreach (var left in new[] { -2400, 0, 1100 })
        foreach (var top in new[] { -1300, 40, 900 })
        foreach (var width in new[] { 100, 300, 700 })
        foreach (var height in new[] { 120, 350 })
        for (var x = 0; x <= 8; x++)
        for (var y = 0; y <= 8; y++)
        {
            var original = new DesktopRect(left + 125 * x, top + 80 * y, width, height);
            var saved = SavedPlacement.Capture("fixture-monitor", original,
                new(left, top, width + 1000, height + 640));
            Assert.AreEqual(x / 8d, saved.HorizontalFraction);
            Assert.AreEqual(y / 8d, saved.VerticalFraction);
            var restored = saved.Restore(new(-9000, 9000, width * 2, height * 2),
                new(left - 900, top + 70, width * 2 + 1600, height * 2 + 960));
            Assert.AreEqual(new DesktopRect(left - 900 + 200 * x, top + 70 + 120 * y,
                width * 2, height * 2), restored);
        }
    }

    [TestMethod]
    public void RepeatedResolutionChangesDoNotAccumulateMeaningfulFractionDrift()
    {
        var saved = new SavedPlacement { MonitorDeviceId = "fixture", HorizontalFraction = 1d / 3, VerticalFraction = 2d / 7 };
        var original = saved;
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            var area = iteration % 2 == 0 ? new DesktopRect(-1920, 40, 1920, 1040) : new DesktopRect(2560, -900, 2560, 1440);
            var size = iteration % 2 == 0 ? new DesktopRect(0, 0, 260, 380) : new DesktopRect(0, 0, 520, 760);
            saved = SavedPlacement.Capture("fixture", saved.Restore(size, area), area);
        }
        Assert.AreEqual(original.HorizontalFraction, saved.HorizontalFraction, 1e-12);
        Assert.AreEqual(original.VerticalFraction, saved.VerticalFraction, 1e-12);
    }

    [TestMethod]
    public void OversizeOnOneAxisDoesNotDestroyTheOtherAxisFraction()
    {
        var captured = SavedPlacement.Capture("fixture", new(-200, 425, 2000, 100), new(-1000, 50, 1000, 600));
        Assert.AreEqual(0d, captured.HorizontalFraction);
        Assert.AreEqual(.75, captured.VerticalFraction);
        Assert.AreEqual(new DesktopRect(80, 660, 2000, 200),
            captured.Restore(new(0, 0, 2000, 200), new(80, 60, 1000, 1000)));
    }

    [TestMethod]
    [DataRow("{\"SchemaVersion\":1,\"SchemaVersion\":99}")]
    [DataRow("{\"SchemaVersion\":99,\"SchemaVersion\":1}")]
    public async Task ConflictingFutureSchemaOrderNeverBecomesRepairable(string json)
    {
        using var directory = new TestDirectory();
        var path = directory.FilePath("placement.json");
        await File.WriteAllTextAsync(path, json);
        using var store = new JsonPlacementStore(directory.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(PlacementLoadState.UnsupportedVersion, loaded.State);
        Assert.IsFalse(loaded.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(
            new() { MonitorDeviceId = "fixture" }, CancellationToken.None));
        Assert.AreEqual(json, await File.ReadAllTextAsync(path));
        Assert.IsFalse(File.Exists(path + ".invalid"));
    }
}
