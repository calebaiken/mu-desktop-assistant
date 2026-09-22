using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Infrastructure.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentSettingsAdversarialTests
{
    private string directory = null!;

    [TestInitialize]
    public void CreateIsolatedDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), "Companion.IndependentQA", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void RemoveOnlyOwnedFixture()
    {
        var full = Path.GetFullPath(directory);
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Companion.IndependentQA"));
        if (!string.Equals(Path.GetDirectoryName(full), expectedParent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(full), "N", out _))
            throw new InvalidOperationException("Refusing to remove a path outside the owned QA fixture.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }

    [TestMethod]
    [DataRow("\"1\"")]
    [DataRow("true")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{}")]
    public async Task WrongJsonTypeForSchemaUsesVisibleCorruptionRecovery(string schemaJson)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"),
            "{\"SchemaVersion\":" + schemaJson + ",\"CompanionName\":\"Untrusted\"}");
        using var store = new JsonSettingsStore(directory);

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.ResetInvalid, result.State);
        Assert.AreEqual(new CompanionSettings().CompanionName, result.Settings.CompanionName);
        Assert.IsTrue(result.CanSave);
    }

    [TestMethod]
    public async Task FailedBackupReadDoesNotAuthorizeSaving()
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{");
        var backupPath = Path.Combine(directory, "settings.json.bak");
        await File.WriteAllTextAsync(backupPath, "{\"SchemaVersion\":1,\"CompanionName\":\"Backup\"}");
        using var store = new JsonSettingsStore(directory);
        Exception? readFailure = null;
        using (var lockedBackup = new FileStream(backupPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { await store.LoadAsync(CancellationToken.None); }
            catch (IOException error) { readFailure = error; }
        }
        Assert.IsNotNull(readFailure, "The real file-sharing violation must be observed.");

        Exception? saveFailure = null;
        try { await store.SaveAsync(new CompanionSettings { CompanionName = "ShouldNotSave" }, CancellationToken.None); }
        catch (InvalidOperationException error) { saveFailure = error; }

        Assert.IsNotNull(saveFailure, "A failed LoadAsync must not leave SaveAsync authorized.");
        Assert.AreEqual("{", await File.ReadAllTextAsync(Path.Combine(directory, "settings.json")));
    }

    [TestMethod]
    public async Task ExternalCorruptionAfterLoadCannotReplaceLastGoodBackup()
    {
        var primaryPath = Path.Combine(directory, "settings.json");
        using (var store = new JsonSettingsStore(directory))
        {
            await store.LoadAsync(CancellationToken.None);
            await store.SaveAsync(new CompanionSettings { CompanionName = "FirstGood" }, CancellationToken.None);
            await store.SaveAsync(new CompanionSettings { CompanionName = "SecondGood" }, CancellationToken.None);
            await File.WriteAllTextAsync(primaryPath, "{ external corruption");
            await store.SaveAsync(new CompanionSettings { CompanionName = "ThirdGood" }, CancellationToken.None);
        }
        await File.WriteAllTextAsync(primaryPath, "{ another interrupted edit");
        using var restarted = new JsonSettingsStore(directory);

        var recovered = await restarted.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.RecoveredBackup, recovered.State,
            "A previously valid backup must survive corruption that occurred after LoadAsync.");
        Assert.IsTrue(recovered.Settings.CompanionName is "FirstGood" or "SecondGood");
    }
}
