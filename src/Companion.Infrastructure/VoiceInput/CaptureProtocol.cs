using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

internal sealed record CaptureCommand(int Version, Guid OperationId, string Kind, AudioCaptureRequest? Request);
internal enum CaptureEventKind : byte { Started = 1, Pcm = 2, Stopped = 3, Devices = 4, Failed = 5 }
internal sealed record CaptureEvent(CaptureEventKind Kind, Guid OperationId, int Sequence, byte[] Payload);

/// <summary>Private byte-pipe framing. Audio remains binary, never JSON/base64, files or diagnostic text.</summary>
internal static class CaptureProtocol
{
    internal const int MaximumCommandBytes = 16 * 1024;
    internal const int MaximumMetadataBytes = 256 * 1024;
    internal const int HeaderBytes = 22; // version, kind, operation UUID, little-endian sequence
    internal const byte StopCommand = 1;

    internal static AudioInputException Invalid() => new(AudioInputFailure.ProtocolError);

    internal static async Task WriteCommandAsync(Stream stream, CaptureCommand command, CancellationToken token)
    {
        ValidateCommand(command);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        try { await WriteFrameAsync(stream, bytes, MaximumCommandBytes, token).ConfigureAwait(false); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static async Task<CaptureCommand> ReadCommandAsync(Stream stream, CancellationToken token)
    {
        var bytes = await ReadFrameAsync(stream, MaximumCommandBytes, token).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 4 });
            var root = document.RootElement;
            Properties(root, "Version", "OperationId", "Kind", "Request");
            var version = root.GetProperty("Version").GetInt32();
            var id = root.GetProperty("OperationId").GetGuid();
            var kind = root.GetProperty("Kind").GetString() ?? throw Invalid();
            var requestValue = root.GetProperty("Request");
            AudioCaptureRequest? request = null;
            if (requestValue.ValueKind != JsonValueKind.Null)
            {
                Properties(requestValue, "DeviceId", "SessionId");
                request = new(requestValue.GetProperty("DeviceId").GetString() ?? throw Invalid(),
                    requestValue.GetProperty("SessionId").GetGuid());
            }
            var command = new CaptureCommand(version, id, kind, request);
            ValidateCommand(command);
            return command;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { throw Invalid(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static async Task WriteEventAsync(Stream stream, CaptureEvent message, CancellationToken token)
    {
        ValidateEvent(message);
        var bytes = new byte[checked(HeaderBytes + message.Payload.Length)];
        try
        {
            bytes[0] = 1;
            bytes[1] = (byte)message.Kind;
            message.OperationId.TryWriteBytes(bytes.AsSpan(2, 16));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), message.Sequence);
            message.Payload.CopyTo(bytes, HeaderBytes);
            await WriteFrameAsync(stream, bytes, HeaderBytes + MaximumMetadataBytes, token).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static async Task<CaptureEvent> ReadEventAsync(Stream stream, CancellationToken token)
    {
        var bytes = await ReadFrameAsync(stream, HeaderBytes + MaximumMetadataBytes, token).ConfigureAwait(false);
        byte[]? payload = null;
        try
        {
            if (bytes.Length < HeaderBytes || bytes[0] != 1) throw Invalid();
            payload = bytes.AsSpan(HeaderBytes).ToArray();
            var message = new CaptureEvent((CaptureEventKind)bytes[1], new Guid(bytes.AsSpan(2, 16)),
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4)), payload);
            ValidateEvent(message);
            payload = null; // Ownership transfers to the caller, including PCM clearing.
            return message;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
        }
    }

    internal static byte[] SerializeDevices(IReadOnlyList<AudioInputDevice> devices)
    {
        ValidateDevices(devices);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(devices);
        if (bytes.Length > MaximumMetadataBytes) { CryptographicOperations.ZeroMemory(bytes); throw Invalid(); }
        return bytes;
    }

    internal static IReadOnlyList<AudioInputDevice> ParseDevices(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumMetadataBytes) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 3 });
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > AudioInputLimits.MaximumDevices)
                throw Invalid();
            List<AudioInputDevice> devices = [];
            foreach (var item in document.RootElement.EnumerateArray())
            {
                Properties(item, "Id", "Name");
                devices.Add(new(item.GetProperty("Id").GetString() ?? throw Invalid(), item.GetProperty("Name").GetString() ?? throw Invalid()));
            }
            ValidateDevices(devices);
            return devices.AsReadOnly();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { throw Invalid(); }
    }

    internal static byte[] FailurePayload(AudioInputFailure failure)
    {
        if (!Enum.IsDefined(failure)) throw Invalid();
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)failure);
        return bytes;
    }

    internal static AudioInputFailure ParseFailure(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 4) throw Invalid();
        var failure = (AudioInputFailure)BinaryPrimitives.ReadInt32LittleEndian(payload);
        return Enum.IsDefined(failure) ? failure : throw Invalid();
    }

    internal static void ValidateRequest(AudioCaptureRequest? request)
    {
        if (request is null || request.SessionId == Guid.Empty || !ValidText(request.DeviceId, AudioInputLimits.MaximumDeviceIdCharacters))
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
    }

    internal static void ValidateDevice(AudioInputDevice? device)
    {
        if (device is null || !ValidText(device.Id, AudioInputLimits.MaximumDeviceIdCharacters) ||
            !ValidText(device.Name, AudioInputLimits.MaximumDeviceNameCharacters)) throw Invalid();
    }

    private static void ValidateDevices(IReadOnlyList<AudioInputDevice> devices)
    {
        if (devices.Count > AudioInputLimits.MaximumDevices) throw Invalid();
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (var device in devices) { ValidateDevice(device); if (!ids.Add(device.Id)) throw Invalid(); }
    }

    private static void ValidateCommand(CaptureCommand command)
    {
        if (command.Version != 1 || command.OperationId == Guid.Empty) throw Invalid();
        if (command.Kind == "devices") { if (command.Request is not null) throw Invalid(); }
        else if (command.Kind == "capture")
        {
            ValidateRequest(command.Request);
            if (command.Request!.SessionId != command.OperationId) throw Invalid();
        }
        else throw Invalid();
    }

    private static void ValidateEvent(CaptureEvent message)
    {
        if (message.OperationId == Guid.Empty || message.Sequence is < 0 or > AudioInputLimits.MaximumBytes / AudioInputLimits.FrameBytes || message.Payload is null)
            throw Invalid();
        switch (message.Kind)
        {
            case CaptureEventKind.Started when message.Sequence == 0 && message.Payload.Length == 0: return;
            case CaptureEventKind.Pcm when message.Sequence > 0 && message.Payload.Length is > 0 and <= AudioInputLimits.FrameBytes && message.Payload.Length % 2 == 0: return;
            case CaptureEventKind.Stopped when message.Payload.Length == 1 && message.Payload[0] <= 1: return;
            case CaptureEventKind.Devices when message.Sequence == 0:
                _ = ParseDevices(message.Payload); return;
            case CaptureEventKind.Failed:
                _ = ParseFailure(message.Payload); return;
            default: throw Invalid();
        }
    }

    private static bool ValidText(string? text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)) return false;
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsSurrogate(text[index])) continue;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index])) return false;
        }
        return true;
    }

    private static void Properties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid();
        var remaining = new HashSet<string>(expected, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) if (!remaining.Remove(property.Name)) throw Invalid();
        if (remaining.Count != 0) throw Invalid();
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, int maximum, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 || length > maximum) throw Invalid();
        var bytes = new byte[length];
        try { await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> bytes, int maximum, CancellationToken token)
    {
        if (bytes.Length is <= 0 || bytes.Length > maximum) throw Invalid();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
}
