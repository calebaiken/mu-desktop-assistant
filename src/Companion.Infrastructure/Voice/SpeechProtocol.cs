using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

internal sealed record SpeechCommand(int Version, Guid OperationId, string Kind, SpeechRequest? Request);
internal sealed record SpeechEvent(int Version, Guid OperationId, string Kind,
    IReadOnlyList<SpeechVoice>? Voices = null, SpeechAudioProbe? Probe = null, SpeechFailure? Failure = null);

/// <summary>One bounded little-endian length-prefixed UTF-8 JSON frame at a time.</summary>
internal static class SpeechProtocol
{
    internal const int MaximumCommandBytes = 16 * 1024;
    internal const int MaximumEventBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    internal static byte[] SerializeCommand(SpeechCommand command)
    {
        ValidateCommand(command);
        return JsonSerializer.SerializeToUtf8Bytes(command, JsonOptions);
    }

    internal static byte[] SerializeEvent(SpeechEvent value)
    {
        ValidateEvent(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    }

    internal static SpeechCommand ParseCommand(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = ParseStrict(bytes, MaximumCommandBytes);
            RequireFields(document.RootElement, "version", "operationId", "kind", "request");
            if (document.RootElement.GetProperty("request") is { ValueKind: not JsonValueKind.Null } request)
                RequireFields(request, "text", "voiceId", "rate");
            var value = document.RootElement.Deserialize<SpeechCommand>(JsonOptions) ?? throw Invalid();
            ValidateCommand(value);
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { throw Invalid(); }
    }

    internal static SpeechEvent ParseEvent(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = ParseStrict(bytes, MaximumEventBytes);
            var root = document.RootElement;
            RequireFields(root, "version", "operationId", "kind", "voices", "probe", "failure");
            if (root.GetProperty("voices") is { ValueKind: not JsonValueKind.Null } voices)
            {
                if (voices.ValueKind != JsonValueKind.Array) throw Invalid();
                foreach (var voice in voices.EnumerateArray()) RequireFields(voice, "id", "name", "culture");
            }
            if (root.GetProperty("probe") is { ValueKind: not JsonValueKind.Null } probe)
            {
                RequireFields(probe, "voice", "totalBytes", "dataBytes", "sampleRate", "channels",
                    "bitsPerSample", "hasNonZeroSamples", "sha256", "chunks", "durationSeconds");
                RequireFields(probe.GetProperty("voice"), "id", "name", "culture");
                var chunks = probe.GetProperty("chunks");
                if (chunks.ValueKind != JsonValueKind.Array) throw Invalid();
                foreach (var chunk in chunks.EnumerateArray()) RequireFields(chunk, "id", "offset", "length");
            }
            var value = root.Deserialize<SpeechEvent>(JsonOptions) ?? throw Invalid();
            ValidateEvent(value);
            if (value.Probe is { } audio && root.GetProperty("probe").GetProperty("durationSeconds").GetDouble() != audio.DurationSeconds)
                throw Invalid();
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { throw Invalid(); }
    }

    private static JsonDocument ParseStrict(ReadOnlySpan<byte> bytes, int maximum)
    {
        if (bytes.Length is <= 0 || bytes.Length > maximum) throw Invalid();
        // The decoder rejects malformed UTF-8; GetString rejects escaped unpaired surrogates.
        var text = StrictUtf8.GetString(bytes);
        var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
        try { ValidateJson(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void ValidateJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name) || !SpeechLimits.IsValidText(property.Name)) throw Invalid();
                    ValidateJson(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) ValidateJson(item);
                break;
            case JsonValueKind.String:
                if (!SpeechLimits.IsValidText(element.GetString()!)) throw Invalid();
                break;
        }
    }

    private static void RequireFields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != names.Length) throw Invalid();
        foreach (var name in names) if (!value.TryGetProperty(name, out _)) throw Invalid();
    }

    internal static void ValidateCommand(SpeechCommand value)
    {
        if (value.Version != 1 || value.OperationId == Guid.Empty) throw Invalid();
        if (value.Kind == "voices") { if (value.Request is not null) throw Invalid(); }
        else if (value.Kind is "speak" or "probe") SpeechLimits.Validate(value.Request);
        else throw Invalid();
    }

    internal static void ValidateEvent(SpeechEvent value)
    {
        if (value.Version != 1 || value.OperationId == Guid.Empty) throw Invalid();
        switch (value.Kind)
        {
            case "voices":
                if (value.Voices is null || value.Voices.Count > SpeechLimits.MaximumVoices || value.Probe is not null || value.Failure is not null)
                    throw Invalid();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var voice in value.Voices) { ValidateVoice(voice); if (!ids.Add(voice.Id)) throw Invalid(); }
                break;
            case "probe":
                if (value.Probe is null || value.Voices is not null || value.Failure is not null) throw Invalid();
                SpeechWave.ValidateMetadata(value.Probe);
                break;
            case "speaking":
            case "completed":
                if (value.Probe is not null || value.Voices is not null || value.Failure is not null) throw Invalid();
                break;
            case "failed":
                if (value.Probe is not null || value.Voices is not null || value.Failure is null || !Enum.IsDefined(value.Failure.Value))
                    throw Invalid();
                break;
            default: throw Invalid();
        }
    }

    internal static void ValidateVoice(SpeechVoice? voice)
    {
        if (voice is null || !Bounded(voice.Id, SpeechLimits.MaximumVoiceIdCharacters) ||
            !Bounded(voice.Name, 128) || !Bounded(voice.Culture, 32)) throw Invalid();
    }

    private static bool Bounded(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit && SpeechLimits.IsValidText(value);

    internal static async Task<byte[]> ReadFrameAsync(Stream stream, int maximum, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        try
        {
            await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if (length <= 0 || length > maximum) throw Invalid();
            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            return payload;
        }
        catch (EndOfStreamException) { throw Invalid(); }
    }

    internal static async Task WriteFrameAsync(Stream stream, byte[] payload, int maximum, CancellationToken cancellationToken)
    {
        if (payload.Length <= 0 || payload.Length > maximum) throw Invalid();
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static SpeechException Invalid() => new(SpeechFailure.ProtocolError);
}
