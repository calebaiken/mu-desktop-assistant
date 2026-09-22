using System.Text.Json;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;
using Companion.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class SavedPlacementTests
{
    private static SavedPlacement Example => new()
    {
        MonitorDeviceId = @"\\.\DISPLAY2", HorizontalFraction = .75, VerticalFraction = .5
    };

    [TestMethod]
    public void NegativeMonitorCoordinatesCaptureAndRestoreWithoutChangingSize()
    {
        var area = new DesktopRect(-1920, -1080, 1920, 1040);
        var window = new DesktopRect(-1040, -650, 160, 180);
        var saved = SavedPlacement.Capture(@"\\.\DISPLAY2", window, area);
        Assert.AreEqual(.5, saved.HorizontalFraction);
        Assert.AreEqual(.5, saved.VerticalFraction);
        Assert.AreEqual(window, saved.Restore(window, area));
    }

    [TestMethod]
    public void ChangedResolutionAndDpiSizePreserveRelativePositionAndWorkAreaInsets()
    {
        var saved = SavedPlacement.Capture("monitor", new(900, 700, 100, 100), new(0, 0, 1000, 800));
        Assert.AreEqual(new DesktopRect(-200, 1160, 200, 200),
            saved.Restore(new(900, 700, 200, 200), new(-1920, 40, 1920, 1320)));
    }

    [TestMethod]
    public void MissingMonitorUsesSuppliedFallbackWorkAreaWithSameRelativePosition()
    {
        Assert.AreEqual(new DesktopRect(1320, 470, 160, 180),
            Example.Restore(new(-5000, -5000, 160, 180), new(0, 40, 1920, 1040)));
    }

    [TestMethod]
    public void OversizedWindowAnchorsAndOutOfRangeCaptureIsConstrained()
    {
        var oversized = SavedPlacement.Capture("monitor", new(100, 100, 2000, 2000), new(-100, -100, 1000, 800));
        Assert.AreEqual(0d, oversized.HorizontalFraction);
        Assert.AreEqual(0d, oversized.VerticalFraction);
        Assert.AreEqual(new DesktopRect(-100, -100, 2000, 2000),
            Example.Restore(new(100, 100, 2000, 2000), new(-100, -100, 1000, 800)));
        var outside = SavedPlacement.Capture("monitor", new(-5000, 5000, 100, 100), new(0, 40, 1000, 800));
        Assert.AreEqual(0d, outside.HorizontalFraction);
        Assert.AreEqual(1d, outside.VerticalFraction);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-.01)]
    [DataRow(1.01)]
    public void InvalidFractionsCannotBeRestored(double invalid)
    {
        foreach (var placement in new[] { Example with { HorizontalFraction = invalid }, Example with { VerticalFraction = invalid } })
        {
            Assert.IsNotEmpty(placement.Validate());
            Assert.ThrowsExactly<InvalidOperationException>(() => placement.Restore(new(0, 0, 100, 100), new(0, 0, 1000, 1000)));
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("display\nname")]
    public void InvalidMonitorIdentityIsRejected(string? identity)
    {
        Assert.IsNotEmpty((Example with { MonitorDeviceId = identity! }).Validate());
        Assert.ThrowsExactly<ArgumentException>(() => SavedPlacement.Capture(identity!, new(0, 0, 100, 100), new(0, 0, 1000, 1000)));
    }

    [TestMethod]
    public void OversizedMonitorIdentityAndInvalidSchemaAreRejected()
    {
        Assert.IsNotEmpty((Example with { MonitorDeviceId = new string('x', 129) }).Validate());
        Assert.IsNotEmpty((Example with { SchemaVersion = 99 }).Validate());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Example.Restore(new(0, 0, 0, 100), new(0, 0, 1000, 1000)));
    }

    [TestMethod]
    public async Task NewPlacementIsNotWrittenUntilSaveAndDoesNotTouchPreferences()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), "preferences untouched");
        using (var store = new JsonPlacementStore(directory.PathValue))
        {
            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.AreEqual(PlacementLoadState.New, loaded.State);
            Assert.IsNull(loaded.Placement);
            Assert.IsTrue(loaded.CanSave);
            Assert.IsFalse(File.Exists(directory.FilePath("placement.json")));
            await store.SaveAsync(Example, CancellationToken.None);
        }
        using var restart = new JsonPlacementStore(directory.PathValue);
        var restored = await restart.LoadAsync(CancellationToken.None);
        Assert.AreEqual(PlacementLoadState.Loaded, restored.State);
        Assert.AreEqual(Example, restored.Placement);
        Assert.AreEqual("preferences untouched", await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{\"SchemaVersion\":1,\"MonitorDeviceId\":\"x\",\"HorizontalFraction\":0}")]
    [DataRow("{\"SchemaVersion\":1,\"MonitorDeviceId\":\"x\",\"HorizontalFraction\":0,\"VerticalFraction\":0,\"Path\":\"C:/untrusted\"}")]
    [DataRow("{\"SchemaVersion\":1,\"MonitorDeviceId\":\"x\",\"HorizontalFraction\":0,\"VerticalFraction\":2}")]
    [DataRow("{\"SchemaVersion\":1,\"MonitorDeviceId\":\"x\",\"HorizontalFraction\":0,\"VerticalFraction\":0,\"VerticalFraction\":0}")]
    public async Task CorruptPlacementIsIgnoredUntilSaveRetainsSingleQuarantine(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), json);
        using var store = new JsonPlacementStore(directory.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(PlacementLoadState.Invalid, loaded.State);
        Assert.IsNull(loaded.Placement);
        Assert.IsTrue(loaded.CanSave);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
        await store.SaveAsync(Example, CancellationToken.None);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("placement.json.invalid")));
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    [TestMethod]
    [DataRow("{\"SchemaVersion\":99}")]
    [DataRow("{\"SchemaVersion\":99,\"SchemaVersion\":1}")]
    public async Task FutureSchemaIsPreservedReadOnly(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), json);
        using var store = new JsonPlacementStore(directory.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(PlacementLoadState.UnsupportedVersion, loaded.State);
        Assert.IsFalse(loaded.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(Example, CancellationToken.None));
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
    }

    [TestMethod]
    public async Task OversizedFileIsReadOnlyBecauseItsSchemaCannotBeSafelyClassified()
    {
        using var directory = new TestDirectory();
        var json = "{\"SchemaVersion\":99,\"FutureValue\":\"" + new string('x', 4096) + "\"}";
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), json);
        using var store = new JsonPlacementStore(directory.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(PlacementLoadState.TooLarge, loaded.State);
        Assert.IsFalse(loaded.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(Example, CancellationToken.None));
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
    }

    [TestMethod]
    public async Task ExternalValidEditOrSchemaUpgradeIsNeverOverwritten()
    {
        using var directory = new TestDirectory();
        using var store = new JsonPlacementStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        await store.SaveAsync(Example, CancellationToken.None);
        var edited = JsonSerializer.Serialize(Example with { HorizontalFraction = .25 });
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), edited);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(Example, CancellationToken.None));
        Assert.AreEqual(edited, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
        const string future = "{\"SchemaVersion\":99}";
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), future);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(Example, CancellationToken.None));
        Assert.AreEqual(future, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
    }

    [TestMethod]
    public async Task LoadIoFailureIsVisibleAndDoesNotUnlockSaving()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), JsonSerializer.Serialize(Example));
        using var store = new JsonPlacementStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        using var locked = new FileStream(directory.FilePath("placement.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        await Assert.ThrowsExactlyAsync<IOException>(() => store.LoadAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(Example, CancellationToken.None));
    }

    [TestMethod]
    public async Task ConcurrentSavesRemainWholeAndCancelledSaveDoesNotCommit()
    {
        using var directory = new TestDirectory();
        using var store = new JsonPlacementStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            store.SaveAsync(Example with { HorizontalFraction = index / 10d }, CancellationToken.None)));
        var before = await File.ReadAllTextAsync(directory.FilePath("placement.json"));
        var placement = JsonSerializer.Deserialize<SavedPlacement>(before);
        Assert.IsNotNull(placement);
        Assert.IsEmpty(placement.Validate());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(Example, cancelled.Token));
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    [TestMethod]
    public async Task DiskFullAndInvalidInputPreservePreviousPlacement()
    {
        using var directory = new TestDirectory();
        var before = JsonSerializer.Serialize(Example);
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), before);
        using var store = new JsonPlacementStore(directory.PathValue, new DiskFullWriter());
        await store.LoadAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<IOException>(() => store.SaveAsync(Example with { HorizontalFraction = .1 }, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(Example with { VerticalFraction = double.NaN }, CancellationToken.None));
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
    }

    private sealed class DiskFullWriter : IAtomicFileWriter
    {
        public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents) =>
            throw new IOException("Injected disk-full failure.", unchecked((int)0x80070070));
    }
}
