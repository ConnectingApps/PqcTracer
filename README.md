# PqcTracer

**Trace if your web requests are quantum-proof.**

## Table of Contents

- [Why Post-Quantum Cryptography Matters](#why-post-quantum-cryptography-matters)
- [Installation](#installation)
- [Incoming Traffic (ASP.NET Core / Kestrel)](#incoming-traffic-aspnet-core--kestrel)
  - [Quick Start](#quick-start)
  - [Complete Example](#complete-example)
  - [What You Get](#what-you-get)
- [Outgoing Traffic (HttpClient)](#outgoing-traffic-httpclient)
  - [Approach 1: Direct Handler Usage](#approach-1-direct-handler-usage)
  - [Approach 2: Dependency Injection with IHttpClientFactory](#approach-2-dependency-injection-with-ihttpclientfactory)
- [Platform Support](#platform-support)
- [How It Works](#how-it-works)
- [Use Cases](#use-cases)
- [License](#license)

## Why Post-Quantum Cryptography Matters

Quantum computers pose an existential threat to current encryption. Algorithms like RSA and elliptic-curve cryptography (used in today's TLS connections) can be broken by sufficiently powerful quantum computers using Shor's algorithm. This means data encrypted today could be harvested and decrypted in the future—a threat known as **"harvest now, decrypt later"**.

To protect against this, the industry is transitioning to **Post-Quantum Cryptography (PQC)**. [ML-KEM (Module-Lattice-Based Key Encapsulation Mechanism)](https://en.wikipedia.org/wiki/Kyber), formerly known as Kyber, is one of the algorithms standardized by NIST for quantum-resistant key exchange. When you see `MLKEM` in your TLS negotiation (e.g., `X25519MLKEM768`), your connection is using a **hybrid key exchange** that combines classical and post-quantum algorithms—making it resistant to both current and future quantum attacks.

**You need to know which of your connections are already quantum-safe.** PqcTracer makes this visible for both incoming and outgoing HTTPS traffic.

## Installation

Install the NuGet package from [nuget.org/packages/ConnectingApps.PqcTracer](https://www.nuget.org/packages/ConnectingApps.PqcTracer):

```bash
dotnet add package ConnectingApps.PqcTracer
```

## Incoming Traffic (ASP.NET Core / Kestrel)

### Quick Start

PqcTracer provides **two extension methods** you need to call:

| Method | Purpose |
|--------|---------|
| `TraceTlsConnection()` | Configures Kestrel to capture TLS negotiation details (key exchange group & cipher suite) |
| `UseTlsTraceHeaders()` | Adds middleware that exposes the captured info as response headers |

### Complete Example

Here's a complete `Program.cs` showing how to integrate PqcTracer:

```csharp
using ConnectingApps.PqcTracer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.WebHost.TraceTlsConnection();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseTlsTraceHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
```

### What You Get

After integrating PqcTracer, every HTTPS response includes two headers:

```
X-Tls-Cipher: TLS_AES_256_GCM_SHA384
X-Tls-Group: X25519MLKEM768
```

- **`X-Tls-Group`**: The key exchange algorithm used. If it contains `MLKEM`, your connection is **quantum-resistant**.
- **`X-Tls-Cipher`**: The symmetric cipher suite negotiated for the session.

## Outgoing Traffic (HttpClient)

PqcTracer can also trace TLS details for **outgoing** HTTPS requests made via `HttpClient`. There are two ways to use this functionality:

### Approach 1: Direct Handler Usage

Use [`TlsTracingHandler`](ConnectingApps.PqcTracer/TlsTracingHandler.cs) directly when creating your `HttpClient`:

```csharp
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
```

### Approach 2: Dependency Injection with IHttpClientFactory

Register `HttpClient` via dependency injection and use the [`AddTlsTracing()`](ConnectingApps.PqcTracer/TlsTracer.cs) extension method:

```csharp
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
```

Both approaches produce the same output. The DI approach is recommended for production applications as it integrates better with ASP.NET Core's service container and enables features like named/typed clients, policies, and lifetime management.

**Example Output** (on a system with OpenSSL 3.0, which lacks PQC support):

```
Negotiated Group: x25519
Cipher Suite: TLS_AES_256_GCM_SHA384
```

> **Note**: The output shows `x25519` instead of `X25519MLKEM768` because OpenSSL 3.0 does not support ML-KEM. You need **OpenSSL 3.5.0 or later** for post-quantum key exchange.

## Platform Support

> ⚠️ **Linux Only**: PqcTracer currently only works on Linux. It uses P/Invoke calls to `libssl.so.3` (OpenSSL 3.x) to query the negotiated TLS group directly from the SSL context.

For PQC support (ML-KEM), you need **OpenSSL 3.5.0 or later** installed on your system.

## How It Works

### Incoming Traffic (Kestrel)
1. **`TraceTlsConnection()`** hooks into Kestrel's TLS authentication callback to intercept the `SslStream` after handshake completion
2. It queries OpenSSL via `SSL_ctrl` to get the negotiated group ID, then converts it to a human-readable name using `SSL_group_to_name`
3. The captured values are stored in the connection context
4. **`UseTlsTraceHeaders()`** middleware reads these values and adds them to every HTTP response

### Outgoing Traffic (HttpClient)
1. **`TlsTracingHandler`** implements a custom `ConnectCallback` for `SocketsHttpHandler`
2. After the TLS handshake completes, it queries the `SslStream` using the same OpenSSL P/Invoke calls
3. The TLS trace is stored in the `HttpRequestMessage.Options` dictionary
4. **`GetTlsTrace()`** extension method retrieves the trace from the response

## Use Cases

- **PQC Compliance Auditing**: Verify which endpoints/clients are using quantum-resistant key exchange
- **Migration Tracking**: Monitor your infrastructure's transition to post-quantum cryptography
- **Security Monitoring**: Detect connections still using classical-only key exchange
- **Debugging**: Diagnose TLS negotiation issues without packet capture tools
- **API Client Monitoring**: Track PQC usage in outgoing API calls to third-party services

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](LICENSE) file for details.

---

*Built by [Connecting Apps](https://github.com/ConnectingApps) and [QuantumSafeAudit.com](https://quantumsafeaudit.com)*