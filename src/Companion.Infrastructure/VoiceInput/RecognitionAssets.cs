using System.Security.Cryptography;
using System.Text.Json;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Fixed offline assets. Held read handles deny replacement/deletion during the operation.</summary>
internal sealed class RecognitionAssets : IDisposable
{
    internal const string BaseModelFile = "ggml-base.en.bin";
    internal const string VadModelFile = "ggml-silero-v6.2.0.bin";
    internal const long BaseModelBytes = 147_964_211;
    internal const long VadModelBytes = 885_098;
    internal const string BaseModelHash = "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002";
    internal const string VadModelHash = "2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987";
    private readonly List<FileStream> leases = [];
    internal string DirectoryPath { get; }
    internal string RuntimeDirectory { get; }
    internal FileStream BaseModel { get; private set; } = null!;
    internal FileStream VadModel { get; private set; } = null!;

    private RecognitionAssets(string directory)
    {
        DirectoryPath = directory;
        RuntimeDirectory = Path.Combine(directory, "runtime");
    }

    internal static string ValidateDirectory(string? directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || directory.Length > 1024 ||
                !Path.IsPathFullyQualified(directory) || directory.Any(char.IsControl) || !SpeechLimits.IsValidText(directory))
                throw new AudioInputException(AudioInputFailure.InvalidRequest);
            var full = Path.GetFullPath(directory);
            if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
                throw new AudioInputException(AudioInputFailure.InvalidRequest);
            for (var current = new DirectoryInfo(full); current is not null; current = current.Parent)
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new AudioInputException(AudioInputFailure.Unavailable);
            return full;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw new AudioInputException(AudioInputFailure.Unavailable); }
    }

    internal static async Task<RecognitionAssets> OpenAsync(string directory, CancellationToken cancellationToken)
    {
        var result = new RecognitionAssets(ValidateDirectory(directory));
        try
        {
            ValidateDirectory(result.RuntimeDirectory);
            var models = ValidateDirectory(Path.Combine(result.DirectoryPath, "models"));
            using var resource = typeof(RecognitionAssets).Assembly.GetManifestResourceStream(
                "Companion.Infrastructure.VoiceInput.recognition-runtime-hashes.json")
                ?? throw new AudioInputException(AudioInputFailure.Unavailable);
            var catalog = await JsonSerializer.DeserializeAsync<Dictionary<string, AssetHash>>(resource,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken).ConfigureAwait(false)
                ?? throw new AudioInputException(AudioInputFailure.Unavailable);
            if (catalog.Count != 12 || catalog.Any(item => item.Key != Path.GetFileName(item.Key) ||
                    item.Value.Bytes is <= 0 or > 2 * 1024 * 1024 || item.Value.Sha256.Length != 64))
                throw new AudioInputException(AudioInputFailure.Unavailable);
            // Backend discovery must never encounter an unreviewed executable, DLL or subdirectory.
            var actual = Directory.EnumerateFileSystemEntries(result.RuntimeDirectory).Take(13)
                .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!actual.SetEquals(catalog.Keys)) throw new AudioInputException(AudioInputFailure.Unavailable);
            foreach (var item in catalog)
                await result.VerifyAsync(Path.Combine(result.RuntimeDirectory, item.Key), item.Value.Bytes,
                    item.Value.Sha256, cancellationToken).ConfigureAwait(false);
            result.BaseModel = await result.VerifyAsync(Path.Combine(models, BaseModelFile), BaseModelBytes,
                BaseModelHash, cancellationToken).ConfigureAwait(false);
            result.VadModel = await result.VerifyAsync(Path.Combine(models, VadModelFile), VadModelBytes,
                VadModelHash, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { result.Dispose(); throw new AudioInputException(AudioInputFailure.Unavailable); }
        catch { result.Dispose(); throw; }
    }

    private async Task<FileStream> VerifyAsync(string path, long expectedBytes, string expectedHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new AudioInputException(AudioInputFailure.Unavailable);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        leases.Add(stream);
        if (stream.Length != expectedBytes) throw new AudioInputException(AudioInputFailure.Unavailable);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(hash, expectedHash, StringComparison.Ordinal)) throw new AudioInputException(AudioInputFailure.Unavailable);
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        foreach (var lease in leases) lease.Dispose();
        leases.Clear();
    }

    private sealed record AssetHash(long Bytes, string Sha256);
}
