using System.Buffers.Binary;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

internal static class RecognitionAudio
{
    internal static float[] ToFloat(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length is <= 0 or > AudioInputLimits.MaximumBytes || (pcm.Length & 1) != 0)
            throw new AudioInputException(AudioInputFailure.InvalidRequest);
        var samples = new float[pcm.Length / 2];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(pcm[(index * 2)..]) / 32768f;
        return samples;
    }

    internal static (int Start, int Length) SelectSpeechSpan(IReadOnlyList<(float Start, float End)> segments, int sampleCount)
    {
        if (sampleCount is <= 0 or > AudioInputLimits.MaximumSamples || segments.Count is <= 0 or > 128)
            throw new AudioInputException(AudioInputFailure.ProtocolError);
        var previousEnd = 0f;
        foreach (var (start, end) in segments)
        {
            // Native standalone VAD returns CENTISECONDS, already padded, rounded from sample indices.
            if (!float.IsFinite(start) || !float.IsFinite(end) || start < previousEnd || end <= start ||
                end > Math.Ceiling(sampleCount / 160d)) throw new AudioInputException(AudioInputFailure.ProtocolError);
            previousEnd = end;
        }
        var first = Math.Clamp(checked((int)Math.Round(segments[0].Start * 160d)), 0, sampleCount);
        var last = Math.Clamp(checked((int)Math.Round(segments[^1].End * 160d)), 0, sampleCount);
        if (last <= first) throw new AudioInputException(AudioInputFailure.ProtocolError);
        return (first, last - first);
    }
}
