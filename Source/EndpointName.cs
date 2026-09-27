using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
namespace DuoMix;
[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioPolicy
{
    void GetMixFormat(); void GetDeviceFormat(); void ResetDeviceFormat(); void SetDeviceFormat();
    void GetProcessingPeriod(); void SetProcessingPeriod(); void GetShareMode(); void SetShareMode(); void GetPropertyValue();
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.Bool)] bool fx, ref PropertyKey key, ref PropVariant value);
}
static class EndpointName
{
    public static void Set(string id, ref PropVariant value)
    {
        var obj = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"))!)!;
        try { var key = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2); Marshal.ThrowExceptionForHR(((IAudioPolicy)obj).SetPropertyValue(id, false, ref key, ref value)); }
        finally { Marshal.FinalReleaseComObject(obj); }
    }
}

