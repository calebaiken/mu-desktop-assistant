namespace Companion.Infrastructure.Voice;

/// <summary>Fixed allocation, seekable SAPI destination; owned PCM is cleared on disposal.</summary>
internal sealed class CappedAudioStream : Stream
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private byte[]? buffer;
    private int position;
    private int length;

    internal CappedAudioStream(int maximumBytes = MaximumBytes)
    {
        if (maximumBytes is <= 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        buffer = new byte[maximumBytes];
    }

    internal ReadOnlySpan<byte> WrittenSpan => GetBuffer().AsSpan(0, length);
    public override bool CanRead => buffer is not null;
    public override bool CanSeek => buffer is not null;
    public override bool CanWrite => buffer is not null;
    public override long Length { get { _ = GetBuffer(); return length; } }
    public override long Position { get { _ = GetBuffer(); return position; } set => Seek(value, SeekOrigin.Begin); }

    public override void Flush() => _ = GetBuffer();
    public override int Read(byte[] destination, int offset, int count) => Read(destination.AsSpan(offset, count));
    public override int Read(Span<byte> destination)
    {
        var data = GetBuffer();
        var count = Math.Min(destination.Length, Math.Max(0, length - position));
        data.AsSpan(position, count).CopyTo(destination);
        position += count;
        return count;
    }

    public override void Write(byte[] source, int offset, int count) => Write(source.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> source)
    {
        var data = GetBuffer();
        if (source.Length > data.Length - position) throw new IOException("Speech audio limit reached.");
        source.CopyTo(data.AsSpan(position));
        position += source.Length;
        length = Math.Max(length, position);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var data = GetBuffer();
        long next;
        try { next = checked((origin switch { SeekOrigin.Begin => 0, SeekOrigin.Current => position, SeekOrigin.End => length, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }) + offset); }
        catch (OverflowException) { throw new IOException("Speech audio limit reached."); }
        if (next < 0 || next > data.Length) throw new IOException("Speech audio limit reached.");
        position = (int)next;
        return position;
    }

    public override void SetLength(long value)
    {
        var data = GetBuffer();
        if (value < 0 || value > data.Length) throw new IOException("Speech audio limit reached.");
        if (value < length) data.AsSpan((int)value, length - (int)value).Clear();
        length = (int)value;
    }

    protected override void Dispose(bool disposing)
    {
        var data = Interlocked.Exchange(ref buffer, null);
        if (data is not null) Array.Clear(data);
        base.Dispose(disposing);
    }

    private byte[] GetBuffer() => buffer ?? throw new ObjectDisposedException(nameof(CappedAudioStream));
}
