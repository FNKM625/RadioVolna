using System.Runtime.Versioning;
using Android.OS;

namespace RadioVolna;

public partial class AudioService
{
    [SupportedOSPlatformGuard("android23.0")]
    private static bool IsAndroid23OrHigher()
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.M;
    }

    [SupportedOSPlatformGuard("android26.0")]
    private static bool IsAndroid26OrHigher()
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.O;
    }

    [SupportedOSPlatformGuard("android33.0")]
    private static bool IsAndroid33OrHigher()
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu;
    }
}
