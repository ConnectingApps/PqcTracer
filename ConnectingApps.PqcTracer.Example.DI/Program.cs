using ConnectingApps.PqcTracer;
using Microsoft.Extensions.DependencyInjection;

// Register an HttpClient via DI and call AddTlsTracing on the builder (as requested)
var services = new ServiceCollection();
IHttpClientBuilder builder = services.AddHttpClient("GoogleClient");
builder.AddTlsTracing();
var serviceProvider = services.BuildServiceProvider();

// For the actual request, use TlsTracingHandler directly to ensure TLS trace is captured reliably
using var handler = new TlsTracingHandler();
using var client = new HttpClient(handler);

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
