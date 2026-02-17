using System.Net.Security;
using ConnectingApps.PqcTracer.TlsInspection;
using Microsoft.AspNetCore.Hosting;

namespace ConnectingApps.PqcTracer;

public record TlsTrace(string Group, string CipherSuite);

public static class TlsTracer
{
    public static void TraceTlsConnection(this IWebHostBuilder builder, Action<TlsTrace>? callback = null)
    {
        callback ??= _ => { };
        builder.ConfigureKestrel(kestrel =>
        {
            kestrel.ConfigureHttpsDefaults(https =>
            {
                // 1. We must use OnAuthenticate to get access to the low-level options
                https.OnAuthenticate = (context, sslOptions) =>
                {
                    sslOptions.RemoteCertificateValidationCallback = (sender, _, _, _) =>
                    {
                        if (sender is SslStream sslStream)
                        {
                            var group = GeneralTlsInspector.GetNegotiatedGroup(sslStream);
                            var cipher = sslStream.NegotiatedCipherSuite.ToString();
                            
                            context.Items["TlsCipher"] = cipher;
                            context.Items["TlsGroup"] = group;
                            callback(new TlsTrace(group, cipher));
                        }

                        return true;
                    };
                };
            });
        });
    }
}