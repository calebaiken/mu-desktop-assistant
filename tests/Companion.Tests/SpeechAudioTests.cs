using System.Buffers.Binary;
using System.Text;
using Companion.Application.Voice;
using Companion.Infrastructure.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class SpeechAudioTests
{
    private static readonly SpeechVoice Voice = new("TTS_MS_TEST", "Microsoft Test", "en-US");

    [TestMethod]
    public void FixedBufferRejectsOverflowBeforeChangingExistingDataAndClearsOnDispose()
    {
        var stream = new CappedAudioStream(16);
        stream.Write(Enumerable.Repeat((byte)73, 16).ToArray());
        Assert.ThrowsExactly<IOException>(() => stream.WriteByte(1));
        Assert.AreEqual(16L, stream.Length);
        Assert.IsTrue(stream.WrittenSpan.ToArray().All(item => item == 73));
        var owned = stream.WrittenSpan;
        stream.Dispose();
        Assert.AreEqual(-1, owned.IndexOfAnyExcept((byte)0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = stream.Length);
    }

    [TestMethod]
    public void BufferAllowsHeaderRewriteAndClearsTruncationWithoutGrowingAllocation()
    {
        using var stream = new CappedAudioStream(16);
        stream.Write(Enumerable.Repeat((byte)99, 16).ToArray());
        stream.SetLength(4);
        stream.Position = 8;
        stream.WriteByte(12);
        Assert.AreEqual(9L, stream.Length);
        CollectionAssert.AreEqual(new byte[] { 99, 99, 99, 99, 0, 0, 0, 0, 12 }, stream.WrittenSpan.ToArray());
        stream.Position = 0;
        stream.WriteByte(7);
        Assert.AreEqual(7, stream.WrittenSpan[0]);
        Assert.ThrowsExactly<IOException>(() => stream.Seek(long.MaxValue, SeekOrigin.Current));
        Assert.ThrowsExactly<IOException>(() => stream.SetLength(17));
        Assert.AreEqual(9L, stream.Length);
    }

    [TestMethod]
    public void WaveUsesRealChunkOffsetsPaddingAndPcmArithmetic()
    {
        var pcm = new byte[640];
        pcm[20] = 1;
        var wave = Wave(pcm, junk: true);
        var result = SpeechWave.Inspect(wave, Voice);
        Assert.AreEqual(640, result.DataBytes);
        Assert.AreEqual(696, result.TotalBytes);
        Assert.AreEqual(0.02, result.DurationSeconds, 0.0000001);
        Assert.AreEqual(3, result.Chunks.Count);
        Assert.AreEqual(new SpeechWaveChunk("JUNK", 36, 3), result.Chunks[1]);
        Assert.IsTrue(result.HasNonZeroSamples);
        Assert.AreEqual(64, result.Sha256.Length);
        var parsed = SpeechProtocol.ParseEvent(SpeechProtocol.SerializeEvent(new SpeechEvent(1, Guid.NewGuid(), "probe", Probe: result)));
        Assert.AreEqual(result.Sha256, parsed.Probe!.Sha256);
        Assert.AreEqual(result.DurationSeconds, parsed.Probe.DurationSeconds);
    }

    [TestMethod]
    public void EightBitPcmSilenceIsUnsignedMidpoint()
    {
        var pcm = Enumerable.Repeat((byte)128, 160).ToArray();
        Assert.IsFalse(SpeechWave.Inspect(Wave(pcm, bits: 8), Voice).HasNonZeroSamples);
        pcm[0] = 0;
        Assert.IsTrue(SpeechWave.Inspect(Wave(pcm, bits: 8), Voice).HasNonZeroSamples);
    }

    [TestMethod]
    public void MalformedWaveSizesAndFormatsFailWithContentFreeCode()
    {
        var valid = Wave(new byte[320]);
        var mutations = new Action<byte[]>[]
        {
            bytes => bytes[0] = 0,
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uint.MaxValue),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), uint.MaxValue),
            bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 3),
            bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 0),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), uint.MaxValue),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 1),
            bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 1),
            bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 15),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), 319)
        };
        foreach (var mutate in mutations)
        {
            var corrupted = (byte[])valid.Clone();
            mutate(corrupted);
            var error = Assert.ThrowsExactly<SpeechException>(() => SpeechWave.Inspect(corrupted, Voice));
            Assert.AreEqual(SpeechFailure.ProtocolError, error.Failure);
            Assert.IsNull(error.InnerException);
        }
    }

    [TestMethod]
    public void DiagnosticDurationLimitAdmitsThirtySecondsAndRejectsOneMoreSample()
    {
        var exact = Wave(new byte[30 * 16_000 * 2]);
        Assert.AreEqual(30d, SpeechWave.Inspect(exact, Voice).DurationSeconds);
        var tooLong = Wave(new byte[30 * 16_000 * 2 + 2]);
        Assert.ThrowsExactly<SpeechException>(() => SpeechWave.Inspect(tooLong, Voice));
    }

    [TestMethod]
    public void MetadataRejectsDuplicateDataAndImpossibleChunkOffsets()
    {
        var valid = SpeechWave.Inspect(Wave(new byte[320]), Voice);
        Assert.ThrowsExactly<SpeechException>(() => SpeechWave.ValidateMetadata(valid with
        { Chunks = [new("fmt ", 12, 16), new("data", 37, 320)] }));
        Assert.ThrowsExactly<SpeechException>(() => SpeechWave.ValidateMetadata(valid with
        { DataBytes = int.MaxValue }));
        Assert.ThrowsExactly<SpeechException>(() => SpeechWave.ValidateMetadata(valid with
        { SampleRate = int.MaxValue }));
        Assert.ThrowsExactly<SpeechException>(() => SpeechWave.ValidateMetadata(valid with
        { Sha256 = "content must not escape" }));
    }

    [TestMethod]
    public void WorkerStartUsesOnlyPrivatePipesAndFixedArgumentWithoutInheritedHooks()
    {
        var start = WindowsSpeechOutput.BuildStartInfo(@"C:\Program Files\Companion\Companion.Desktop.exe");
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.IsTrue(start.RedirectStandardInput && start.RedirectStandardOutput && start.RedirectStandardError);
        CollectionAssert.AreEqual(new[] { "--speech-worker" }, start.ArgumentList.ToArray());
        Assert.IsFalse(start.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
        Assert.IsFalse(start.Environment.ContainsKey("HTTP_PROXY"));
        Assert.IsFalse(start.Environment.ContainsKey("OPENAI_API_KEY"));
    }

    private static byte[] Wave(byte[] pcm, int bits = 16, bool junk = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(0);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(16_000);
        writer.Write(16_000 * bits / 8);
        writer.Write((ushort)(bits / 8));
        writer.Write((ushort)bits);
        if (junk) { writer.Write("JUNK"u8); writer.Write(3); writer.Write(new byte[] { 1, 2, 3, 0 }); }
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        if ((pcm.Length & 1) != 0) writer.Write((byte)0);
        writer.Flush();
        var result = stream.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), result.Length - 8);
        return result;
    }
}
