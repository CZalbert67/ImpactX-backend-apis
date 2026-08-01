using System.Net;
using System.Text.Json;
using ImpactX.Infrastructure.Data;
using ImpactX.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactX.Tests.Integration;

public class HealthReadinessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthReadinessTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_Returns200_WithoutCosmosDependency()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_Returns200_WithHealthyConfiguration()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_ReturnsExpectedProperties()
    {
        var response = await _client.GetAsync("/health");
        var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("impactx-api", root.GetProperty("service").GetString());
        Assert.NotNull(root.GetProperty("environment").GetString());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));

        var checks = root.GetProperty("checks");
        Assert.True(checks.GetArrayLength() >= 2);
        var names = new List<string>();
        foreach (var check in checks.EnumerateArray())
        {
            names.Add(check.GetProperty("name").GetString()!);
            Assert.NotNull(check.GetProperty("status").GetString());
            Assert.True(check.TryGetProperty("duration", out _));
        }

        Assert.Contains("live", names);
        Assert.Contains("config", names);
    }

    [Fact]
    public async Task Live_IncludesCheckDetails()
    {
        var response = await _client.GetAsync("/health/live");
        var json = await ReadJsonAsync(response);

        var checks = json.RootElement.GetProperty("checks");
        Assert.True(checks.GetArrayLength() >= 1);
        Assert.Equal("live", checks[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Ready_IncludesConfigCheck()
    {
        var response = await _client.GetAsync("/health/ready");
        var json = await ReadJsonAsync(response);

        var names = json.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("config", names);
    }

    [Fact]
    public async Task HealthEndpoints_ReturnJsonContentType()
    {
        foreach (var endpoint in new[] { "/health", "/health/live", "/health/ready" })
        {
            var response = await _client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType ?? "");
        }
    }

    [Fact]
    public async Task HealthJson_DoesNotExposeSecretsOrInternals()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("YOUR_AZURE_COSMOS_KEY", body, StringComparison.Ordinal);
        Assert.DoesNotContain("connectionString", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_IncludesCorrelationIdFromRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "client-trace-abc-123");
        var response = await _client.SendAsync(request);
        var json = await ReadJsonAsync(response);

        Assert.Equal("client-trace-abc-123", response.Headers.GetValues("X-Correlation-Id").First());
        Assert.Equal("client-trace-abc-123", json.RootElement.GetProperty("correlationId").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }
}

public class ReadyUnhealthyTests
{
    private static WebApplicationFactory<Program> CreateFactoryWithFailingDatabase()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseCosmosDb", "true");
                builder.UseSetting("UseInMemoryDatabase", "false");
                builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
                builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
                builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
                builder.UseSetting("DatabaseInitialization:Enabled", "false");
                builder.UseSetting("AzureCosmosDb:Endpoint", "https://localhost:443/");
                builder.UseSetting("AzureCosmosDb:Key", "dGVzdC1rZXk=");
                builder.UseSetting("AzureCosmosDb:DatabaseName", "ImpactX-Test");
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<CosmosDbContext>(new TestCosmosDbContext
                    {
                        AccessCheck = _ => Task.FromResult(false)
                    });
                });
            });

    [Fact]
    public async Task Ready_Returns503_WhenCriticalDependencyFails()
    {
        using var factory = CreateFactoryWithFailingDatabase();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("unhealthy", root.GetProperty("status").GetString());

        var databaseCheck = root.GetProperty("checks")
            .EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("unhealthy", databaseCheck.GetProperty("status").GetString());
        Assert.NotNull(databaseCheck.GetProperty("description").GetString());

        Assert.DoesNotContain("dGVzdC1rZXk=", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("YOUR_AZURE_COSMOS_KEY", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_StillReturns200_WhenCriticalDependencyFails()
    {
        using var factory = CreateFactoryWithFailingDatabase();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_AggregatesUnhealthy_WhenCriticalDependencyFails()
    {
        using var factory = CreateFactoryWithFailingDatabase();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.Equal("unhealthy", doc.RootElement.GetProperty("status").GetString());
    }
}
