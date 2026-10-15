using System.Text.Json;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Infrastructure.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Reviewer-owned migration and appearance authority checks.</summary>
[TestClass]
public sealed class IndependentAvatarCustomizationTests
{
    [TestMethod]
    public async Task EveryPresetAndAccentRoundTripsWithoutChangingUnrelatedPreferences()
    {
        foreach (var preset in Enum.GetValues<AvatarPreset>())
        foreach (var accent in Enum.GetValues<AvatarAccent>())
        foreach (var decoration in Enum.GetValues<AvatarDecoration>())
        foreach (var face in Enum.GetValues<AvatarFace>())
        {
            using var files = new TestDirectory();
            var expected = new CompanionSettings
            {
                CompanionName = "Existing identity", AlwaysOnTop = true, DiagnosticsEnabled = true,
                ReducedMotion = true, AvatarScale = 1.25, AvatarOpacity = .65,
                SummonShortcut = SummonHotkey.CtrlAltSpace, AvatarPreset = preset, AvatarAccent = accent,
                AvatarDecoration = decoration, AvatarFace = face
            };
            using (var store = new JsonSettingsStore(files.PathValue))
            {
                await store.LoadAsync(CancellationToken.None);
                await store.SaveAsync(expected, CancellationToken.None);
            }
            using var restarted = new JsonSettingsStore(files.PathValue);
            Assert.AreEqual(expected, (await restarted.LoadAsync(CancellationToken.None)).Settings);
            using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(files.FilePath("settings.json")));
            Assert.AreEqual(3, saved.RootElement.GetProperty("SchemaVersion").GetInt32());
        }
    }

    [TestMethod]
    public async Task VersionTwoKeepsClassicAppearanceAndExistingPreferencesWithoutAutomaticWrite()
    {
        using var files = new TestDirectory();
        const string original = "{\"SchemaVersion\":2,\"CompanionName\":\"Legacy Mu\",\"AlwaysOnTop\":true,\"ReducedMotion\":true,\"AvatarScale\":1.35,\"AvatarOpacity\":0.7,\"DiagnosticsEnabled\":true,\"SummonShortcut\":0}";
        await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
        using var store = new JsonSettingsStore(files.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.Loaded, loaded.State);
        Assert.IsTrue(loaded.CanSave);
        Assert.AreEqual(3, loaded.Settings.SchemaVersion);
        Assert.AreEqual(AvatarPreset.Classic, loaded.Settings.AvatarPreset);
        Assert.AreEqual(AvatarAccent.Teal, loaded.Settings.AvatarAccent);
        Assert.AreEqual("Legacy Mu", loaded.Settings.CompanionName);
        Assert.AreEqual(1.35, loaded.Settings.AvatarScale);
        Assert.AreEqual(.7, loaded.Settings.AvatarOpacity);
        Assert.IsTrue(loaded.Settings.AlwaysOnTop && loaded.Settings.ReducedMotion && loaded.Settings.DiagnosticsEnabled);
        Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
        Assert.HasCount(1, Directory.GetFiles(files.PathValue));
        await store.SaveAsync(loaded.Settings with { AvatarPreset = AvatarPreset.Mu, AvatarAccent = AvatarAccent.Violet }, CancellationToken.None);
        using var backup = JsonDocument.Parse(await File.ReadAllTextAsync(files.FilePath("settings.json.bak")));
        Assert.AreEqual(3, backup.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.AreEqual((int)AvatarPreset.Classic, backup.RootElement.GetProperty("AvatarPreset").GetInt32());
        Assert.AreEqual(1.35, backup.RootElement.GetProperty("AvatarScale").GetDouble());
    }

    [TestMethod]
    public async Task VersionOneMinimalMigrationIsClassicWhileNewProfileDefaultsToMu()
    {
        using var files = new TestDirectory();
        await File.WriteAllTextAsync(files.FilePath("settings.json"), "{\"SchemaVersion\":1}");
        using var store = new JsonSettingsStore(files.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(AvatarPreset.Classic, loaded.Settings.AvatarPreset);
        Assert.AreEqual(AvatarAccent.Teal, loaded.Settings.AvatarAccent);
        Assert.AreEqual(1d, loaded.Settings.AvatarOpacity);
        Assert.AreEqual(1d, loaded.Settings.AvatarScale);
        Assert.AreEqual(AvatarPreset.Mu, new CompanionSettings().AvatarPreset);
        Assert.AreEqual("Pip", loaded.Settings.CompanionName);
        Assert.AreEqual("Mu", new CompanionSettings().CompanionName);
        Assert.AreEqual("{\"SchemaVersion\":1}", await File.ReadAllTextAsync(files.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task LegacyMissingNameRetainsOriginalDefaultAcrossBothAcceptedVersions()
    {
        foreach (var version in new[] { 1, 2 })
        {
            using var files = new TestDirectory();
            var original = $"{{\"SchemaVersion\":{version}}}";
            await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
            using var store = new JsonSettingsStore(files.PathValue);
            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.AreEqual("Pip", loaded.Settings.CompanionName);
            Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
        }
    }

    [TestMethod]
    public async Task NewAppearanceFieldsCannotBeSmuggledIntoLegacySchemas()
    {
        foreach (var version in new[] { 1, 2 })
        foreach (var property in new[] { "AvatarPreset", "AvatarAccent", "AvatarDecoration", "AvatarFace" })
        {
            using var files = new TestDirectory();
            var original = $"{{\"SchemaVersion\":{version},\"{property}\":0}}";
            await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
            using var store = new JsonSettingsStore(files.PathValue);
            Assert.AreEqual(SettingsLoadState.ResetInvalid, (await store.LoadAsync(CancellationToken.None)).State);
            Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
        }
    }

    [TestMethod]
    public async Task InvalidNumericEnumValuesCannotBeSavedOrLoaded()
    {
        foreach (var invalid in new[] { -1, int.MaxValue, 99 })
        {
            using var files = new TestDirectory();
            using var store = new JsonSettingsStore(files.PathValue);
            await store.LoadAsync(CancellationToken.None);
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new() { AvatarPreset = (AvatarPreset)invalid }, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new() { AvatarAccent = (AvatarAccent)invalid }, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new() { AvatarDecoration = (AvatarDecoration)invalid }, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(new() { AvatarFace = (AvatarFace)invalid }, CancellationToken.None));
            Assert.IsEmpty(Directory.GetFiles(files.PathValue));
            foreach (var property in new[] { "AvatarPreset", "AvatarAccent", "AvatarDecoration", "AvatarFace" })
            {
                var original = $"{{\"SchemaVersion\":3,\"{property}\":{invalid}}}";
                await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
                using var reader = new JsonSettingsStore(files.PathValue);
                Assert.AreEqual(SettingsLoadState.ResetInvalid, (await reader.LoadAsync(CancellationToken.None)).State);
                Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
            }
        }
    }

    [TestMethod]
    public async Task ArbitraryAppearanceTextAndDuplicateFieldsAreInertInvalidData()
    {
        foreach (var values in new[]
        {
            "\"AvatarPreset\":\"C:/untrusted.xaml\"", "\"AvatarAccent\":\"#00ff00\"",
            "\"AvatarAccent\":0,\"AvatarAccent\":1", "\"AvatarPreset\":{}", "\"AvatarAccent\":null",
            "\"AvatarDecoration\":\"external.xaml\"", "\"AvatarFace\":true", "\"AvatarFace\":0,\"AvatarFace\":1"
        })
        {
            using var files = new TestDirectory();
            var original = "{\"SchemaVersion\":3," + values + "}";
            await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
            using var store = new JsonSettingsStore(files.PathValue);
            Assert.AreEqual(SettingsLoadState.ResetInvalid, (await store.LoadAsync(CancellationToken.None)).State);
            Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
        }
    }

    [TestMethod]
    public async Task FutureSchemaStaysReadOnlyEvenWithAValidClassicBackup()
    {
        using var files = new TestDirectory();
        var original = $"{{\"SchemaVersion\":{CompanionSettings.CurrentVersion + 1},\"CompanionName\":\"Future\"}}";
        await File.WriteAllTextAsync(files.FilePath("settings.json"), original);
        await File.WriteAllTextAsync(files.FilePath("settings.json.bak"), "{\"SchemaVersion\":2}");
        using var store = new JsonSettingsStore(files.PathValue);
        var result = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual(SettingsLoadState.UnsupportedVersion, result.State);
        Assert.IsFalse(result.CanSave);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(new(), CancellationToken.None));
        Assert.AreEqual(original, await File.ReadAllTextAsync(files.FilePath("settings.json")));
    }

    [TestMethod]
    public async Task ExternalLegacyEditCannotBeOverwrittenByAppearanceSave()
    {
        using var files = new TestDirectory();
        await File.WriteAllTextAsync(files.FilePath("settings.json"), "{\"SchemaVersion\":2,\"CompanionName\":\"Original\"}");
        using var store = new JsonSettingsStore(files.PathValue);
        var loaded = await store.LoadAsync(CancellationToken.None);
        const string external = "{\"SchemaVersion\":2,\"CompanionName\":\"Externally changed\"}";
        await File.WriteAllTextAsync(files.FilePath("settings.json"), external);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.SaveAsync(loaded.Settings with { AvatarAccent = AvatarAccent.Blue }, CancellationToken.None));
        Assert.AreEqual(external, await File.ReadAllTextAsync(files.FilePath("settings.json")));
        Assert.IsFalse(File.Exists(files.FilePath("settings.json.bak")));
    }
}
