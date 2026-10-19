using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.Storage;

namespace Companion.Infrastructure.MachineLearning;

/// <summary>Small versioned metadata catalog; no dataset, chat or model content.</summary>
public sealed class JsonResearchProjectStore : IResearchProjectStore
{
    public const int MaximumCatalogBytes = 64 * 1024;
    private readonly string root, path;
    private readonly IAtomicFileWriter writer;
    private readonly SemaphoreSlim gate = new(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { MaxDepth = 6, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record Entry(Guid Id, string Name, DateTimeOffset CreatedAt);
    private sealed record Catalog(int Version, Entry[] Projects);

    public JsonResearchProjectStore(string profileDirectory, IAtomicFileWriter? writer = null)
    {
        root = LocalMlLabService.ValidatePath(Path.Combine(LocalMlLabService.ValidatePath(profileDirectory), "ml-lab"));
        path = Path.Combine(root, "research-projects.json");
        this.writer = writer ?? new AtomicFileWriter();
    }

    public Task<IReadOnlyList<ResearchProject>> ListAsync(CancellationToken cancellationToken = default) => RunAsync<IReadOnlyList<ResearchProject>>(() =>
    {
        var result = Read().Projects.Select(Project).ToList();
        var legacy = Path.Combine(root, "default-project");
        LocalMlLabService.ValidatePath(legacy);
        if (Directory.Exists(legacy)) result.Insert(0, new(ResearchProjects.LegacyId, "Existing default project", legacy, DateTimeOffset.UnixEpoch, true));
        return result.AsReadOnly();
    }, cancellationToken);

    public Task<ResearchProject> OpenAsync(Guid id, CancellationToken cancellationToken = default) => RunAsync(() =>
    {
        var catalog = Read();
        if (id == ResearchProjects.LegacyId)
        {
            var legacy = LocalMlLabService.ValidatePath(Path.Combine(root, "default-project"));
            if (Directory.Exists(legacy)) return new(id, "Existing default project", legacy, DateTimeOffset.UnixEpoch, true);
        }
        else if (catalog.Projects.SingleOrDefault(item => item.Id == id) is { } entry) return Project(entry);
        throw new MlException("project_missing", "The selected research project is unavailable. Refresh the project list.");
    }, cancellationToken);

    public Task<ResearchProject> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        ResearchProjects.ValidateName(name);
        return RunAsync(() =>
        {
            var catalog = Read();
            if (catalog.Projects.Length >= ResearchProjects.MaximumProjects)
                throw new MlException("project_limit", "The research catalog is limited to 32 projects.");
            var entry = new Entry(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
            var project = Project(entry);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Catalog(1, [.. catalog.Projects, entry]), Json);
            if (bytes.Length > MaximumCatalogBytes) throw Invalid();
            LocalMlLabService.ValidatePath(path + ".bak");
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(path, path + ".bak", bytes);
            // Once committed, return success even if cancellation arrived during the atomic write.
            return project;
        }, cancellationToken);
    }

    private ResearchProject Project(Entry entry) => new(entry.Id, entry.Name,
        LocalMlLabService.ValidatePath(Path.Combine(root, "projects", entry.Id.ToString("N"))), entry.CreatedAt);

    private Catalog Read()
    {
        LocalMlLabService.ValidatePath(path);
        if (!File.Exists(path))
        {
            LocalMlLabService.ValidatePath(path + ".bak");
            var projectRoot = LocalMlLabService.ValidatePath(Path.Combine(root, "projects"));
            if (Directory.Exists(path) || File.Exists(path + ".bak") || Directory.Exists(projectRoot)) throw Invalid();
            return new(1, []);
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumCatalogBytes) throw Invalid();
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw Invalid();
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 6 });
        RejectDuplicateFields(document.RootElement);
        var catalog = JsonSerializer.Deserialize<Catalog>(bytes, Json);
        if (catalog is not { Version: 1, Projects: not null } || catalog.Projects.Length > ResearchProjects.MaximumProjects ||
            catalog.Projects.Any(item => item is null || item.Id == ResearchProjects.LegacyId || item.CreatedAt == default || item.CreatedAt.Offset != TimeSpan.Zero) ||
            catalog.Projects.Select(item => item.Id).Distinct().Count() != catalog.Projects.Length) throw Invalid();
        foreach (var entry in catalog.Projects) ResearchProjects.ValidateName(entry.Name);
        return catalog;
    }

    private static void RejectDuplicateFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in value.EnumerateObject())
            {
                if (!names.Add(field.Name)) throw Invalid();
                RejectDuplicateFields(field.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateFields(item);
    }

    private async Task<T> RunAsync<T>(Func<T> action, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    LocalMlLabService.ValidatePath(root);
                    Directory.CreateDirectory(root);
                    var lockPath = LocalMlLabService.ValidatePath(Path.Combine(root, "research-projects.lock"));
                    using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return action();
                }
                catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
                { throw Invalid(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { throw new MlException("project_access", "Research project metadata could not be accessed. Check storage and folder access, then refresh."); }
            }, token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static MlException Invalid() => new("project_catalog", "Research project metadata is invalid or from an unsupported version. It was not overwritten; preserve the catalog and its backup for recovery.");
}
