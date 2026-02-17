using System.Net.Security;

namespace ConnectingApps.PqcTracer.TlsInspection;

internal static class WindowsTlsInspector
{
    public static string GetNegotiatedGroup(SslStream sslStream)
    {
        return "Non-Linux";
    }
}
