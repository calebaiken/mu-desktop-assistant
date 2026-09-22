namespace Companion.Infrastructure.Storage;

// Narrow fault-injection seam for disk-full/interrupted-write tests.
public interface IAtomicFileWriter
{
    void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents);
}
