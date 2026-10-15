using System.Security.Cryptography;

namespace Companion.Application.Voice;

public sealed record AudioInputDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record AudioCaptureRequest(string DeviceId, Guid SessionId);
public enum AudioCaptureState { Started, Stopped }
public enum AudioInputFailure
{
    InvalidRequest, Unavailable, PermissionDenied, DeviceLost, Busy, Overflow,
    Timeout, ProtocolError, CleanupFailed
}

/// <summary>Fixed errors contain no device identity, audio, transcript or native payload.</summary>
public sealed class AudioInputException(AudioInputFailure failure)
    : Exception($"Local voice input failed: {failure}.")
{
    public AudioInputFailure Failure { get; } = failure;
}

/// <summary>Single-owner, mono 16 kHz signed little-endian PCM16. Dispose clears the entire backing allocation.</summary>
public sealed class Pcm16Audio : IDisposable
{
    private byte[]? buffer;
    public int ByteLength { get; }
    public int SampleCount => ByteLength / 2;
    public bool DurationLimited { get; }
    public TimeSpan Duration => TimeSpan.FromSeconds((double)SampleCount / AudioInputLimits.SampleRate);
    public ReadOnlyMemory<byte> Bytes => (buffer ?? throw new ObjectDisposedException(nameof(Pcm16Audio))).AsMemory(0, ByteLength);

    private Pcm16Audio(byte[] ownedBuffer, int validByteLength, bool durationLimited)
    {
        buffer = ownedBuffer;
        ByteLength = validByteLength;
        DurationLimited = durationLimited;
    }

    /// <summary>The caller transfers exclusive ownership and must not access the array again, even on failure.</summary>
    public static Pcm16Audio TakeOwnership(byte[] ownedBuffer, int validByteLength, bool durationLimited = false)
    {
        ArgumentNullException.ThrowIfNull(ownedBuffer);
        if (ownedBuffer.Length > AudioInputLimits.MaximumBytes || validByteLength < 0 ||
            validByteLength > ownedBuffer.Length || (validByteLength & 1) != 0)
        {
            CryptographicOperations.ZeroMemory(ownedBuffer);
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
        }
        return new(ownedBuffer, validByteLength, durationLimited);
    }

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref buffer, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }

    public override string ToString() => $"PCM16 audio ({SampleCount} samples).";
}

public interface IAudioCapture : IAsyncDisposable
{
    Task<IReadOnlyList<AudioInputDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);
    /// <summary>stopToken is deliberate hold release; cancellationToken discards the operation. Neither may restart capture.</summary>
    Task<Pcm16Audio> CaptureAsync(AudioCaptureRequest request, IProgress<AudioCaptureState>? progress,
        CancellationToken stopToken, CancellationToken cancellationToken = default);
}

public sealed record SpeechRecognitionResult(string Text, bool HasSpeech)
{
    public override string ToString() => HasSpeech ? "Recognized speech draft." : "No speech detected.";
}

public interface ISpeechToTextProvider : IAsyncDisposable
{
    /// <summary>Caller retains ownership of audio until this task completes, then must dispose it.</summary>
    Task<SpeechRecognitionResult> TranscribeAsync(Pcm16Audio audio, CancellationToken cancellationToken = default);
}

public static class AudioInputLimits
{
    public const int SampleRate = 16_000;
    public const int FrameSamples = 320;
    public const int FrameBytes = 640;
    public const int QueueFrames = 50;
    public const int MaximumSamples = 480_000;
    public const int MaximumBytes = 960_000;
    public const int MaximumDevices = 32;
    public const int MaximumDeviceIdCharacters = 512;
    public const int MaximumDeviceNameCharacters = 256;
    public const int MaximumTextCharacters = 2_000;
    public static readonly TimeSpan CaptureDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan InitializationDeadline = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CommandDeadline = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan StopDeadline = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan RecognitionDeadline = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan OutputTail = TimeSpan.FromMilliseconds(300);

    public static void Validate(AudioCaptureRequest? request)
    {
        if (request is null || request.SessionId == Guid.Empty || !IsValidDeviceId(request.DeviceId))
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
    }

    public static bool IsValidDeviceId(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumDeviceIdCharacters && SpeechLimits.IsValidText(value) && !value.Any(char.IsControl);

    public static void Validate(SpeechRecognitionResult? result)
    {
        if (result is null || result.Text is null || result.Text.Length > MaximumTextCharacters ||
            !SpeechLimits.IsValidText(result.Text) || result.HasSpeech != !string.IsNullOrWhiteSpace(result.Text))
            throw new AudioInputException(AudioInputFailure.ProtocolError);
    }
}
