using System.Runtime.InteropServices;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

/// <summary>Narrow Windows SDK COM declarations. All acquired interfaces stay on one owned STA.</summary>
internal static class WasapiNative
{
    internal const uint EventCallback = 0x00040000;
    internal const uint AutoConvertPcm = 0x80000000;
    internal const uint SrcDefaultQuality = 0x08000000;
    internal const uint DeviceActive = 1;
    internal const uint Silent = 2;
    internal const uint Discontinuity = 1;
    internal const uint TimestampError = 4;
    internal const int BufferEmpty = 0x08890001;
    internal static readonly Guid AudioClientId = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    internal static readonly Guid CaptureClientId = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    internal static void Check(int result)
    {
        if (result >= 0) return;
        var failure = unchecked((uint)result) switch
        {
            0x80070005 => AudioInputFailure.PermissionDenied,
            0x88890004 or 0x88890026 => AudioInputFailure.DeviceLost,
            _ => AudioInputFailure.Unavailable
        };
        throw new AudioInputException(failure);
    }

    internal static DeviceEnumerator CreateEnumerator() => (DeviceEnumerator)new EnumeratorComObject();

    internal static void Release(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
    }

    internal static string ReadBoundedString(IntPtr pointer, int maximum)
    {
        if (pointer == IntPtr.Zero) throw new AudioInputException(AudioInputFailure.Unavailable);
        var characters = new char[maximum];
        for (var index = 0; index <= maximum; index++)
        {
            var value = (char)Marshal.ReadInt16(pointer, checked(index * 2));
            if (value == '\0') return new string(characters, 0, index);
            if (index == maximum) break;
            characters[index] = value;
        }
        throw new AudioInputException(AudioInputFailure.Unavailable);
    }

    [DllImport("ole32.dll")]
    internal static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")]
    internal static extern void CoUninitialize();
    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropertyValue value);

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct WaveFormat
    {
        internal ushort FormatTag, Channels;
        internal uint SamplesPerSecond, AverageBytesPerSecond;
        internal ushort BlockAlign, BitsPerSample, ExtraSize;
        internal static WaveFormat Pcm16Mono => new()
        {
            FormatTag = 1, Channels = 1, SamplesPerSecond = 16000,
            AverageBytesPerSecond = 32000, BlockAlign = 2, BitsPerSample = 16, ExtraSize = 0
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        internal Guid FormatId;
        internal uint PropertyId;
        internal static PropertyKey FriendlyName => new()
        { FormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0"), PropertyId = 14 };
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyBlob { internal uint Length; internal IntPtr Data; }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropertyValue
    {
        [FieldOffset(0)] internal ushort Type;
        [FieldOffset(8)] internal IntPtr Pointer;
        // PROPVARIANT's union contains a counted pointer; this preserves its native x86/x64 size.
        [FieldOffset(8)] internal PropertyBlob Blob;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), ClassInterface(ClassInterfaceType.None)]
    private class EnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface DeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out DeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out Device device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out Device device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IWasapiEndpointNotification client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IWasapiEndpointNotification client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-C0A3D4B73FE6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface DeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out Device device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface Device
    {
        [PreserveSig] int Activate(ref Guid id, uint context, IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out PropertyStore properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface Endpoint
    {
        [PreserveSig] int GetDataFlow(out int dataFlow);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface PropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropertyValue value);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface AudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity,
            ref WaveFormat format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int shareMode, ref WaveFormat format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface CaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong counterPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

}
