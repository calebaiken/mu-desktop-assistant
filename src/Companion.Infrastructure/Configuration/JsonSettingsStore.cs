using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.Configuration;
using Companion.Core.Configuration;
using Companion.Infrastructure.Storage;

namespace Companion.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
    private readonly string path;
    private readonly IAtomicFileWriter writer;
    private readonly SemaphoreSlim gate = new(1);
    private bool loaded;
    private bool canSave;
    private CompanionSettings? knownGood;

    public JsonSettingsStore(string directory, IAtomicFileWriter? writer = null)
    {
        path = Path.Combine(Path.GetFullPath(directory), "settings.json");
        this.writer = writer ?? new AtomicFileWriter();
    }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken)
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
                var primary = Read(path);
                if (primary.Unsupported)
                    return new SettingsLoadResult(new(), SettingsLoadState.UnsupportedVersion, false);
                if (primary.Settings is not null)
                    return new SettingsLoadResult(primary.Settings, SettingsLoadState.Loaded, true);
                var backup = Read(path + ".bak");
                if (backup.Unsupported)
                {
                    return new SettingsLoadResult(new(), SettingsLoadState.UnsupportedVersion, false);
                }
                if (backup.Settings is not null)
                    return new SettingsLoadResult(backup.Settings, SettingsLoadState.RecoveredBackup, true);
                return new SettingsLoadResult(new(), primary.Exists || backup.Exists
                    ? SettingsLoadState.ResetInvalid : SettingsLoadState.New, true);
            }, cancellationToken).ConfigureAwait(false);
            loaded = true;
            canSave = result.CanSave;
            knownGood = result.Settings;
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(CompanionSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Validate().Count != 0) throw new ArgumentException("Invalid companion settings.", nameof(settings));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!loaded || !canSave) throw new InvalidOperationException("Settings must be loaded from a supported schema before saving.");
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = Read(path);
                if (current.Unsupported)
                    throw new InvalidOperationException("Settings schema changed. Restart before saving.");
                if (current.Settings is not null && current.Settings != knownGood)
                    throw new InvalidOperationException("Settings changed in another program. Restart before saving.");
                var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
                if (bytes.Length > MaximumBytes) throw new ArgumentException("Settings are too large.", nameof(settings));
                // Back up the validated in-memory version, never reread untrusted bytes as a backup.
                writer.Write(path + ".bak", null, JsonSerializer.SerializeToUtf8Bytes(knownGood!, JsonOptions));
                // File.Replace moves a corrupt/oversized original into a single retained quarantine.
                // No unbounded File.Copy; the target remains intact if commit fails.
                writer.Write(path, current.Exists && current.Settings is null ? path + ".invalid" : null, bytes);
                knownGood = settings;
                // Cancellation is checked before commit; a committed save reports success.
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static (CompanionSettings? Settings, bool Exists, bool Unsupported) Read(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumBytes) return (null, true, false);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (null, true, false);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) return (null, true, false);
            if (!document.RootElement.TryGetProperty("SchemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version)) return (null, true, false);
            if (version != CompanionSettings.CurrentVersion) return (null, true, true);
            var settings = document.Deserialize<CompanionSettings>(JsonOptions);
            return settings is not null && settings.Validate().Count == 0
                ? (settings, true, false) : (null, true, false);
        }
        catch (FileNotFoundException) { return (null, false, false); }
        catch (DirectoryNotFoundException) { return (null, false, false); }
        catch (JsonException) { return (null, true, false); }
        // I/O/permission failures propagate to visible application recovery, not defaults.
    }

    public void Dispose() => gate.Dispose();
}
