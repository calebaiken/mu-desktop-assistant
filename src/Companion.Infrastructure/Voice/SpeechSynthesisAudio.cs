namespace Companion.Infrastructure.Voice;

/// <summary>Explicit local diagnostic audio. Normal read-aloud never returns or saves audio bytes.</summary>
public sealed record SpeechSynthesisAudio(SpeechAudioProbe Probe, byte[] Wave);
