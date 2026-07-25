using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImpactX.Tests.Integration;

public class PlatformFoundationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PlatformFoundationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsExpectedStructure()
    {
        var response = await _client.GetAsync("/health");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("impactx-api", root.GetProperty("service").GetString());
        Assert.NotNull(root.GetProperty("environment").GetString());
        Assert.NotNull(root.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task GetHealthLive_Returns200()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthLive_ReturnsExpectedStructure()
    {
        var response = await _client.GetAsync("/health/live");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("impactx-api", root.GetProperty("service").GetString());
        Assert.NotNull(root.GetProperty("environment").GetString());
        Assert.NotNull(root.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task GetHealthReady_Returns200()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthReady_ReturnsExpectedStructure()
    {
        var response = await _client.GetAsync("/health/ready");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("impactx-api", root.GetProperty("service").GetString());
        Assert.NotNull(root.GetProperty("environment").GetString());
        Assert.NotNull(root.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task HealthEndpoints_TimestampIsValidIso8601()
    {
        var response = await _client.GetAsync("/health");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var timestamp = doc.RootElement.GetProperty("timestamp").GetString();

        Assert.NotNull(timestamp);
        Assert.True(DateTimeOffset.TryParse(timestamp, out _));
    }

    [Fact]
    public async Task GetOpenApiV1Json_Returns200()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSwaggerIndex_Returns200()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class SwaggerDisabledInProductionTests : IClassFixture<SwaggerDisabledInProductionTests.ProductionFactory>
{
    private readonly HttpClient _client;

    public SwaggerDisabledInProductionTests(ProductionFactory factory)
    {
        _client = factory.CreateClient();
    }

    public class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("UseCosmosDb", "false");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("Jwt:Secret", "test-secret-key-that-is-at-least-32-characters-long-for-hmac");
        }
    }

    [Fact]
    public async Task OpenApiV1Json_Returns200_InProduction()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_Returns404_InProduction()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
