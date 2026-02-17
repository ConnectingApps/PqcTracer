using System.Net.Security;
using System.Runtime.InteropServices;

namespace ConnectingApps.PqcTracer.TlsInspection;

internal class GeneralTlsInspector
{
    public static string GetNegotiatedGroup(SslStream sslStream)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LinuxTlsInspector.GetNegotiatedGroup(sslStream);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacOsTlsInspector.GetNegotiatedGroup(sslStream);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsTlsInspector.GetNegotiatedGroup(sslStream);
        throw new PlatformNotSupportedException("Unsupported platform");
    }
}
