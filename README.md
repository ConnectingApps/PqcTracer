# PqcTracer

**Trace if your web requests are quantum-proof.**

![PqcTracer](ConnectingApps.PqcTracer/icon.png)

## Why Post-Quantum Cryptography Matters

Quantum computers pose an existential threat to current encryption. Algorithms like RSA and elliptic-curve cryptography (used in today's TLS connections) can be broken by sufficiently powerful quantum computers using Shor's algorithm. This means data encrypted today could be harvested and decrypted in the future—a threat known as **"harvest now, decrypt later"**.

To protect against this, the industry is transitioning to **Post-Quantum Cryptography (PQC)**. [ML-KEM (Module-Lattice-Based Key Encapsulation Mechanism)](https://en.wikipedia.org/wiki/Kyber), formerly known as Kyber, is one of the algorithms standardized by NIST for quantum-resistant key exchange. When you see `MLKEM` in your TLS negotiation (e.g., `X25519MLKEM768`), your connection is using a **hybrid key exchange** that combines classical and post-quantum algorithms—making it resistant to both current and future quantum attacks.

**You need to know which of your connections are already quantum-safe.** PqcTracer makes this visible.

## Installation

Install the NuGet package from [nuget.org/packages/ConnectingApps.PqcTracer](https://www.nuget.org/packages/ConnectingApps.PqcTracer):

```bash
dotnet add package ConnectingApps.PqcTracer
```

## Quick Start

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

## What You Get

After integrating PqcTracer, every HTTPS response includes two headers:

```
X-Tls-Cipher: TLS_AES_256_GCM_SHA384
X-Tls-Group: X25519MLKEM768
```

- **`X-Tls-Group`**: The key exchange algorithm used. If it contains `MLKEM`, your connection is **quantum-resistant**.
- **`X-Tls-Cipher`**: The symmetric cipher suite negotiated for the session.

## Platform Support

> ⚠️ **Linux Only**: PqcTracer currently only works on Linux. It uses P/Invoke calls to `libssl.so.3` (OpenSSL 3.x) to query the negotiated TLS group directly from the SSL context.

For PQC support (ML-KEM), you need **OpenSSL 3.5.0 or later** installed on your system.

## How It Works

1. **`TraceTlsConnection()`** hooks into Kestrel's TLS authentication callback to intercept the `SslStream` after handshake completion
2. It queries OpenSSL via `SSL_ctrl` to get the negotiated group ID, then converts it to a human-readable name using `SSL_group_to_name`
3. The captured values are stored in the connection context
4. **`UseTlsTraceHeaders()`** middleware reads these values and adds them to every HTTP response

## Use Cases

- **PQC Compliance Auditing**: Verify which endpoints/clients are using quantum-resistant key exchange
- **Migration Tracking**: Monitor your infrastructure's transition to post-quantum cryptography
- **Security Monitoring**: Detect connections still using classical-only key exchange
- **Debugging**: Diagnose TLS negotiation issues without packet capture tools

## License

This project is licensed under the GPL-3.0 License. See the [LICENSE](LICENSE) file for details.

---

*Built by [Connecting Apps](https://github.com/ConnectingApps) and [QuantumSafeAudit.com](https://quantumsafeaudit.com)*