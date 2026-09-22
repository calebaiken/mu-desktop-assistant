using System.Text.Json;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Infrastructure.Configuration;
using Companion.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    public async Task NewSettingsStayInMemoryUntilExplicitSaveAndRoundTrip()
    {
        using var directory = new TestDirectory();
        using (var store = new JsonSettingsStore(directory.PathValue))
        {
            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.AreEqual(SettingsLoadState.New, loaded.State);
            Assert.IsFalse(File.Exists(directory.FilePath("settings.json")));
            await store.SaveAsync(loaded.Settings with { CompanionName = "Nova", AlwaysOnTop = true }, CancellationToken.None);
        }
        using var restarted = new JsonSettingsStore(directory.PathValue);
        var actual = await restarted.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.Loaded, actual.State);
        Assert.AreEqual("Nova", actual.Settings.CompanionName);
        Assert.IsTrue(actual.Settings.AlwaysOnTop);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{\"SchemaVersion\":1,\"CompanionName\":null}")]
    [DataRow("{\"SchemaVersion\":1,\"CompanionName\":\"x\",\"ApiKey\":\"SECRET\"}")]
    [DataRow("{\"SchemaVersion\":1,\"SchemaVersion\":1}")]
    public async Task MalformedPreferencesRecoverWithoutOverwritingOriginal(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), json);
        using var store = new JsonSettingsStore(directory.PathValue);
        var result = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.ResetInvalid, result.State);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
        await store.SaveAsync(new(), CancellationToken.None);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("settings.json.invalid")));
    }

    [TestMethod]
    public async Task FutureSchemaIsReadOnlyAndNotOverwritten()
    {
        using var directory = new TestDirectory();
        const string json = "{\"SchemaVersion\":99,\"CompanionName\":\"Future\"}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), json);
        using var store = new JsonSettingsStore(directory.PathValue);
        var result = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.UnsupportedVersion, result.State);
        Assert.IsFalse(result.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(new(), CancellationToken.None));
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task OversizedSettingsAreQuarantinedWithoutReadingAsJson()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), new string('x', 20000));
        using var store = new JsonSettingsStore(directory.PathValue);
        Assert.AreEqual(SettingsLoadState.ResetInvalid, (await store.LoadAsync(CancellationToken.None)).State);
        await store.SaveAsync(new(), CancellationToken.None);
        Assert.AreEqual(20000L, new FileInfo(directory.FilePath("settings.json.invalid")).Length);
        Assert.IsTrue(new FileInfo(directory.FilePath("settings.json")).Length < 16384);
    }

    [TestMethod]
    public async Task SimulatedDiskFullPreservesPrimaryAndCanRecover()
    {
        using var directory = new TestDirectory();
        using (var initial = new JsonSettingsStore(directory.PathValue))
        {
            await initial.LoadAsync(CancellationToken.None);
            await initial.SaveAsync(new() { CompanionName = "Preserved" }, CancellationToken.None);
        }
        var before = await File.ReadAllTextAsync(directory.FilePath("settings.json"));
        using var failed = new JsonSettingsStore(directory.PathValue, new DiskFullWriter());
        await failed.LoadAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<IOException>(() => failed.SaveAsync(new(), CancellationToken.None));
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task ConcurrentSavesRemainWholeDocumentsAndLeaveNoTemporaryFiles()
    {
        using var directory = new TestDirectory();
        using var store = new JsonSettingsStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
            store.SaveAsync(new() { CompanionName = "Name " + index }, CancellationToken.None)));
        using var current = JsonDocument.Parse(await File.ReadAllTextAsync(directory.FilePath("settings.json")));
        using var backup = JsonDocument.Parse(await File.ReadAllTextAsync(directory.FilePath("settings.json.bak")));
        Assert.IsTrue(current.RootElement.GetProperty("CompanionName").GetString()!.StartsWith("Name ", StringComparison.Ordinal));
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    [TestMethod]
    public async Task CancelledSaveDoesNotCommit()
    {
        using var directory = new TestDirectory();
        using var store = new JsonSettingsStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(new(), cancelled.Token));
        Assert.IsFalse(File.Exists(directory.FilePath("settings.json")));
    }

    [TestMethod]
    public void AtomicReplaceFailureKeepsOriginalAndCleansTemporaryFile()
    {
        using var directory = new TestDirectory();
        var target = directory.FilePath("settings.json");
        File.WriteAllText(target, "original");
        using var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.ThrowsExactly<IOException>(() => new AtomicFileWriter().Write(target, null, "replacement"u8));
        locked.Dispose();
        Assert.AreEqual("original", File.ReadAllText(target));
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("line\nbreak")]
    [DataRow("12345678901234567890123456789012345678901")]
    public void InvalidNameIsRejected(string name) => Assert.IsNotEmpty(new CompanionSettings { CompanionName = name }.Validate());

    private sealed class DiskFullWriter : IAtomicFileWriter
    {
        public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents)
            => throw new IOException("Injected disk-full failure.", unchecked((int)0x80070070));
    }
}
