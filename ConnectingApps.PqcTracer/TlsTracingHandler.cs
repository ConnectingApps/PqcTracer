using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using ConnectingApps.PqcTracer.TlsInspection;

namespace ConnectingApps.PqcTracer;

public sealed class TlsTracingHandler : DelegatingHandler
{
    private readonly Action<TlsTrace>? _callback;
    private readonly RemoteCertificateValidationCallback? _certificateValidator;

    public TlsTracingHandler(Action<TlsTrace>? callback = null, RemoteCertificateValidationCallback? certificateValidator = null)
        : this(new SocketsHttpHandler(), callback, certificateValidator)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TlsTracingHandler"/> class.
    /// The handler takes ownership of the <paramref name="innerHandler"/> and will dispose it when this handler is disposed.
    /// </summary>
    public TlsTracingHandler(SocketsHttpHandler innerHandler, Action<TlsTrace>? callback = null, RemoteCertificateValidationCallback? certificateValidator = null)
        : base(innerHandler)
    {
        _callback = callback;
        _certificateValidator = certificateValidator;
        innerHandler.ConnectCallback = ConnectWithTlsAsync;
    }

    private async ValueTask<Stream> ConnectWithTlsAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        Stream? stream = null;
        SslStream? sslStream = null;
        bool success = false;
        try
        {
            await tcpClient.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, cancellationToken)
                .ConfigureAwait(false);

            stream = tcpClient.GetStream();
            var request = context.InitialRequestMessage;

            if (request.RequestUri?.Scheme != Uri.UriSchemeHttps)
            {
                success = true;
                return stream;
            }

            sslStream = new SslStream(stream, leaveInnerStreamOpen: false, ValidateCertificate);
            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = context.DnsEndPoint.Host,
                RemoteCertificateValidationCallback = ValidateCertificate
            };

            await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
            CaptureTrace(request, sslStream);
            success = true;
            return sslStream;
        }
        finally
        {
            if (!success)
            {
                if (sslStream != null)
                {
                    await sslStream.DisposeAsync().ConfigureAwait(false);
                }
                if (stream != null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                tcpClient.Dispose();
            }
        }
    }

    private bool ValidateCertificate(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (_certificateValidator != null)
        {
            return _certificateValidator(sender ?? this, certificate, chain, errors);
        }

        return errors == SslPolicyErrors.None;
    }

    private void CaptureTrace(HttpRequestMessage request, SslStream sslStream)
    {
        var group = GeneralTlsInspector.GetNegotiatedGroup(sslStream);
        var cipher = sslStream.NegotiatedCipherSuite.ToString();
        var trace = new TlsTrace(group, cipher);

        request.Options.Set(TlsTracer.TlsTraceKey, trace);
        _callback?.Invoke(trace);
    }
}
