using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImpactX.Tests.Integration;

public class RateLimitingContractTests : IDisposable
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    private (WebApplicationFactory<Program> Factory, HttpClient Client) CreateRateLimitedClient()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseCosmosDb", "false");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
                builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
                builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
                builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1");
                builder.UseSetting("RateLimiting:Auth:LoginPerMinute", "1000");
            });
        return (factory, factory.CreateClient());
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RateLimited_Returns429()
    {
        var (factory, client) = CreateRateLimitedClient();
        _factory = factory;
        _client = client;

        var email = $"ratelimit_{Guid.NewGuid()}@test.com";

        var response1 = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Rate Limit Tester",
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var email2 = $"ratelimit2_{Guid.NewGuid()}@test.com";
        var response2 = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Rate Limit Tester 2",
            correo = email2,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, response2.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RateLimited_ReturnsProblemDetails()
    {
        var (factory, client) = CreateRateLimitedClient();
        _factory = factory;
        _client = client;

        var email = $"rlpd_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL PD Tester",
            correo = email,
            password = "Password123!"
        });

        var email2 = $"rlpd2_{Guid.NewGuid()}@test.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL PD Tester 2",
            correo = email2,
            password = "Password123!"
        });

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("type", out var type));
        Assert.Contains("rate-limited", type.GetString(), StringComparison.OrdinalIgnoreCase);

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(429, status.GetInt32());

        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RateLimited_ReturnsRetryAfter()
    {
        var (factory, client) = CreateRateLimitedClient();
        _factory = factory;
        _client = client;

        var email = $"rlra_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL RA Tester",
            correo = email,
            password = "Password123!"
        });

        var email2 = $"rlra2_{Guid.NewGuid()}@test.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL RA Tester 2",
            correo = email2,
            password = "Password123!"
        });

        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task HealthEndpoint_ExemptFromRateLimiting()
    {
        var (factory, client) = CreateRateLimitedClient();
        _factory = factory;
        _client = client;

        var email = $"rlexempt_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL Exempt Tester",
            correo = email,
            password = "Password123!"
        });

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task PartitionKey_NotExposed()
    {
        var (factory, client) = CreateRateLimitedClient();
        _factory = factory;
        _client = client;

        var email = $"rlpk_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL PK Tester",
            correo = email,
            password = "Password123!"
        });

        var email2 = $"rlpk2_{Guid.NewGuid()}@test.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "RL PK Tester 2",
            correo = email2,
            password = "Password123!"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("partitionKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("partition", body, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
