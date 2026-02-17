using System.Net.Security;

namespace ConnectingApps.PqcTracer.TlsInspection;

internal class MacOsTlsInspector
{
    public static string GetNegotiatedGroup(SslStream sslStream)
    {
        return "Non-Linux";
    }
}
