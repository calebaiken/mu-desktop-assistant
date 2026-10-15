using System.Security.Cryptography;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>One bounded partial frame; the recipient owns each accepted frame and must clear it.</summary>
internal sealed class CapturePacketizer(Func<byte[], bool> tryWrite) : IDisposable
{
    private readonly byte[] partial = new byte[AudioInputLimits.FrameBytes];
    private int partialLength;
    private bool finished;
    internal int TotalBytes { get; private set; }
    internal bool LimitReached => TotalBytes == AudioInputLimits.MaximumBytes;

    internal void Append(ReadOnlySpan<byte> packet)
    {
        if (finished || packet.Length % 2 != 0) throw new AudioInputException(AudioInputFailure.ProtocolError);
        var remaining = Math.Min(packet.Length, AudioInputLimits.MaximumBytes - TotalBytes);
        while (remaining > 0)
        {
            var length = Math.Min(remaining, partial.Length - partialLength);
            packet[..length].CopyTo(partial.AsSpan(partialLength));
            partialLength += length;
            TotalBytes += length;
            remaining -= length;
            packet = packet[length..];
            if (partialLength == partial.Length) PublishPartial();
        }
    }

    internal void Finish()
    {
        if (finished) throw new AudioInputException(AudioInputFailure.ProtocolError);
        if (partialLength > 0) PublishPartial();
        finished = true;
    }

    private void PublishPartial()
    {
        var frame = partial.AsSpan(0, partialLength).ToArray();
        CryptographicOperations.ZeroMemory(partial);
        partialLength = 0;
        var transferred = false;
        try
        {
            transferred = tryWrite(frame);
            if (!transferred) throw new AudioInputException(AudioInputFailure.Overflow);
        }
        finally { if (!transferred) CryptographicOperations.ZeroMemory(frame); }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(partial);
        partialLength = 0;
        finished = true;
    }
}
