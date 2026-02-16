using System.Net.Security;
using Microsoft.AspNetCore.Connections;
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
                https.OnAuthenticate = (ConnectionContext context, SslServerAuthenticationOptions sslOptions) =>
                {
                    sslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
                    {
                        if (sender is SslStream sslStream)
                        {
                            var group = TlsInspector.GetNegotiatedGroup(sslStream);
                            var cipher = sslStream.NegotiatedCipherSuite.ToString();

                            context.Items["TlsCipher"] = cipher;
                            context.Items["TlsGroup"] = group;
                            callback(new TlsTrace(group, cipher));

                            Console.WriteLine($"[TLS] Cipher: {cipher} | Group: {group}");
                        }

                        return true;
                    };
                };
            });
        });
    }
}