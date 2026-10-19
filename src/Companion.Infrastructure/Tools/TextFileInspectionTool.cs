using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Companion.Application.Tools;
using Microsoft.Win32.SafeHandles;

namespace Companion.Infrastructure.Tools;

/// <summary>Read-only Windows file capability. Never follows a reparse point.</summary>
public sealed class TextFileInspectionTool : ITool
{
    public ToolDescriptor Descriptor { get; } = new(TextInspection.ToolId, 1, "Inspect one selected plain-text file (read only)", ToolRisk.Confirm);

    public Task<IPreparedTool> PrepareAsync(ToolProposal proposal, CancellationToken cancellationToken)
        => Task.Run<IPreparedTool>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) throw new ToolException(ToolFailure.Unavailable);
            var path = ValidatePath(proposal.Arguments.Path);
            if (GetDriveType(path[..3]) != 3) throw new ToolException(ToolFailure.InvalidRequest);
            var leases = new List<Lease>();
            FileStream? stream = null;
            try
            {
                // Retain each ancestor without write/delete sharing. Checking only
                // the leaf allows an attacker to replace a directory after consent.
                leases.Add(Open(path[..3], directory: true));
                for (var index = 3; index < path.Length; index++)
                    if (path[index] == '\\') leases.Add(Open(path[..index], directory: true));
                var leaf = Open(path, directory: false);
                leases.Add(leaf);
                var bytes = leaf.Identity.Size;
                if (bytes > TextInspection.MaxBytes) throw new ToolException(ToolFailure.TooLarge);
                stream = new FileStream(leaf.Handle, FileAccess.Read, 4096, isAsync: true);
                cancellationToken.ThrowIfCancellationRequested();
                return new Prepared(proposal.Id, path, leases, stream);
            }
            catch (ToolException) { stream?.Dispose(); foreach (var lease in leases) lease.Handle.Dispose(); throw; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { stream?.Dispose(); foreach (var lease in leases) lease.Handle.Dispose(); throw new ToolException(ToolFailure.Unavailable); }
            catch { stream?.Dispose(); foreach (var lease in leases) lease.Handle.Dispose(); throw; }
        }, cancellationToken);

    public static string ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length is < 4 or > 1024 || !char.IsAsciiLetter(path[0]) ||
            path[1] != ':' || path[2] != '\\' || path.AsSpan(2).Contains(':') ||
            path.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format || "<>\"|?*/".Contains(c)))
            throw new ToolException(ToolFailure.InvalidRequest);
        var parts = path[3..].Split('\\');
        if (parts.Length > 32) throw new ToolException(ToolFailure.InvalidRequest);
        try { _ = new UTF8Encoding(false, true).GetByteCount(path); }
        catch (EncoderFallbackException) { throw new ToolException(ToolFailure.InvalidRequest); }
        foreach (var rune in path.EnumerateRunes())
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format) throw new ToolException(ToolFailure.InvalidRequest);
        foreach (var part in parts)
        {
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (part.Length == 0 || part.EndsWith(' ') || part.EndsWith('.') || part is "." or ".." ||
                stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 "123456789¹²³".Contains(stem[3]))) throw new ToolException(ToolFailure.InvalidRequest);
        }
        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new ToolException(ToolFailure.InvalidRequest);
        return path;
    }

    private sealed record Lease(string Path, SafeFileHandle Handle, FileIdentity Identity);
    private sealed record FileIdentity(uint Volume, ulong Index, ulong Size, long Created, long Written);

    private static Lease Open(string path, bool directory)
    {
        const uint openReparsePoint = 0x00200000, backupSemantics = 0x02000000, overlapped = 0x40000000;
        var handle = CreateFile(@"\\?\" + path, directory ? 0x80u : 0x80000080u, 1, IntPtr.Zero, 3,
            openReparsePoint | (directory ? backupSemantics : overlapped), IntPtr.Zero);
        try
        {
            if (handle.IsInvalid || GetFileType(handle) != 1) throw new ToolException(ToolFailure.Unavailable);
            var identity = Inspect(handle, path, directory);
            return new(path, handle, identity);
        }
        catch { handle.Dispose(); throw; }
    }

    private static FileIdentity Inspect(SafeFileHandle handle, string path, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var info)) throw new ToolException(ToolFailure.Unavailable);
        // Refuse links, devices, offline and recall-on-access files before reading.
        const uint refused = 0x400 | 0x40 | 0x1000 | 0x40000 | 0x400000;
        if ((info.Attributes & refused) != 0 || ((info.Attributes & 0x10) != 0) != directory)
            throw new ToolException(ToolFailure.InvalidRequest);
        var finalPath = new StringBuilder(1100);
        var length = GetFinalPathNameByHandle(handle, finalPath, (uint)finalPath.Capacity, 0);
        if (length == 0 || length >= finalPath.Capacity || !string.Equals(finalPath.ToString(), @"\\?\" + path, StringComparison.OrdinalIgnoreCase))
            throw new ToolException(ToolFailure.Stale);
        return new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow, ((ulong)info.SizeHigh << 32) | info.SizeLow,
            ((long)info.CreationHigh << 32) | info.CreationLow, ((long)info.WriteHigh << 32) | info.WriteLow);
    }

    private sealed class Prepared(Guid id, string path, List<Lease> leases, FileStream stream) : IPreparedTool
    {
        public ToolResource Resource { get; } = new(path, $"{leases[^1].Identity.Volume:X8}:{leases[^1].Identity.Index:X16}:{leases[^1].Identity.Written:X16}", (long)leases[^1].Identity.Size);
        public bool IsCurrent()
        {
            foreach (var lease in leases)
            {
                var current = Inspect(lease.Handle, lease.Path, directory: !ReferenceEquals(lease, leases[^1]));
                if (current.Volume != lease.Identity.Volume || current.Index != lease.Identity.Index ||
                    (ReferenceEquals(lease, leases[^1]) && current != lease.Identity)) return false;
            }
            return true;
        }

        public Task<string> ExecuteAsync(AuthorizedContext authorization, CancellationToken cancellationToken)
        {
            if (authorization.ProposalId != id) throw new ToolException(ToolFailure.Denied);
            return BoundedTextReader.ReadAsync(stream, cancellationToken);
        }
        public void Dispose() { try { stream.Dispose(); } finally { foreach (var lease in leases) lease.Handle.Dispose(); } }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveType(string root);
}
