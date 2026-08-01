using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactX.Tests.Integration;

public class ObservabilityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ObservabilityTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidCorrelationId_IsPreservedAndEchoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "client-trace-valid-1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("client-trace-valid-1", response.Headers.GetValues("X-Correlation-Id").First());
    }

    [Fact]
    public async Task OverlongCorrelationId_IsReplacedOrLimited()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", new string('a', 200));
        var response = await _client.SendAsync(request);

        var returned = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.True(returned.Length <= 100);
        Assert.DoesNotContain('\r', returned);
        Assert.DoesNotContain('\n', returned);
    }

    [Fact]
    public async Task MissingCorrelationId_IsGenerated()
    {
        var response = await _client.GetAsync("/health");

        var returned = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.False(string.IsNullOrWhiteSpace(returned));
        Assert.True(returned.Length <= 100);
    }

    [Fact]
    public async Task ProblemDetails_ContainsCorrelationId()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/does-not-exist-xyz");
        request.Headers.Add("X-Correlation-Id", "problem-trace-789");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("problem-trace-789", doc.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("problem-trace-789", response.Headers.GetValues("X-Correlation-Id").First());
    }

    [Fact]
    public async Task RequestLogs_ContainCorrelationId()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "log-trace-456");

        await client.GetAsync("/health");
        await Task.Delay(100);

        Assert.Contains(factory.LogCapture.LogEntries, e => e.Contains("log-trace-456"));
    }

    [Fact]
    public async Task RequestLogs_DoNotContainSecretsOrQueryStrings()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer SUPERSECRET-JWT-TOKEN");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "no-leak-trace");

        await client.GetAsync("/health?password=hunter2&access_token=TOPSECRETTOKEN&refresh_token=REFRESHSECRET");
        await Task.Delay(100);

        var logs = factory.LogCapture.LogEntries;
        Assert.DoesNotContain(logs, e => e.Contains("hunter2"));
        Assert.DoesNotContain(logs, e => e.Contains("TOPSECRETTOKEN"));
        Assert.DoesNotContain(logs, e => e.Contains("REFRESHSECRET"));
        Assert.DoesNotContain(logs, e => e.Contains("SUPERSECRET-JWT-TOKEN"));
        Assert.DoesNotContain(logs, e => e.Contains("password=hunter2"));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UnexpectedException_500_IncludesCorrelationIdWithoutInternals()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseCosmosDb", "false");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
                builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
                builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
                builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1000");
                builder.ConfigureServices(services =>
                {
                    services.AddScoped<ITokenService>(_ => new ThrowingTokenService());
                });
            });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "error-trace-321");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Observability Tester",
            correo = $"obs_{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("error-trace-321", root.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("Unexpected token generation failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UnexpectedException_500_RequestLog_RecordsStatusAndCorrelationId_WithoutSecrets()
    {
        using var factory = new ThrowingTokenServiceFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "log-error-500");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Log 500 Tester",
            correo = $"log500_{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await Task.Delay(100);

        var entry = factory.LogCapture.LogEntries.SingleOrDefault(e => e.Contains("HTTP POST /api/v1/auth/register"));
        Assert.NotNull(entry);
        Assert.Contains("completed with status 500", entry);
        Assert.Contains("log-error-500", entry);
        Assert.DoesNotContain("Unexpected token generation failure", entry, StringComparison.Ordinal);
        Assert.DoesNotContain("Password123!", entry, StringComparison.Ordinal);
        Assert.DoesNotContain("token", entry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", entry, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingTokenServiceFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ITokenService>(_ => new ThrowingTokenService());
            });
        }
    }

    private sealed class ThrowingTokenService : ITokenService
    {
        public string GenerateAccessToken(Usuario usuario)
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string GenerateRefreshToken()
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string GeneratePasswordResetToken()
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string? GetPrincipalIdFromExpiredToken(string token) => null;
    }
}
