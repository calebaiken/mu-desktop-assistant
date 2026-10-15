using System.Runtime.InteropServices;
using Companion.Infrastructure.VoiceInput;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>
/// Reviewer-derived Windows x64 C layout oracle from the pinned v1.9.4 header.
/// These tests never load a native speech DLL, read a model or open a microphone.
/// </summary>
[TestClass]
public sealed class IndependentWhisperAbiTests
{
    [TestMethod]
    public void NativeStructSizesMatchIndependentlyAlignedX64Header()
    {
        Assert.AreEqual(8, IntPtr.Size, "The qualified native recognition ABI is Windows x64.");
        Assert.AreEqual(48, Marshal.SizeOf<WhisperContextParameters>());
        Assert.AreEqual(304, Marshal.SizeOf<WhisperFullParameters>());
        Assert.AreEqual(12, Marshal.SizeOf<WhisperVadContextParameters>());
        Assert.AreEqual(24, Marshal.SizeOf<WhisperVadParameters>());
        Assert.AreEqual(32, Marshal.SizeOf<WhisperModelLoader>());
    }

    [TestMethod]
    public void EveryFullParameterOffsetMatchesIndependentHeaderOracle()
    {
        // C bool aligns to one, int/float/enum to four, pointer/size_t to eight.
        // The nested greedy/beam structures align to four; VAD parameters occupy 24 bytes.
        (string Name, int Offset)[] fields =
        [
            (nameof(WhisperFullParameters.Strategy), 0),
            (nameof(WhisperFullParameters.Threads), 4),
            (nameof(WhisperFullParameters.MaximumTextContext), 8),
            (nameof(WhisperFullParameters.OffsetMilliseconds), 12),
            (nameof(WhisperFullParameters.DurationMilliseconds), 16),
            (nameof(WhisperFullParameters.Translate), 20),
            (nameof(WhisperFullParameters.NoContext), 21),
            (nameof(WhisperFullParameters.NoTimestamps), 22),
            (nameof(WhisperFullParameters.SingleSegment), 23),
            (nameof(WhisperFullParameters.PrintSpecial), 24),
            (nameof(WhisperFullParameters.PrintProgress), 25),
            (nameof(WhisperFullParameters.PrintRealtime), 26),
            (nameof(WhisperFullParameters.PrintTimestamps), 27),
            (nameof(WhisperFullParameters.TokenTimestamps), 28),
            (nameof(WhisperFullParameters.TokenThreshold), 32),
            (nameof(WhisperFullParameters.TokenSumThreshold), 36),
            (nameof(WhisperFullParameters.MaximumSegmentLength), 40),
            (nameof(WhisperFullParameters.SplitOnWord), 44),
            (nameof(WhisperFullParameters.MaximumTokens), 48),
            (nameof(WhisperFullParameters.DebugMode), 52),
            (nameof(WhisperFullParameters.AudioContext), 56),
            (nameof(WhisperFullParameters.Tinydiarize), 60),
            (nameof(WhisperFullParameters.SuppressRegex), 64),
            (nameof(WhisperFullParameters.InitialPrompt), 72),
            (nameof(WhisperFullParameters.CarryInitialPrompt), 80),
            (nameof(WhisperFullParameters.PromptTokens), 88),
            (nameof(WhisperFullParameters.PromptTokenCount), 96),
            (nameof(WhisperFullParameters.Language), 104),
            (nameof(WhisperFullParameters.DetectLanguage), 112),
            (nameof(WhisperFullParameters.SuppressBlank), 113),
            (nameof(WhisperFullParameters.SuppressNonSpeechTokens), 114),
            (nameof(WhisperFullParameters.Temperature), 116),
            (nameof(WhisperFullParameters.MaximumInitialTimestamp), 120),
            (nameof(WhisperFullParameters.LengthPenalty), 124),
            (nameof(WhisperFullParameters.TemperatureIncrement), 128),
            (nameof(WhisperFullParameters.EntropyThreshold), 132),
            (nameof(WhisperFullParameters.LogProbabilityThreshold), 136),
            (nameof(WhisperFullParameters.NoSpeechThreshold), 140),
            (nameof(WhisperFullParameters.GreedyBestOf), 144),
            (nameof(WhisperFullParameters.BeamSize), 148),
            (nameof(WhisperFullParameters.BeamPatience), 152),
            (nameof(WhisperFullParameters.NewSegmentCallback), 160),
            (nameof(WhisperFullParameters.NewSegmentUserData), 168),
            (nameof(WhisperFullParameters.ProgressCallback), 176),
            (nameof(WhisperFullParameters.ProgressUserData), 184),
            (nameof(WhisperFullParameters.EncoderBeginCallback), 192),
            (nameof(WhisperFullParameters.EncoderBeginUserData), 200),
            (nameof(WhisperFullParameters.AbortCallback), 208),
            (nameof(WhisperFullParameters.AbortUserData), 216),
            (nameof(WhisperFullParameters.LogitsFilterCallback), 224),
            (nameof(WhisperFullParameters.LogitsFilterUserData), 232),
            (nameof(WhisperFullParameters.GrammarRules), 240),
            (nameof(WhisperFullParameters.GrammarRuleCount), 248),
            (nameof(WhisperFullParameters.GrammarStartRule), 256),
            (nameof(WhisperFullParameters.GrammarPenalty), 264),
            (nameof(WhisperFullParameters.Vad), 268),
            (nameof(WhisperFullParameters.VadModelPath), 272),
            (nameof(WhisperFullParameters.VadParameters), 280)
        ];
        foreach (var field in fields) AssertOffset<WhisperFullParameters>(field.Name, field.Offset);
    }

    [TestMethod]
    public void ContextVadAndLoaderOffsetsMatchIndependentHeaderOracle()
    {
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.UseGpu), 0);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.FlashAttention), 1);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.GpuDevice), 4);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwTokenTimestamps), 8);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwHeadsPreset), 12);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwTop), 16);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwHeadCount), 24);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwHeads), 32);
        AssertOffset<WhisperContextParameters>(nameof(WhisperContextParameters.DtwMemorySize), 40);
        AssertOffset<WhisperVadContextParameters>(nameof(WhisperVadContextParameters.Threads), 0);
        AssertOffset<WhisperVadContextParameters>(nameof(WhisperVadContextParameters.UseGpu), 4);
        AssertOffset<WhisperVadContextParameters>(nameof(WhisperVadContextParameters.GpuDevice), 8);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.Threshold), 0);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.MinimumSpeechMilliseconds), 4);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.MinimumSilenceMilliseconds), 8);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.MaximumSpeechSeconds), 12);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.PaddingMilliseconds), 16);
        AssertOffset<WhisperVadParameters>(nameof(WhisperVadParameters.SampleOverlap), 20);
        AssertOffset<WhisperModelLoader>(nameof(WhisperModelLoader.Context), 0);
        AssertOffset<WhisperModelLoader>(nameof(WhisperModelLoader.Read), 8);
        AssertOffset<WhisperModelLoader>(nameof(WhisperModelLoader.Eof), 16);
        AssertOffset<WhisperModelLoader>(nameof(WhisperModelLoader.Close), 24);
    }

    [TestMethod]
    public void LiteralNativeByteLayoutSeparatesFlagsPointersAndNestedVad()
    {
        var memory = Marshal.AllocHGlobal(304);
        try
        {
            Marshal.Copy(new byte[304], 0, memory, 304);
            Marshal.WriteInt32(memory, 4, 2);
            Marshal.WriteByte(memory, 21, 1);
            Marshal.WriteByte(memory, 27, 1);
            Marshal.WriteByte(memory, 80, 1);
            Marshal.WriteInt64(memory, 104, 0x1122334455667788);
            Marshal.WriteByte(memory, 114, 1);
            Marshal.WriteInt32(memory, 116, BitConverter.SingleToInt32Bits(0.25f));
            Marshal.WriteInt64(memory, 208, 0x1020304050607080);
            Marshal.WriteInt64(memory, 216, 0x0102030405060708);
            Marshal.WriteByte(memory, 268, 1);
            Marshal.WriteInt32(memory, 280, BitConverter.SingleToInt32Bits(0.5f));
            Marshal.WriteInt32(memory, 284, 250);
            Marshal.WriteInt32(memory, 288, 100);
            Marshal.WriteInt32(memory, 292, BitConverter.SingleToInt32Bits(30f));
            Marshal.WriteInt32(memory, 296, 150);
            Marshal.WriteInt32(memory, 300, BitConverter.SingleToInt32Bits(0.1f));

            var value = Marshal.PtrToStructure<WhisperFullParameters>(memory);
            Assert.AreEqual(2, value.Threads);
            Assert.AreEqual((byte)1, value.NoContext);
            Assert.AreEqual((byte)0, value.Translate);
            Assert.AreEqual((byte)1, value.PrintTimestamps);
            Assert.AreEqual((byte)0, value.TokenTimestamps);
            Assert.AreEqual((byte)1, value.CarryInitialPrompt);
            Assert.AreEqual(0L, value.PromptTokens.ToInt64());
            Assert.AreEqual(0x1122334455667788L, value.Language.ToInt64());
            Assert.AreEqual((byte)1, value.SuppressNonSpeechTokens);
            Assert.AreEqual(0.25f, value.Temperature);
            Assert.AreEqual(0x1020304050607080L, value.AbortCallback.ToInt64());
            Assert.AreEqual(0x0102030405060708L, value.AbortUserData.ToInt64());
            Assert.AreEqual((byte)1, value.Vad);
            Assert.AreEqual(0L, value.VadModelPath.ToInt64());
            Assert.AreEqual(0.5f, value.VadParameters.Threshold);
            Assert.AreEqual(250, value.VadParameters.MinimumSpeechMilliseconds);
            Assert.AreEqual(100, value.VadParameters.MinimumSilenceMilliseconds);
            Assert.AreEqual(30f, value.VadParameters.MaximumSpeechSeconds);
            Assert.AreEqual(150, value.VadParameters.PaddingMilliseconds);
            Assert.AreEqual(0.1f, value.VadParameters.SampleOverlap);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static void AssertOffset<T>(string field, int expected) where T : struct =>
        Assert.AreEqual((long)expected, Marshal.OffsetOf<T>(field).ToInt64(), typeof(T).Name + "." + field);
}
