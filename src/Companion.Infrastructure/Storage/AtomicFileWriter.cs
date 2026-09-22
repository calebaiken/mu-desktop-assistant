namespace Companion.Infrastructure.Storage;

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents)
    {
        var temporary = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(targetPath)) File.Replace(temporary, targetPath, backupPath);
            else File.Move(temporary, targetPath);
        }
        finally
        {
            // A failed cleanup is reported, never silently ignored; the original remains intact.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
