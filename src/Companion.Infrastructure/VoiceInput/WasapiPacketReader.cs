using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>The fakeable packet seam never activates a device.</summary>
internal interface IWasapiCapturePackets
{
    int GetNextPacketSize(out uint frames);
    int GetBuffer(out IntPtr data, out uint frames, out uint flags);
    int ReleaseBuffer(uint frames);
}

internal sealed class WasapiCapturePackets(WasapiNative.CaptureClient client) : IWasapiCapturePackets
{
    public int GetNextPacketSize(out uint frames) => client.GetNextPacketSize(out frames);
    public int GetBuffer(out IntPtr data, out uint frames, out uint flags) => client.GetBuffer(out data, out frames, out flags, out _, out _);
    public int ReleaseBuffer(uint frames) => client.ReleaseBuffer(frames);
}

/// <summary>No wait, pipe write or await is permitted between acquisition and release.</summary>
internal sealed class WasapiPacketReader(uint maximumPacketFrames) : IDisposable
{
    private readonly byte[] scratch = CreateScratch(maximumPacketFrames);

    internal void Drain(IWasapiCapturePackets client, CapturePacketizer destination, Func<bool> shouldStop)
    {
        while (!destination.LimitReached && !shouldStop())
        {
            WasapiNative.Check(client.GetNextPacketSize(out var next));
            if (next == 0) return;
            if (next > maximumPacketFrames) throw new AudioInputException(AudioInputFailure.ProtocolError);
            var result = client.GetBuffer(out var data, out var frames, out var flags);
            WasapiNative.Check(result);
            if (result == WasapiNative.BufferEmpty)
            {
                if (frames != 0) throw new AudioInputException(AudioInputFailure.ProtocolError);
                return;
            }
            if (frames == 0) throw new AudioInputException(AudioInputFailure.ProtocolError);
            try
            {
                if (frames > maximumPacketFrames || frames > next || (flags & ~7u) != 0)
                    throw new AudioInputException(AudioInputFailure.ProtocolError);
                // No documented first-packet exception: a discontinuous/invalidly timed utterance fails visibly.
                if ((flags & (WasapiNative.Discontinuity | WasapiNative.TimestampError)) != 0)
                    throw new AudioInputException(AudioInputFailure.DeviceLost);
                var bytes = checked((int)frames * 2);
                if ((flags & WasapiNative.Silent) != 0) Array.Clear(scratch, 0, bytes);
                else
                {
                    if (data == IntPtr.Zero) throw new AudioInputException(AudioInputFailure.ProtocolError);
                    Marshal.Copy(data, scratch, 0, bytes);
                }
                destination.Append(scratch.AsSpan(0, bytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(scratch);
                // Consume the whole native packet even when the bounded utterance keeps only its prefix.
                WasapiNative.Check(client.ReleaseBuffer(frames));
            }
        }
    }

    private static byte[] CreateScratch(uint frames)
    {
        // The engine buffer is separate from the 50-frame managed queue and is bounded to one second.
        if (frames is 0 or > AudioInputLimits.SampleRate) throw new AudioInputException(AudioInputFailure.Unavailable);
        return new byte[checked((int)frames * 2)];
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(scratch);
}
