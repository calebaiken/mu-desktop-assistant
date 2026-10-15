using System.Text.Json;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Infrastructure.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class AppearanceSettingsTests
{
    [TestMethod]
    [DataRow(0.75, 0.4)]
    [DataRow(1.25, 0.85)]
    [DataRow(1.5, 1.0)]
    public async Task AppearancePreferencesRoundTripIncludingBoundaryValues(double scale, double opacity)
    {
        using var directory = new TestDirectory();
        var expected = new CompanionSettings
        {
            CompanionName = "Nova",
            AvatarScale = scale,
            AvatarOpacity = opacity,
            ReducedMotion = true
        };
        using (var store = new JsonSettingsStore(directory.PathValue))
        {
            await store.LoadAsync(CancellationToken.None);
            await store.SaveAsync(expected, CancellationToken.None);
        }

        using var restarted = new JsonSettingsStore(directory.PathValue);
        var actual = await restarted.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.Loaded, actual.State);
        Assert.AreEqual(expected, actual.Settings);
        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(directory.FilePath("settings.json")));
        Assert.AreEqual(CompanionSettings.CurrentVersion, saved.RootElement.GetProperty("SchemaVersion").GetInt32());
    }

    [TestMethod]
    public async Task LegacySettingsMigrateInMemoryWithoutWritingAndSaveKnownGoodBackup()
    {
        using var directory = new TestDirectory();
        const string original = "{\"SchemaVersion\":1,\"CompanionName\":\"Legacy\",\"AlwaysOnTop\":true,\"ReducedMotion\":true,\"DiagnosticsEnabled\":true}";
        var primaryPath = directory.FilePath("settings.json");
        await File.WriteAllTextAsync(primaryPath, original);
        using var store = new JsonSettingsStore(directory.PathValue);

        var migrated = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.Loaded, migrated.State);
        Assert.IsTrue(migrated.CanSave);
        Assert.AreEqual(new CompanionSettings
        {
            CompanionName = "Legacy", AlwaysOnTop = true, ReducedMotion = true, DiagnosticsEnabled = true,
            AvatarPreset = AvatarPreset.Classic
        }, migrated.Settings);
        Assert.AreEqual(original, await File.ReadAllTextAsync(primaryPath));
        Assert.HasCount(1, Directory.GetFiles(directory.PathValue));

        await store.SaveAsync(migrated.Settings with { AvatarScale = 1.5, AvatarOpacity = 0.4 }, CancellationToken.None);
        using var backup = JsonDocument.Parse(await File.ReadAllTextAsync(directory.FilePath("settings.json.bak")));
        Assert.AreEqual(CompanionSettings.CurrentVersion, backup.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.AreEqual("Legacy", backup.RootElement.GetProperty("CompanionName").GetString());
        Assert.AreEqual(1.0, backup.RootElement.GetProperty("AvatarScale").GetDouble());
        Assert.AreEqual(1.0, backup.RootElement.GetProperty("AvatarOpacity").GetDouble());
        Assert.IsFalse(File.Exists(directory.FilePath("settings.json.invalid")));

        await File.WriteAllTextAsync(primaryPath, "{");
        using var restarted = new JsonSettingsStore(directory.PathValue);
        var recovered = await restarted.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.RecoveredBackup, recovered.State);
        Assert.AreEqual(migrated.Settings, recovered.Settings);
    }

    [TestMethod]
    public async Task LegacySettingsWithoutOptionalFieldsReceiveSafeDefaults()
    {
        using var directory = new TestDirectory();
        const string original = "{\"SchemaVersion\":1}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), original);
        using var store = new JsonSettingsStore(directory.PathValue);

        var migrated = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.Loaded, migrated.State);
        Assert.AreEqual(new CompanionSettings { CompanionName = "Pip", AvatarPreset = AvatarPreset.Classic }, migrated.Settings);
        Assert.AreEqual(original, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    [DataRow("AvatarScale", "1.2")]
    [DataRow("AvatarOpacity", "0.5")]
    [DataRow("Unknown", "true")]
    public async Task LegacySchemaRejectsNewAndUnknownFields(string property, string value)
    {
        using var directory = new TestDirectory();
        var original = "{\"SchemaVersion\":1,\"CompanionName\":\"Legacy\",\"" + property + "\":" + value + "}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), original);
        using var store = new JsonSettingsStore(directory.PathValue);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.ResetInvalid, loaded.State);
        Assert.AreEqual(new CompanionSettings(), loaded.Settings);
        Assert.AreEqual(original, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task ExternalLegacyEditAfterMigrationIsNotOverwritten()
    {
        using var directory = new TestDirectory();
        var primaryPath = directory.FilePath("settings.json");
        await File.WriteAllTextAsync(primaryPath, "{\"SchemaVersion\":1,\"CompanionName\":\"Original\"}");
        using var store = new JsonSettingsStore(directory.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        const string external = "{\"SchemaVersion\":1,\"CompanionName\":\"External edit\"}";
        await File.WriteAllTextAsync(primaryPath, external);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            store.SaveAsync(loaded.Settings with { AvatarScale = 1.5 }, CancellationToken.None));

        Assert.AreEqual(external, await File.ReadAllTextAsync(primaryPath));
        Assert.IsFalse(File.Exists(directory.FilePath("settings.json.bak")));
    }

    [TestMethod]
    [DataRow(0.749, 1.0)]
    [DataRow(1.501, 1.0)]
    [DataRow(1.0, 0.399)]
    [DataRow(1.0, 1.001)]
    [DataRow(double.NaN, 1.0)]
    [DataRow(double.PositiveInfinity, 1.0)]
    [DataRow(double.NegativeInfinity, 1.0)]
    [DataRow(1.0, double.NaN)]
    [DataRow(1.0, double.PositiveInfinity)]
    [DataRow(1.0, double.NegativeInfinity)]
    public async Task InvalidAppearanceValuesCannotBeSaved(double scale, double opacity)
    {
        using var directory = new TestDirectory();
        using var store = new JsonSettingsStore(directory.PathValue);
        await store.LoadAsync(CancellationToken.None);
        var invalid = new CompanionSettings { AvatarScale = scale, AvatarOpacity = opacity };

        Assert.IsNotEmpty(invalid.Validate());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(invalid, CancellationToken.None));
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue));
    }

    [TestMethod]
    [DataRow("AvatarScale", "0.74")]
    [DataRow("AvatarScale", "1.51")]
    [DataRow("AvatarOpacity", "0.39")]
    [DataRow("AvatarOpacity", "1.01")]
    [DataRow("AvatarScale", "1e400")]
    [DataRow("AvatarOpacity", "\"NaN\"")]
    public async Task InvalidStoredAppearanceUsesVisibleRecoveryWithoutRewriting(string property, string value)
    {
        using var directory = new TestDirectory();
        var original = "{\"SchemaVersion\":2,\"" + property + "\":" + value + "}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), original);
        using var store = new JsonSettingsStore(directory.PathValue);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.ResetInvalid, loaded.State);
        Assert.AreEqual(new CompanionSettings(), loaded.Settings);
        Assert.AreEqual(original, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task NextSchemaRemainsReadOnlyDespiteValidLegacyBackup()
    {
        using var directory = new TestDirectory();
        var future = "{\"SchemaVersion\":" + (CompanionSettings.CurrentVersion + 1) + ",\"CompanionName\":\"Future\"}";
        await File.WriteAllTextAsync(directory.FilePath("settings.json"), future);
        await File.WriteAllTextAsync(directory.FilePath("settings.json.bak"), "{\"SchemaVersion\":1}");
        using var store = new JsonSettingsStore(directory.PathValue);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(SettingsLoadState.UnsupportedVersion, loaded.State);
        Assert.IsFalse(loaded.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(new(), CancellationToken.None));
        Assert.AreEqual(future, await File.ReadAllTextAsync(directory.FilePath("settings.json")));
    }
}
