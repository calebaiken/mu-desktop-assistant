using System.Diagnostics;
using System.Runtime.InteropServices;
using Companion.Application.Voice;

namespace Companion.Infrastructure.VoiceInput;

internal static class WasapiCapture
{
    internal static IReadOnlyList<AudioInputDevice> Enumerate(CancellationToken token)
    {
        WasapiNative.Check(WasapiNative.CoInitializeEx(IntPtr.Zero, 2));
        WasapiNative.DeviceEnumerator? enumerator = null;
        WasapiNative.DeviceCollection? collection = null;
        try
        {
            token.ThrowIfCancellationRequested();
            enumerator = WasapiNative.CreateEnumerator();
            WasapiNative.Check(enumerator.EnumAudioEndpoints(1, WasapiNative.DeviceActive, out collection));
            WasapiNative.Check(collection.GetCount(out var count));
            if (count > AudioInputLimits.MaximumDevices) throw new AudioInputException(AudioInputFailure.Unavailable);
            List<AudioInputDevice> devices = [];
            for (uint index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                WasapiNative.Device? device = null;
                try
                {
                    WasapiNative.Check(collection.Item(index, out device));
                    devices.Add(ReadDevice(device));
                }
                finally { WasapiNative.Release(device); }
            }
            return devices.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        }
        finally
        {
            WasapiNative.Release(collection);
            WasapiNative.Release(enumerator);
            WasapiNative.CoUninitialize();
        }
    }

    internal static bool Capture(AudioCaptureRequest request, CapturePacketizer destination,
        Action started, WaitHandle stop, CancellationToken cancellationToken)
    {
        CaptureProtocol.ValidateRequest(request);
        WasapiNative.Check(WasapiNative.CoInitializeEx(IntPtr.Zero, 2));
        WasapiNative.DeviceEnumerator? enumerator = null;
        WasapiNative.Device? device = null;
        WasapiNative.AudioClient? audio = null;
        WasapiNative.CaptureClient? capture = null;
        using var available = new AutoResetEvent(false);
        using var invalidated = new ManualResetEvent(false);
        var notification = new WasapiEndpointNotifications(request.DeviceId, invalidated);
        var registered = false;
        var startedNative = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stop.WaitOne(0)) return false;
            enumerator = WasapiNative.CreateEnumerator();
            WasapiNative.Check(enumerator.RegisterEndpointNotificationCallback(notification));
            registered = true;
            WasapiNative.Check(enumerator.GetDevice(request.DeviceId, out device));
            WasapiNative.Check(device.GetState(out var state));
            WasapiNative.Check(((WasapiNative.Endpoint)device).GetDataFlow(out var flow));
            if (state != WasapiNative.DeviceActive || flow != 1 || !string.Equals(ReadDevice(device).Id, request.DeviceId, StringComparison.Ordinal))
                throw new AudioInputException(AudioInputFailure.DeviceLost);
            cancellationToken.ThrowIfCancellationRequested();
            if (stop.WaitOne(0)) return false;
            var id = WasapiNative.AudioClientId;
            WasapiNative.Check(device.Activate(ref id, 0x17, IntPtr.Zero, out var activated));
            audio = (WasapiNative.AudioClient)activated;
            var format = WasapiNative.WaveFormat.Pcm16Mono;
            // The engine performs real channel/rate conversion. No mix-format data is relabelled.
            // IsFormatSupported has no conversion-flags argument, so Initialize is authoritative here.
            WasapiNative.Check(audio.Initialize(0, WasapiNative.EventCallback | WasapiNative.AutoConvertPcm |
                WasapiNative.SrcDefaultQuality, 0, 0, ref format, IntPtr.Zero));
            // Set the event immediately after Initialize, including failure paths before Start.
            WasapiNative.Check(audio.SetEventHandle(available.SafeWaitHandle.DangerousGetHandle()));
            WasapiNative.Check(audio.GetBufferSize(out var bufferFrames));
            using var reader = new WasapiPacketReader(bufferFrames);
            id = WasapiNative.CaptureClientId;
            WasapiNative.Check(audio.GetService(ref id, out var service));
            capture = (WasapiNative.CaptureClient)service;
            var packets = new WasapiCapturePackets(capture);
            cancellationToken.ThrowIfCancellationRequested();
            if (invalidated.WaitOne(0)) throw new AudioInputException(AudioInputFailure.DeviceLost);
            if (stop.WaitOne(0)) return false;
            WasapiNative.Check(audio.Start());
            startedNative = true;
            var duration = Stopwatch.StartNew();
            started();
            WaitHandle[] waits = [cancellationToken.WaitHandle, invalidated, stop, available];
            var durationLimited = false;
            bool ShouldStop() => cancellationToken.IsCancellationRequested || invalidated.WaitOne(0) || stop.WaitOne(0) ||
                duration.Elapsed >= AudioInputLimits.CaptureDuration;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (invalidated.WaitOne(0)) throw new AudioInputException(AudioInputFailure.DeviceLost);
                if (stop.WaitOne(0)) break;
                if (destination.LimitReached || duration.Elapsed >= AudioInputLimits.CaptureDuration)
                { durationLimited = true; break; }
                var milliseconds = Math.Max(1, (int)Math.Ceiling((AudioInputLimits.CaptureDuration - duration.Elapsed).TotalMilliseconds));
                var ready = WaitHandle.WaitAny(waits, milliseconds);
                if (ready == 3) reader.Drain(packets, destination, ShouldStop);
                // Every wake, including timeout/cancel/device notification, returns through the same guards.
            }
            WasapiNative.Check(audio.Stop());
            startedNative = false;
            destination.Finish();
            return durationLimited;
        }
        finally
        {
            // This entire method executes on one owned STA; all native releases do too.
            try { if (startedNative && audio is not null) WasapiNative.Check(audio.Stop()); }
            finally
            {
                try
                {
                    if (registered && enumerator is not null)
                        WasapiNative.Check(enumerator.UnregisterEndpointNotificationCallback(notification));
                }
                finally
                {
                    WasapiNative.Release(capture);
                    WasapiNative.Release(audio);
                    WasapiNative.Release(device);
                    WasapiNative.Release(enumerator);
                    GC.KeepAlive(notification);
                    WasapiNative.CoUninitialize();
                }
            }
        }
    }

    private static AudioInputDevice ReadDevice(WasapiNative.Device device)
    {
        IntPtr idPointer = IntPtr.Zero;
        WasapiNative.PropertyStore? properties = null;
        try
        {
            WasapiNative.Check(device.GetId(out idPointer));
            var id = WasapiNative.ReadBoundedString(idPointer, AudioInputLimits.MaximumDeviceIdCharacters);
            WasapiNative.Check(device.OpenPropertyStore(0, out properties));
            var key = WasapiNative.PropertyKey.FriendlyName;
            WasapiNative.Check(properties.GetValue(ref key, out var value));
            try
            {
                if (value.Type != 31) throw new AudioInputException(AudioInputFailure.Unavailable);
                var name = WasapiNative.ReadBoundedString(value.Pointer, AudioInputLimits.MaximumDeviceNameCharacters);
                var result = new AudioInputDevice(id, name);
                CaptureProtocol.ValidateDevice(result);
                return result;
            }
            finally { WasapiNative.Check(WasapiNative.PropVariantClear(ref value)); }
        }
        finally
        {
            if (idPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(idPointer);
            WasapiNative.Release(properties);
        }
    }

}
