using System.Text.Json;
using Companion.Application.Diagnostics;

namespace Companion.Infrastructure.Diagnostics;

public sealed class JsonEventLog : IEventLog, IDisposable
{
    private readonly string directory;
    private readonly long maximumBytes;
    private readonly int retainedFiles;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1);
    public bool DiagnosticsEnabled { get; set; }
    public int RecoveredCorruptLogs { get; private set; }

    public JsonEventLog(string directory, long maximumBytes = 10 * 1024 * 1024,
        int retainedFiles = 5, TimeProvider? timeProvider = null)
    {
        if (maximumBytes < 1024 || maximumBytes > 100 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (retainedFiles < 1 || retainedFiles > 10) throw new ArgumentOutOfRangeException(nameof(retainedFiles));
        this.directory = Path.GetFullPath(directory);
        this.maximumBytes = maximumBytes;
        this.retainedFiles = retainedFiles;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task WriteAsync(AppEvent entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!Enum.IsDefined(entry.Area) || !Enum.IsDefined(entry.Code) || entry.DurationMs < 0)
            throw new ArgumentException("Invalid diagnostic event.", nameof(entry));
        if (entry.Area == EventArea.Diagnostics && !DiagnosticsEnabled) return;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(directory);
                var name = entry.Area.ToString().ToLowerInvariant();
                var file = Path.Combine(directory, name + ".jsonl");
                var now = timeProvider.GetUtcNow();
                Sweep(now);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1, timestamp = now, area = entry.Area.ToString(),
                    code = entry.Code.ToString(), operationId = entry.OperationId, durationMs = entry.DurationMs
                });
                if (File.Exists(file) && new FileInfo(file).Length + bytes.Length + 1 > maximumBytes)
                {
                    if (retainedFiles == 1) File.Delete(file);
                    else
                    {
                        for (var index = retainedFiles - 1; index > 1; index--)
                        {
                            var previous = file + "." + (index - 1);
                            if (File.Exists(previous)) File.Move(previous, file + "." + index, overwrite: true);
                        }
                        File.Move(file, file + ".1", overwrite: true);
                    }
                }
                using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
                stream.WriteByte((byte)'\n');
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static DateTime FirstEventTime(string file)
    {
        using var stream = File.OpenRead(file);
        var prefix = new byte[1024];
        var count = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        var newline = Array.IndexOf(prefix, (byte)'\n', 0, count);
        if (newline < 0) throw new InvalidDataException("Diagnostic log has an invalid record.");
        stream.Seek(-1, SeekOrigin.End);
        if (stream.ReadByte() != '\n') throw new InvalidDataException("Diagnostic log has an incomplete record.");
        try
        {
            using var first = JsonDocument.Parse(prefix.AsMemory(0, newline));
            if (first.RootElement.ValueKind != JsonValueKind.Object ||
                !first.RootElement.TryGetProperty("timestamp", out var stamp) || stamp.ValueKind != JsonValueKind.String ||
                !stamp.TryGetDateTimeOffset(out var timestamp))
                throw new InvalidDataException("Diagnostic log has an invalid timestamp.");
            return timestamp.UtcDateTime;
        }
        catch (JsonException error) { throw new InvalidDataException("Diagnostic log has invalid JSON.", error); }
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var area in Enum.GetValues<EventArea>())
        {
            var file = Path.Combine(directory, area.ToString().ToLowerInvariant() + ".jsonl");
            var cutoff = now.AddDays(area == EventArea.ToolAudit ? -90 : -14).UtcDateTime;
            var quarantine = file + ".corrupt";
            if (File.Exists(quarantine) && File.GetLastWriteTimeUtc(quarantine) < cutoff) File.Delete(quarantine);
            for (var index = 0; index < retainedFiles; index++)
            {
                var candidate = index == 0 ? file : file + "." + index;
                if (!File.Exists(candidate)) continue;
                try { if (FirstEventTime(candidate) < cutoff) File.Delete(candidate); }
                catch (InvalidDataException)
                {
                    File.Move(candidate, quarantine, overwrite: true);
                    RecoveredCorruptLogs++;
                }
            }
        }
    }

    public void Dispose() => gate.Dispose();
}
