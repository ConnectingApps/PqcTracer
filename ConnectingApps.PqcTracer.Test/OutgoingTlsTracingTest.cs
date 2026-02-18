using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConnectingApps.PqcTracer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ConnectingApps.PqcTracer.Test;

public class OutgoingTlsTracingTest
{
    [Fact]
    public async Task HttpClient_WithTlsTracing_CapturesValidNegotiatedGroup()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        using var certificate = CreateSelfSignedCertificate("CN=localhost");
        await using var app = CreateHttpsApp(certificate);
        await app.StartAsync();

        var baseAddress = GetServerBaseAddress(app);
        using var client = new HttpClient(new TlsTracingHandler(certificateValidator: (_, _, _, _) => true))
        {
            BaseAddress = baseAddress
        };

        using var response = await client.GetAsync("/ping");
        response.EnsureSuccessStatusCode();

        var trace = response.GetTlsTrace();
        Assert.NotNull(trace);
        Assert.False(string.IsNullOrWhiteSpace(trace!.CipherSuite));
        Assert.False(string.IsNullOrWhiteSpace(trace.Group));
        Assert.False(IsInvalidGroup(trace.Group), $"Unexpected TLS group value: {trace.Group}");
    }

    private static WebApplication CreateHttpsApp(X509Certificate2 certificate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listenOptions => listenOptions.UseHttps(certificate));
        });

        var app = builder.Build();
        app.MapGet("/ping", () => Results.Ok("pong"));
        return app;
    }

    private static Uri GetServerBaseAddress(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses == null || addresses.Count == 0)
        {
            throw new InvalidOperationException("No server addresses were assigned.");
        }

        return new Uri(addresses.First());
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static bool IsInvalidGroup(string group)
    {
        if (group.StartsWith("Err:", StringComparison.OrdinalIgnoreCase)) return true;
        if (group.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) return true;
        if (group.StartsWith("Non-Linux", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
