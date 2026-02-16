using System.Net.Security;
using ConnectingApps.PqcTracer;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.WebHost.TraceTlsConnection();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Use(async (context, next) =>
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