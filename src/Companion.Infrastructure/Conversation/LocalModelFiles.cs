using System.Security.Cryptography;
using System.Text.Json;

namespace Companion.Infrastructure.Conversation;

/// <summary>Admission of the fixed, reviewed model/runtime. Open handles prevent replacement while in use.</summary>
internal sealed class LocalModelFiles : IDisposable
{
    internal const string ModelFileName = "Qwen3-0.6B-Q8_0.gguf";
    internal const string ModelHash = "9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031";
    private readonly List<FileStream> leases = [];
    internal string RuntimeDirectory { get; }
    internal string ModelPath { get; }
    internal string ExecutablePath => Path.Combine(RuntimeDirectory, "llama-server.exe");

    private LocalModelFiles(string directory)
    {
        RuntimeDirectory = Path.Combine(directory, "runtime");
        ModelPath = Path.Combine(directory, ModelFileName);
    }

    internal static string ValidateDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\", StringComparison.Ordinal)
            || directory.Any(char.IsControl)) throw new ArgumentException("An absolute local model directory is required.");
        var full = Path.GetFullPath(directory);
        for (var current = new DirectoryInfo(full); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Model paths cannot contain reparse points.");
        return full;
    }

    internal static async Task<LocalModelFiles> OpenAsync(string directory, CancellationToken cancellationToken)
    {
        var files = new LocalModelFiles(ValidateDirectory(directory));
        try
        {
            ValidateDirectory(files.RuntimeDirectory);
            using var resource = typeof(LocalModelFiles).Assembly.GetManifestResourceStream(
                "Companion.Infrastructure.Conversation.local-runtime-hashes.json")
                ?? throw new InvalidOperationException("Runtime catalog is missing.");
            var catalog = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(resource, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Runtime catalog is invalid.");
            var actual = Directory.EnumerateFiles(files.RuntimeDirectory).Where(path =>
                Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!actual.SetEquals(catalog.Keys)) throw new IOException("Runtime file inventory differs from the pinned package.");
            foreach (var entry in catalog)
                await files.VerifyAsync(Path.Combine(files.RuntimeDirectory, entry.Key), entry.Value, 32 * 1024 * 1024, null, cancellationToken);
            await files.VerifyAsync(files.ModelPath, ModelHash, 639446688, 639446688, cancellationToken);
            return files;
        }
        catch { files.Dispose(); throw; }
    }

    private async Task VerifyAsync(string path, string hash, long maximum, long? exact, CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("Model/runtime must be regular files.");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        leases.Add(stream);
        if (stream.Length > maximum || stream.Length <= 0 || (exact.HasValue && stream.Length != exact))
            throw new IOException("Model/runtime file length mismatch.");
        var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(digest, hash, StringComparison.Ordinal)) throw new IOException("Model/runtime checksum mismatch.");
    }

    public void Dispose()
    {
        foreach (var lease in leases) lease.Dispose();
        leases.Clear();
    }
}
