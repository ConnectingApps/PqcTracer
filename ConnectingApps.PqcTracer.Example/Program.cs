using ConnectingApps.PqcTracer;

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
