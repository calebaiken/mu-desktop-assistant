using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

internal sealed record RecognitionCommand(int Version, Guid OperationId, string AssetDirectory,
    int SampleRate, int Channels, int BitsPerSample, int SampleCount);
internal sealed record RecognitionEvent(int Version, Guid OperationId, string Kind,
    SpeechRecognitionResult? Result = null, AudioInputFailure? Failure = null);

/// <summary>One command, one exact PCM frame, then ready and one terminal event. No text on stderr.</summary>
internal static class RecognitionProtocol
{
    internal const int MaximumJsonBytes = 16 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 4
    };

    internal static byte[] SerializeCommand(RecognitionCommand value)
    { ValidateCommand(value); return JsonSerializer.SerializeToUtf8Bytes(value, Options); }
    internal static byte[] SerializeEvent(RecognitionEvent value)
    { ValidateEvent(value); return JsonSerializer.SerializeToUtf8Bytes(value, Options); }

    internal static RecognitionCommand ParseCommand(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = Parse(bytes);
            Fields(document.RootElement, "version", "operationId", "assetDirectory", "sampleRate", "channels", "bitsPerSample", "sampleCount");
            var value = document.RootElement.Deserialize<RecognitionCommand>(Options) ?? throw Invalid();
            ValidateCommand(value);
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { throw Invalid(); }
    }

    internal static RecognitionEvent ParseEvent(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = Parse(bytes);
            Fields(document.RootElement, "version", "operationId", "kind", "result", "failure");
            if (document.RootElement.GetProperty("result") is { ValueKind: not JsonValueKind.Null } result)
                Fields(result, "text", "hasSpeech");
            var value = document.RootElement.Deserialize<RecognitionEvent>(Options) ?? throw Invalid();
            ValidateEvent(value);
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { throw Invalid(); }
    }

    internal static void ValidateCommand(RecognitionCommand value)
    {
        if (value.Version != 1 || value.OperationId == Guid.Empty || value.SampleRate != AudioInputLimits.SampleRate ||
            value.Channels != 1 || value.BitsPerSample != 16 || value.SampleCount is <= 0 or > AudioInputLimits.MaximumSamples ||
            string.IsNullOrWhiteSpace(value.AssetDirectory) || value.AssetDirectory.Length > 1024 ||
            !SpeechLimits.IsValidText(value.AssetDirectory) || value.AssetDirectory.Any(char.IsControl) ||
            !Path.IsPathFullyQualified(value.AssetDirectory) || value.AssetDirectory.StartsWith("\\\\", StringComparison.Ordinal) ||
            value.AssetDirectory.StartsWith("//", StringComparison.Ordinal)) throw Invalid();
    }

    internal static void ValidateEvent(RecognitionEvent value)
    {
        if (value.Version != 1 || value.OperationId == Guid.Empty) throw Invalid();
        switch (value.Kind)
        {
            case "ready":
                if (value.Result is not null || value.Failure is not null) throw Invalid();
                break;
            case "completed":
                if (value.Failure is not null) throw Invalid();
                AudioInputLimits.Validate(value.Result);
                break;
            case "failed":
                if (value.Result is not null || value.Failure is null || !Enum.IsDefined(value.Failure.Value)) throw Invalid();
                break;
            default: throw Invalid();
        }
    }

    private static JsonDocument Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is <= 0 or > MaximumJsonBytes) throw Invalid();
        var document = JsonDocument.Parse(Utf8.GetString(bytes), new JsonDocumentOptions { MaxDepth = 4 });
        try { ValidateJson(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void ValidateJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in element.EnumerateObject())
            {
                if (!names.Add(item.Name) || !SpeechLimits.IsValidText(item.Name)) throw Invalid();
                ValidateJson(item.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) throw Invalid();
        else if (element.ValueKind == JsonValueKind.String && !SpeechLimits.IsValidText(element.GetString()!)) throw Invalid();
    }

    private static void Fields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != fields.Length) throw Invalid();
        foreach (var field in fields) if (!value.TryGetProperty(field, out _)) throw Invalid();
    }

    internal static async Task<byte[]> ReadFrameAsync(Stream stream, int maximum, CancellationToken cancellationToken,
        int? exactLength = null)
    {
        var prefix = new byte[4];
        byte[]? payload = null;
        try
        {
            await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if (length <= 0 || length > maximum || (exactLength is not null && length != exactLength)) throw Invalid();
            payload = new byte[length];
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            var result = payload;
            payload = null;
            return result;
        }
        catch (EndOfStreamException) { throw Invalid(); }
        finally { if (payload is not null) CryptographicOperations.ZeroMemory(payload); }
    }

    internal static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> bytes, int maximum, CancellationToken cancellationToken)
    {
        if (bytes.Length <= 0 || bytes.Length > maximum) throw Invalid();
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static AudioInputException Invalid() => new(AudioInputFailure.ProtocolError);
}
