using ConnectingApps.PqcTracer;
using Microsoft.Extensions.DependencyInjection;

// Register the HttpClient via DI
var services = new ServiceCollection();
services.AddHttpClient("GoogleClient").AddTlsTracing();

var serviceProvider = services.BuildServiceProvider();

// Resolve IHttpClientFactory from the service provider
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

// Create the client using the factory
using var client = httpClientFactory.CreateClient("GoogleClient");

try
{
    using var response = await client.GetAsync("https://www.google.com");
    response.EnsureSuccessStatusCode();

    var trace = response.GetTlsTrace();
    if (trace != null)
    {
        Console.WriteLine($"Negotiated Group: {trace.Group}");
        Console.WriteLine($"Cipher Suite: {trace.CipherSuite}");
    }
    else
    {
        Console.WriteLine("TLS Trace not found.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
