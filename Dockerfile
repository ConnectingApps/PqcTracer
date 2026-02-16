FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ConnectingApps.PqcTracer/ConnectingApps.PqcTracer.csproj ConnectingApps.PqcTracer/
COPY ConnectingApps.PqcTracer.WebApi/ConnectingApps.PqcTracer.WebApi.csproj ConnectingApps.PqcTracer.WebApi/
RUN dotnet restore ConnectingApps.PqcTracer.WebApi/ConnectingApps.PqcTracer.WebApi.csproj

# Copy source code
COPY ConnectingApps.PqcTracer/ ConnectingApps.PqcTracer/
COPY ConnectingApps.PqcTracer.WebApi/ ConnectingApps.PqcTracer.WebApi/
RUN dotnet publish ConnectingApps.PqcTracer.WebApi/ConnectingApps.PqcTracer.WebApi.csproj -c Release -o /app/publish

# Runtime stage - build OpenSSL 3.5+ from source
FROM ubuntu:24.04 AS openssl-builder

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        build-essential \
        wget \
        ca-certificates \
        perl \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Build OpenSSL 3.5.0 from source (skip documentation for faster build)
RUN wget https://github.com/openssl/openssl/releases/download/openssl-3.5.0/openssl-3.5.0.tar.gz && \
    tar -xzf openssl-3.5.0.tar.gz && \
    cd openssl-3.5.0 && \
    ./Configure --prefix=/opt/openssl --openssldir=/opt/openssl/ssl shared zlib no-docs && \
    make -j$(nproc) && \
    make install_sw install_ssldirs && \
    cd .. && \
    rm -rf openssl-3.5.0 openssl-3.5.0.tar.gz

# Runtime stage
FROM ubuntu:24.04 AS runtime

# Install system dependencies first
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        wget \
        libicu-dev \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

# Copy and set up custom OpenSSL 3.5+ BEFORE installing .NET
COPY --from=openssl-builder /opt/openssl /opt/openssl

# Replace system OpenSSL with the custom build so ALL consumers (including .NET) use 3.5.0
RUN ln -sf /opt/openssl/lib64/libssl.so.3 /usr/lib/x86_64-linux-gnu/libssl.so.3 && \
    ln -sf /opt/openssl/lib64/libcrypto.so.3 /usr/lib/x86_64-linux-gnu/libcrypto.so.3 && \
    ldconfig && \
    update-ca-certificates

# Set up OpenSSL paths
ENV PATH="/opt/openssl/bin:${PATH}"
ENV LD_LIBRARY_PATH="/opt/openssl/lib64:/opt/openssl/lib:${LD_LIBRARY_PATH}"
ENV PKG_CONFIG_PATH="/opt/openssl/lib64/pkgconfig:/opt/openssl/lib/pkgconfig:${PKG_CONFIG_PATH}"

# Verify OpenSSL version is 3.5+
RUN openssl version

# Now install .NET 10 runtime - it will use the custom OpenSSL we just set up
RUN wget --no-check-certificate https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet && \
    ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet && \
    rm dotnet-install.sh

WORKDIR /app
COPY --from=build /app/publish .

# Generate a self-signed certificate for HTTPS
RUN openssl req -x509 -newkey rsa:2048 -keyout /app/cert.key -out /app/cert.crt \
    -days 365 -nodes -subj "/CN=localhost" && \
    openssl pkcs12 -export -out /app/cert.pfx -inkey /app/cert.key -in /app/cert.crt -passout pass:password

ENV ASPNETCORE_URLS="https://+:7156"
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/cert.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=password
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 7156

# Entrypoint script: start the WebAPI in the background, wait for it, then curl
COPY <<'EOF' /app/entrypoint.sh
#!/bin/bash
set -e

# Start the web API in the background
dotnet ConnectingApps.PqcTracer.WebApi.dll &
APP_PID=$!

# Wait for the server to be ready
echo "Waiting for WebAPI to start..."
for i in $(seq 1 30); do
    if curl -sk -o /dev/null -w '' https://localhost:7156/weatherforecast 2>/dev/null; then
        break
    fi
    sleep 1
done

echo ""
echo "========================================"
echo "  Calling GET /weatherforecast"
echo "========================================"
echo ""

# Execute GET request showing all response headers
curl -sk -D - -o /dev/null https://localhost:7156/weatherforecast

echo ""
echo "========================================"
echo "  Full response with body"
echo "========================================"
echo ""

curl -sk -i https://localhost:7156/weatherforecast

echo ""
echo ""
echo "OpenSSL version: $(openssl version)"
echo ""

# Keep the container running with the web API
wait $APP_PID
EOF

RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
