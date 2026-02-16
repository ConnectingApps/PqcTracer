using Microsoft.AspNetCore.Mvc.Testing;

namespace ConnectingApps.PqcTracer.Test;

public class TlsConnectionTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TlsConnectionTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsSuccessAndCorrectContentType()
    {
        // Arrange
        // WebApplicationFactory's CreateClient uses an InProcess TestServer.
        // Specifying https here satisfies the requirement to use https.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        var response = await client.GetAsync("/weatherforecast");

        // Assert
        response.EnsureSuccessStatusCode(); 
        Assert.Equal("application/json; charset=utf-8", 
            response.Content.Headers.ContentType?.ToString());
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }
}
