using System.Runtime.InteropServices;

namespace Companion.Infrastructure.VoiceInput;

// ABI pinned to whisper.cpp v1.9.4 / b5130, commit927cfce34f31707e17f2bff35c349632fb9e2c3a.
// These are Windows x64 C structures: bool=one byte; enum/int=4; pointer/size_t=8.
// Keep every field, including unused parameters, so native defaults survive a round trip.
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WhisperContextParameters
{
    internal byte UseGpu, FlashAttention;
    internal int GpuDevice;
    internal byte DtwTokenTimestamps;
    internal int DtwHeadsPreset, DtwTop;
    internal nuint DtwHeadCount;
    internal nint DtwHeads;
    internal nuint DtwMemorySize;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WhisperFullParameters
{
    internal int Strategy, Threads, MaximumTextContext, OffsetMilliseconds, DurationMilliseconds;
    internal byte Translate, NoContext, NoTimestamps, SingleSegment;
    internal byte PrintSpecial, PrintProgress, PrintRealtime, PrintTimestamps;
    internal byte TokenTimestamps;
    internal float TokenThreshold, TokenSumThreshold;
    internal int MaximumSegmentLength;
    internal byte SplitOnWord;
    internal int MaximumTokens;
    internal byte DebugMode;
    internal int AudioContext;
    internal byte Tinydiarize;
    internal nint SuppressRegex, InitialPrompt;
    internal byte CarryInitialPrompt;
    internal nint PromptTokens;
    internal int PromptTokenCount;
    internal nint Language;
    internal byte DetectLanguage, SuppressBlank, SuppressNonSpeechTokens;
    internal float Temperature, MaximumInitialTimestamp, LengthPenalty;
    internal float TemperatureIncrement, EntropyThreshold, LogProbabilityThreshold, NoSpeechThreshold;
    internal int GreedyBestOf, BeamSize;
    internal float BeamPatience;
    internal nint NewSegmentCallback, NewSegmentUserData;
    internal nint ProgressCallback, ProgressUserData;
    internal nint EncoderBeginCallback, EncoderBeginUserData;
    internal nint AbortCallback, AbortUserData;
    internal nint LogitsFilterCallback, LogitsFilterUserData;
    internal nint GrammarRules;
    internal nuint GrammarRuleCount, GrammarStartRule;
    internal float GrammarPenalty;
    internal byte Vad;
    internal nint VadModelPath;
    internal WhisperVadParameters VadParameters;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WhisperVadContextParameters
{
    internal int Threads;
    internal byte UseGpu;
    internal int GpuDevice;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WhisperVadParameters
{
    internal float Threshold;
    internal int MinimumSpeechMilliseconds, MinimumSilenceMilliseconds;
    internal float MaximumSpeechSeconds;
    internal int PaddingMilliseconds;
    internal float SampleOverlap;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WhisperModelLoader
{
    internal nint Context, Read, Eof, Close;
}

internal static class WhisperAbi
{
    internal static bool IsSupported => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
        Marshal.SizeOf<WhisperContextParameters>() == 48 && Marshal.SizeOf<WhisperFullParameters>() == 304 &&
        Marshal.SizeOf<WhisperVadContextParameters>() == 12 && Marshal.SizeOf<WhisperVadParameters>() == 24 &&
        Marshal.SizeOf<WhisperModelLoader>() == 32 &&
        Offset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwHeadCount)) == 24 &&
        Offset<WhisperFullParameters>(nameof(WhisperFullParameters.Language)) == 104 &&
        Offset<WhisperFullParameters>(nameof(WhisperFullParameters.AbortCallback)) == 208 &&
        Offset<WhisperFullParameters>(nameof(WhisperFullParameters.Vad)) == 268 &&
        Offset<WhisperFullParameters>(nameof(WhisperFullParameters.VadModelPath)) == 272 &&
        Offset<WhisperFullParameters>(nameof(WhisperFullParameters.VadParameters)) == 280;

    private static long Offset<T>(string name) where T : struct => Marshal.OffsetOf<T>(name).ToInt64();
}
