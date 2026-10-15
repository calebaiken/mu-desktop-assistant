using System.Text.Json;
using Companion.Application.Voice;
using Companion.Infrastructure.Storage;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Only mute and explicitly selected endpoint identity persist. Audio and drafts have no storage path.</summary>
public sealed class JsonVoiceInputPreferences : IVoiceInputPreferencesStore
{
    private readonly string directory;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IAtomicFileWriter writer;
    private const int MaximumFileBytes = 4096;

    public JsonVoiceInputPreferences(string dataDirectory, IAtomicFileWriter? writer = null)
    {
        if (!Path.IsPathFullyQualified(dataDirectory) || dataDirectory.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("An absolute local profile is required.", nameof(dataDirectory));
        directory = Path.GetFullPath(dataDirectory);
        path = Path.Combine(directory, "voice-input.json");
        this.writer = writer ?? new AtomicFileWriter();
    }

    public async Task<VoiceInputPreferencesResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidatePath();
            return new(await ReadAsync(cancellationToken).ConfigureAwait(false), true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // Preserve invalid/future data. A corrupt preference cannot enable recording.
            return new(new(), false);
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(VoiceInputPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.DeviceId is not null && !AudioInputLimits.IsValidDeviceId(preferences.DeviceId))
            throw new ArgumentException("Invalid microphone preference.", nameof(preferences));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidatePath();
            _ = await ReadAsync(cancellationToken).ConfigureAwait(false); // Never overwrite malformed/future state.
            var contents = JsonSerializer.SerializeToUtf8Bytes(new
            { schemaVersion = 1, microphoneMuted = preferences.MicrophoneMuted, deviceId = preferences.DeviceId });
            if (contents.Length > MaximumFileBytes) throw new IOException("Microphone preferences exceed their bound.");
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePath();
                Directory.CreateDirectory(directory);
                writer.Write(path, null, contents);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task<VoiceInputPreferences> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(path)) return new();
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("Invalid microphone preference file.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            MaximumFileBytes, FileOptions.Asynchronous);
        if (stream.Length is <= 0 or > MaximumFileBytes) throw new IOException("Invalid microphone preference size.");
        using var document = await JsonDocument.ParseAsync(stream, new() { MaxDepth = 3 }, token).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
        var fields = root.EnumerateObject().ToArray();
        if (fields.Length != 3 || fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != 3 ||
            fields.Any(field => field.Name is not ("schemaVersion" or "microphoneMuted" or "deviceId")) ||
            root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
            !root.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1 ||
            root.GetProperty("microphoneMuted").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            root.GetProperty("deviceId").ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException();
        var device = root.GetProperty("deviceId").GetString();
        if (device is not null && !AudioInputLimits.IsValidDeviceId(device)) throw new JsonException();
        return new(root.GetProperty("microphoneMuted").GetBoolean(), device);
    }

    private void ValidatePath()
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Microphone preference paths cannot contain reparse points.");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Microphone preference files cannot be reparse points.");
    }
}
