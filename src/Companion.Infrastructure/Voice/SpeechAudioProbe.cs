using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

/// <summary>Content-free metadata from a separate, inaudible synthesis diagnostic.</summary>
public sealed record SpeechAudioProbe(SpeechVoice Voice, int TotalBytes, int DataBytes,
    int SampleRate, int Channels, int BitsPerSample, bool HasNonZeroSamples, string Sha256,
    IReadOnlyList<SpeechWaveChunk> Chunks)
{
    public double DurationSeconds => (double)DataBytes / (SampleRate * Channels * (BitsPerSample / 8));
}

public sealed record SpeechWaveChunk(string Id, int Offset, int Length);
