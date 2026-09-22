using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Core.Desktop;
using Companion.Infrastructure.Storage;

namespace Companion.Infrastructure.Desktop;

public enum PlacementLoadState { New, Loaded, Invalid, UnsupportedVersion, TooLarge }
public sealed record PlacementLoadResult(SavedPlacement? Placement, PlacementLoadState State, bool CanSave);

/// <summary>Separate bounded persistence so moving the companion cannot overwrite preferences.</summary>
public sealed class JsonPlacementStore : IDisposable
{
    private const int MaximumBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4
    };
    private readonly string path;
    private readonly IAtomicFileWriter writer;
    private readonly SemaphoreSlim gate = new(1);
    private bool loaded;
    private bool canSave;
    private SavedPlacement? knownGood;

    public JsonPlacementStore(string directory, IAtomicFileWriter? writer = null)
    {
        path = Path.Combine(Path.GetFullPath(directory), "placement.json");
        this.writer = writer ?? new AtomicFileWriter();
    }

    public async Task<PlacementLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            loaded = false;
            canSave = false;
            knownGood = null;
            var result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                return Read();
            }, cancellationToken).ConfigureAwait(false);
            loaded = true;
            canSave = result.CanSave;
            knownGood = result.Placement;
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(SavedPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Validate().Count != 0) throw new ArgumentException("Invalid placement.", nameof(placement));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!loaded || !canSave) throw new InvalidOperationException("Placement must be loaded from a writable schema before saving.");
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = Read();
                if (!current.CanSave) throw new InvalidOperationException("Placement schema changed. Restart before saving.");
                if (current.Placement is not null && current.Placement != knownGood)
                    throw new InvalidOperationException("Placement changed in another program. Restart before saving.");
                if (current.Placement == placement) return;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(placement, JsonOptions);
                if (bytes.Length > MaximumBytes) throw new ArgumentException("Placement is too large.", nameof(placement));
                // Keep a single malformed original without copying or reading unbounded bytes.
                writer.Write(path, current.State == PlacementLoadState.Invalid ? path + ".invalid" : null, bytes);
                knownGood = placement;
                // Cancellation after the atomic commit must not report a rolled-back save.
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private PlacementLoadResult Read()
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // An oversized document cannot safely be classified by schema, so preserve it read-only.
            if (stream.Length > MaximumBytes) return new(null, PlacementLoadState.TooLarge, false);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Invalid();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var duplicate = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                duplicate |= !names.Add(property.Name);
                // Preserve a future version even when another duplicate version appears later.
                if (property.Name == "SchemaVersion" && property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetInt32(out var candidate) && candidate != SavedPlacement.CurrentVersion)
                    return new(null, PlacementLoadState.UnsupportedVersion, false);
            }
            if (duplicate || names.Count != 4 ||
                !names.SetEquals(["SchemaVersion", "MonitorDeviceId", "HorizontalFraction", "VerticalFraction"])) return Invalid();
            if (!document.RootElement.TryGetProperty("SchemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) ||
                version != SavedPlacement.CurrentVersion) return Invalid();
            var placement = document.Deserialize<SavedPlacement>(JsonOptions);
            return placement is not null && placement.Validate().Count == 0
                ? new(placement, PlacementLoadState.Loaded, true) : Invalid();
        }
        catch (FileNotFoundException) { return new(null, PlacementLoadState.New, true); }
        catch (DirectoryNotFoundException) { return new(null, PlacementLoadState.New, true); }
        catch (JsonException) { return Invalid(); }
        // I/O and permission failures remain visible to the caller.
    }

    private static PlacementLoadResult Invalid() => new(null, PlacementLoadState.Invalid, true);
    public void Dispose() => gate.Dispose();
}
