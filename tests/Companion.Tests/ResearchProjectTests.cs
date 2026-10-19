using System.Text.Json;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;
using Companion.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class ResearchProjectTests
{
    private readonly string profile = Path.Combine(Path.GetTempPath(), "MuResearchAuthor", Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(profile, "ml-lab", "research-projects.json");
    [TestCleanup] public void Cleanup() { if (Directory.Exists(profile)) Directory.Delete(profile, true); }

    [TestMethod]
    public async Task ProjectsSurviveReopenWithDistinctIdentityAndNoImplicitDatasetWork()
    {
        var store = new JsonResearchProjectStore(profile);
        Assert.AreEqual(0, (await store.ListAsync()).Count);
        Assert.IsFalse(File.Exists(Catalog));
        var first = await store.CreateAsync("Particle study");
        var second = await store.CreateAsync("Particle study");
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(first.Directory, second.Directory);
        Assert.IsFalse(Directory.Exists(first.Directory), "Creating metadata must not run a worker or create analysis artifacts.");
        var reopened = new JsonResearchProjectStore(profile);
        Assert.AreEqual(first, await reopened.OpenAsync(first.Id));
        Assert.AreEqual(2, (await reopened.ListAsync()).Count);
        var forged = first with { Directory = Path.GetTempPath(), Name = "Forged" };
        Assert.AreEqual(first, await reopened.OpenAsync(forged.Id), "Only the catalog identity resolves storage.");
        using var backup = JsonDocument.Parse(await File.ReadAllBytesAsync(Catalog + ".bak"));
        Assert.AreEqual(first.Id, backup.RootElement.GetProperty("projects")[0].GetProperty("id").GetGuid());
    }

    [TestMethod]
    public async Task ExistingDefaultProjectRemainsAtOriginalLocationAndIsNeverCreatedImplicitly()
    {
        var store = new JsonResearchProjectStore(profile);
        await Assert.ThrowsAsync<MlException>(() => store.OpenAsync(ResearchProjects.LegacyId));
        var legacy = Path.Combine(profile, "ml-lab", "default-project");
        Directory.CreateDirectory(legacy);
        var receipt = Path.Combine(legacy, "private-sentinel.json");
        await File.WriteAllTextAsync(receipt, "unchanged original artifacts");
        var project = (await store.ListAsync()).Single();
        Assert.IsTrue(project.IsLegacy);
        Assert.AreEqual(legacy, project.Directory);
        Assert.AreEqual(project, await store.OpenAsync(ResearchProjects.LegacyId));
        await store.CreateAsync("Separate research");
        Assert.AreEqual("unchanged original artifacts", await File.ReadAllTextAsync(receipt));
        Assert.AreEqual(1, Directory.GetFiles(legacy).Length);
    }

    [TestMethod]
    public async Task FailedCatalogCommitPreservesOldMetadataAndDoesNotClaimCreation()
    {
        var writer = new FailingWriter();
        var store = new JsonResearchProjectStore(profile, writer);
        var first = await store.CreateAsync("Known project");
        var before = await File.ReadAllBytesAsync(Catalog);
        writer.Fail = true;
        var error = await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("Never committed"));
        Assert.AreEqual("project_access", error.Code);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(Catalog));
        Assert.AreEqual(first, (await new JsonResearchProjectStore(profile).ListAsync()).Single());
    }

    [TestMethod]
    public async Task CancellationAndCapacityDoNotCreateUnapprovedProjectMetadata()
    {
        var store = new JsonResearchProjectStore(profile);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => store.CreateAsync("Cancelled", cancelled.Token));
        Assert.IsFalse(Directory.Exists(profile));
        for (var index = 0; index < ResearchProjects.MaximumProjects; index++) await store.CreateAsync($"Project {index}");
        var before = await File.ReadAllBytesAsync(Catalog);
        var error = await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("Beyond capacity"));
        Assert.AreEqual("project_limit", error.Code);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(Catalog));
    }

    [TestMethod]
    [DataRow("{\"version\":2,\"projects\":[]}")]
    [DataRow("{\"version\":1,\"projects\":[],\"path\":\"elsewhere\"}")]
    [DataRow("{\"version\":1,\"version\":1,\"projects\":[]}")]
    [DataRow("{\"version\":1}")]
    [DataRow("{\"version\":1,\"projects\":[null]}")]
    [DataRow("unreadable catalog")]
    public async Task InvalidOrFutureMetadataIsNotOverwrittenOrSilentlyReplaced(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Catalog)!);
        await File.WriteAllTextAsync(Catalog, json);
        var store = new JsonResearchProjectStore(profile);
        await Assert.ThrowsAsync<MlException>(() => store.ListAsync());
        await Assert.ThrowsAsync<MlException>(() => store.CreateAsync("Refused replacement"));
        Assert.AreEqual(json, await File.ReadAllTextAsync(Catalog));
    }

    [TestMethod]
    public async Task RealMissingRuntimeFailureSettlesAdmissionAndStaleProjectCannotExecute()
    {
        var store = new JsonResearchProjectStore(profile);
        var first = await store.CreateAsync("A"); var second = await store.CreateAsync("B");
        await using var workspace = new ResearchLabWorkspace(store, project => new LocalMlLabService(
            Path.Combine(profile, "missing", "python.exe"), Path.Combine(profile, "missing", "worker.py"), project.Directory));
        var selected = await workspace.OpenAsync(first.Id);
        var error = await Assert.ThrowsAsync<MlException>(() => selected.Service.ExecuteAsync(new MlList()));
        Assert.AreEqual("missing_runtime", error.Code);
        Assert.IsFalse(workspace.IsBusy);
        var next = await workspace.OpenAsync(second.Id);
        var stale = await Assert.ThrowsAsync<MlException>(() => selected.Service.ExecuteAsync(new MlList()));
        Assert.AreEqual("stale_project", stale.Code);
        Assert.AreEqual(second.Directory, next.Service.ProjectDirectory);
        Assert.AreEqual(0, next.Service.Jobs.Count);
    }

    private sealed class FailingWriter : IAtomicFileWriter
    {
        internal bool Fail { get; set; }
        public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents)
        {
            if (Fail) throw new IOException("Synthetic disk exhaustion before commit.");
            new AtomicFileWriter().Write(targetPath, backupPath, contents);
        }
    }
}
