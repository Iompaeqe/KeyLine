using System.Runtime.InteropServices;
using KeyLine.Domain;

namespace KeyLine.Services.SystemActions;

public static class SystemVolumeService
{
    private const float VolumeStep = 0.05f;
    private const uint ClsCtxAll = 0x17;

    public static bool TryExecute(MacroNode node)
    {
        try
        {
            using var endpoint = AudioEndpointVolumeHandle.Create();
            var eventContext = Guid.Empty;

            switch (node.SystemVolumeAction)
            {
                case SystemVolumeAction.VolumeDown:
                    AdjustVolume(endpoint.Volume, -VolumeStep, ref eventContext);
                    return true;

                case SystemVolumeAction.MuteToggle:
                    ThrowIfFailed(endpoint.Volume.GetMute(out var isMuted));
                    ThrowIfFailed(endpoint.Volume.SetMute(!isMuted, ref eventContext));
                    return true;

                case SystemVolumeAction.Mute:
                    ThrowIfFailed(endpoint.Volume.SetMute(true, ref eventContext));
                    return true;

                case SystemVolumeAction.Unmute:
                    ThrowIfFailed(endpoint.Volume.SetMute(false, ref eventContext));
                    return true;

                case SystemVolumeAction.SetVolumePercent:
                    SetVolume(endpoint.Volume, Math.Clamp(node.SystemVolumePercent, 0, 100) / 100f, ref eventContext);
                    return true;

                default:
                    AdjustVolume(endpoint.Volume, VolumeStep, ref eventContext);
                    return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void AdjustVolume(IAudioEndpointVolume volume, float delta, ref Guid eventContext)
    {
        ThrowIfFailed(volume.GetMasterVolumeLevelScalar(out var current));
        SetVolume(volume, current + delta, ref eventContext);
    }

    private static void SetVolume(IAudioEndpointVolume volume, float level, ref Guid eventContext)
    {
        ThrowIfFailed(volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref eventContext));
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult);
    }

    private sealed class AudioEndpointVolumeHandle : IDisposable
    {
        private readonly object _enumeratorObject;
        private readonly IMMDevice _device;
        private readonly object _volumeObject;

        private AudioEndpointVolumeHandle(object enumeratorObject, IMMDevice device, object volumeObject)
        {
            _enumeratorObject = enumeratorObject;
            _device = device;
            _volumeObject = volumeObject;
            Volume = (IAudioEndpointVolume)volumeObject;
        }

        public IAudioEndpointVolume Volume { get; }

        public static AudioEndpointVolumeHandle Create()
        {
            object enumeratorObject = new MMDeviceEnumeratorComObject();
            var enumerator = (IMMDeviceEnumerator)enumeratorObject;

            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device));

            var endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
            ThrowIfFailed(device.Activate(ref endpointVolumeId, ClsCtxAll, IntPtr.Zero, out var volumeObject));

            return new AudioEndpointVolumeHandle(enumeratorObject, device, volumeObject);
        }

        public void Dispose()
        {
            ReleaseComObject(_volumeObject);
            ReleaseComObject(_device);
            ReleaseComObject(_enumeratorObject);
        }

        private static void ReleaseComObject(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }
    }

    private enum EDataFlow
    {
        Render = 0
    }

    private enum ERole
    {
        Multimedia = 1
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfaceObject);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr properties);

        [PreserveSig]
        int GetId(out IntPtr id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channelNumber, out float levelDb);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channelNumber, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
    }
}
