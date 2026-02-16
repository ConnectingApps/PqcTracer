using System.Net.Security;
using ConnectingApps.PqcTracer;
using Microsoft.AspNetCore.Connections;
using ConnectingApps.PqcTracer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ConfigureHttpsDefaults(https =>
    {
        // 1. We must use OnAuthenticate to get access to the low-level options
        https.OnAuthenticate = (ConnectionContext context, SslServerAuthenticationOptions sslOptions) =>
        {
            // 2. We assign to 'RemoteCertificateValidationCallback' (NOT ClientCertificateValidation)
            // This delegate signature IS (sender, certificate, chain, errors)
            sslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                // 3. Now 'sender' is available and is the SslStream
                if (sender is SslStream sslStream)
                {
                    // This will now work without crashing
                    var group = TlsInspector.GetNegotiatedGroup(sslStream);

                    Console.WriteLine($"[TLS] Cipher: {sslStream.NegotiatedCipherSuite} | Group: {group}");
                }

                // Return true to accept the connection
                return true;
            };
        };
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();