using System.Runtime.InteropServices;

namespace Companion.Infrastructure.VoiceInput;

// These three interop types must be public for a COM callable wrapper. They are not registered
// COM classes, expose no capture operation, and the sink has no public constructor.
[StructLayout(LayoutKind.Sequential)]
public struct WasapiNotificationPropertyKey
{
    public Guid FormatId;
    public uint PropertyId;
}

[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IWasapiEndpointNotification
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDefaultDeviceChanged(int dataFlow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, WasapiNotificationPropertyKey key);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class WasapiEndpointNotifications : IWasapiEndpointNotification
{
    private readonly string selectedId;
    private readonly EventWaitHandle invalidated;
    internal WasapiEndpointNotifications(string selectedId, EventWaitHandle invalidated)
    { this.selectedId = selectedId; this.invalidated = invalidated; }

    private void Invalidate(string id)
    {
        if (!string.Equals(id, selectedId, StringComparison.Ordinal)) return;
        try { invalidated.Set(); } catch (ObjectDisposedException) { }
    }
    public int OnDeviceStateChanged(string id, uint state) { if (state != WasapiNative.DeviceActive) Invalidate(id); return 0; }
    public int OnDeviceAdded(string id) => 0;
    public int OnDeviceRemoved(string id) { Invalidate(id); return 0; }
    public int OnDefaultDeviceChanged(int dataFlow, int role, string? id) => 0; // Fixed selection never follows a default.
    public int OnPropertyValueChanged(string id, WasapiNotificationPropertyKey key) => 0;
}
