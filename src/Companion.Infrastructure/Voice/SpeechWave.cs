using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Companion.Application.Voice;

namespace Companion.Infrastructure.Voice;

internal static class SpeechWave
{
    internal static SpeechAudioProbe Inspect(ReadOnlySpan<byte> wave, SpeechVoice voice)
    {
        SpeechProtocol.ValidateVoice(voice);
        if (wave.Length < 44 || wave.Length > CappedAudioStream.MaximumBytes ||
            !wave[..4].SequenceEqual("RIFF"u8) || !wave.Slice(8, 4).SequenceEqual("WAVE"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(wave[4..]) != wave.Length - 8)
            throw SpeechProtocol.Invalid();
        var offset = 12;
        var sampleRate = 0;
        var channels = 0;
        var bits = 0;
        var dataBytes = 0;
        var hasFormat = false;
        var hasData = false;
        var nonzero = false;
        var chunks = new List<SpeechWaveChunk>();
        while (offset < wave.Length)
        {
            if (chunks.Count >= 32 || wave.Length - offset < 8) throw SpeechProtocol.Invalid();
            var length = BinaryPrimitives.ReadUInt32LittleEndian(wave[(offset + 4)..]);
            if (length > wave.Length - offset - 8) throw SpeechProtocol.Invalid();
            var idBytes = wave.Slice(offset, 4);
            if (idBytes.ContainsAnyExceptInRange((byte)32, (byte)126)) throw SpeechProtocol.Invalid();
            var id = Encoding.ASCII.GetString(idBytes);
            chunks.Add(new SpeechWaveChunk(id, offset, (int)length));
            var payload = wave.Slice(offset + 8, (int)length);
            if (id == "fmt ")
            {
                if (hasFormat || length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(payload) != 1)
                    throw SpeechProtocol.Invalid();
                hasFormat = true;
                channels = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
                var rate = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
                if (rate is < 8_000 or > 96_000 || channels is < 1 or > 2 || bits is not (8 or 16 or 24 or 32))
                    throw SpeechProtocol.Invalid();
                sampleRate = (int)rate;
                var alignment = channels * bits / 8;
                if (BinaryPrimitives.ReadUInt16LittleEndian(payload[12..]) != alignment ||
                    BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]) != sampleRate * alignment)
                    throw SpeechProtocol.Invalid();
            }
            else if (id == "data")
            {
                if (hasData || !hasFormat || length == 0 || length % (channels * bits / 8) != 0)
                    throw SpeechProtocol.Invalid();
                hasData = true;
                dataBytes = (int)length;
                // Unsigned eight-bit PCM silence is 128; wider integer PCM silence is zero.
                nonzero = payload.IndexOfAnyExcept(bits == 8 ? (byte)128 : (byte)0) >= 0;
            }
            offset += 8 + (int)length;
            if ((length & 1) != 0) offset++;
            if (offset > wave.Length) throw SpeechProtocol.Invalid();
        }
        if (!hasFormat || !hasData || offset != wave.Length) throw SpeechProtocol.Invalid();
        var result = new SpeechAudioProbe(voice, wave.Length, dataBytes, sampleRate, channels, bits,
            nonzero, Convert.ToHexString(SHA256.HashData(wave)), chunks.AsReadOnly());
        ValidateMetadata(result);
        return result;
    }

    internal static void ValidateMetadata(SpeechAudioProbe value)
    {
        SpeechProtocol.ValidateVoice(value.Voice);
        if (value.TotalBytes is < 44 or > CappedAudioStream.MaximumBytes ||
            value.SampleRate is < 8_000 or > 96_000 || value.Channels is < 1 or > 2 ||
            value.BitsPerSample is not (8 or 16 or 24 or 32) || value.DataBytes <= 0 ||
            value.DataBytes > value.TotalBytes - 44 || value.DataBytes % (value.Channels * value.BitsPerSample / 8) != 0 ||
            value.DurationSeconds > 30 || value.Chunks is null || value.Chunks.Count is < 2 or > 32 ||
            value.Sha256 is null || value.Sha256.Length != 64 || value.Sha256.Any(item => !char.IsAsciiHexDigit(item)))
            throw SpeechProtocol.Invalid();
        var offset = 12;
        var formatCount = 0;
        var dataCount = 0;
        foreach (var chunk in value.Chunks)
        {
            if (chunk is null || chunk.Id is null || chunk.Id.Length != 4 || chunk.Id.Any(item => item is < ' ' or > '~') ||
                chunk.Offset != offset || chunk.Length < 0 || chunk.Length > value.TotalBytes - offset - 8)
                throw SpeechProtocol.Invalid();
            if (chunk.Id == "fmt ") { if (chunk.Length < 16 || dataCount > 0) throw SpeechProtocol.Invalid(); formatCount++; }
            if (chunk.Id == "data") { if (chunk.Length != value.DataBytes) throw SpeechProtocol.Invalid(); dataCount++; }
            offset += 8 + chunk.Length + (chunk.Length & 1);
        }
        if (offset != value.TotalBytes || formatCount != 1 || dataCount != 1) throw SpeechProtocol.Invalid();
    }
}
