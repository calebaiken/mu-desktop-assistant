using System.Runtime.InteropServices;

namespace Companion.Infrastructure.Conversation;

internal static class LocalListenerOwner
{
    internal static bool IsOwnedBy(int port, int processId)
    {
        var size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
        if (result != 122 || size is < 4 or > 4 * 1024 * 1024) return false;
        var table = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(table, ref size, false, 2, 3, 0);
            if (result != 0) return false;
            var rows = Marshal.ReadInt32(table);
            if (rows < 0 || rows > (size - 4) / 24) return false;
            for (var index = 0; index < rows; index++)
            {
                var row = table + 4 + index * 24;
                var localPort = Marshal.ReadByte(row, 8) * 256 + Marshal.ReadByte(row, 9);
                if (localPort == port && Marshal.ReadInt32(row, 20) == processId &&
                    Marshal.ReadByte(row, 4) == 127 && Marshal.ReadByte(row, 5) == 0 &&
                    Marshal.ReadByte(row, 6) == 0 && Marshal.ReadByte(row, 7) == 1) return true;
            }
            return false;
        }
        finally { Marshal.FreeHGlobal(table); }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order, uint addressFamily, int tableClass, uint reserved);
}
