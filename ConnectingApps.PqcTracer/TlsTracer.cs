using System.Net.Security;
using ConnectingApps.PqcTracer.TlsInspection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectingApps.PqcTracer;

public record TlsTrace(string Group, string CipherSuite);

public static class TlsTracer
{
    public static readonly HttpRequestOptionsKey<TlsTrace> TlsTraceKey = new("TlsTrace");

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

    public static IApplicationBuilder UseTlsTraceHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var connectionItems = context.Features.Get<IConnectionItemsFeature>()?.Items;

            if (connectionItems != null)
            {
                if (connectionItems.TryGetValue("TlsCipher", out var cipher))
                {
                    context.Response.Headers["X-Tls-Cipher"] = cipher?.ToString();
                }

                if (connectionItems.TryGetValue("TlsGroup", out var group))
                {
                    context.Response.Headers["X-Tls-Group"] = group?.ToString();
                }
            }

            await next();
        });
    }

    public static IHttpClientBuilder AddTlsTracing(this IHttpClientBuilder builder, Action<TlsTrace>? callback = null,
        RemoteCertificateValidationCallback? certificateValidator = null)
    {
        return builder.AddHttpMessageHandler(() => new TlsTracingHandler(callback, certificateValidator));
    }

    public static TlsTrace? GetTlsTrace(this HttpResponseMessage response)
    {
        if (response.RequestMessage == null) return null;
        return response.RequestMessage.Options.TryGetValue(TlsTraceKey, out TlsTrace? trace) ? trace : null;
    }
}