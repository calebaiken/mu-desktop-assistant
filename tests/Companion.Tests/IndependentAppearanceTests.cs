using System.Text.Json;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Infrastructure.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentAppearanceTests
{
    [TestMethod]
    public async Task RepairFromLegacyBackupPreservesDamagedPrimaryAndMigratedBackup()
    {
        using var directory = new TestDirectory();
        const string damaged = "{ interrupted version-one primary";
        const string legacy = "{\"SchemaVersion\":1,\"CompanionName\":\"Recover me\",\"AlwaysOnTop\":true,\"ReducedMotion\":true}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), damaged);
        await File.WriteAllTextAsync(directory.FilePath("settings.json.bak"), legacy);
        var migrated = new CompanionSettings
        {
            CompanionName = "Recover me", AlwaysOnTop = true, ReducedMotion = true
        };
        var saved = migrated with { AvatarScale = 1.5, AvatarOpacity = 0.4 };

        using (var store = new JsonSettingsStore(directory.PathValue))
        {
            var recovered = await store.LoadAsync(CancellationToken.None);
            Assert.AreEqual(SettingsLoadState.RecoveredBackup, recovered.State);
            Assert.AreEqual(migrated, recovered.Settings);
            Assert.IsTrue(recovered.CanSave);
            Assert.AreEqual(damaged, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
            Assert.AreEqual(legacy, await File.ReadAllTextAsync(directory.FilePath("settings.json.bak")));

            await store.SaveAsync(saved, CancellationToken.None);
        }

        Assert.AreEqual(damaged, await File.ReadAllTextAsync(directory.FilePath("settings.json.invalid")));
        var backup = JsonSerializer.Deserialize<CompanionSettings>(
            await File.ReadAllTextAsync(directory.FilePath("settings.json.bak")));
        Assert.AreEqual(migrated, backup);
        using var restarted = new JsonSettingsStore(directory.PathValue);
        var loaded = await restarted.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.Loaded, loaded.State);
        Assert.AreEqual(saved, loaded.Settings);
    }

    [TestMethod]
    [DataRow("\"AvatarScale\":1,\"AvatarScale\":1.5")]
    [DataRow("\"AvatarOpacity\":null")]
    [DataRow("\"AvatarScale\":\"1.25\"")]
    [DataRow("\"AvatarOpacity\":true")]
    [DataRow("\"avatarscale\":1.25")]
    public async Task MalformedAppearanceRecoversLegacyBackupWithoutAlteringEitherFile(string fields)
    {
        using var directory = new TestDirectory();
        var malformed = "{\"SchemaVersion\":2,\"CompanionName\":\"Untrusted\"," + fields + "}";
        const string legacy = "{\"SchemaVersion\":1,\"CompanionName\":\"Trusted\"}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), malformed);
        await File.WriteAllTextAsync(directory.FilePath("settings.json.bak"), legacy);
        using var store = new JsonSettingsStore(directory.PathValue);

        var recovered = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.RecoveredBackup, recovered.State);
        Assert.AreEqual(new CompanionSettings { CompanionName = "Trusted" }, recovered.Settings);
        Assert.IsTrue(recovered.CanSave);
        Assert.AreEqual(malformed, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
        Assert.AreEqual(legacy, await File.ReadAllTextAsync(directory.FilePath("settings.json.bak")));
        Assert.HasCount(2, Directory.GetFiles(directory.PathValue));
    }
}
