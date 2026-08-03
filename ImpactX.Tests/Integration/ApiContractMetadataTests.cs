using System.Net;
using System.Text.Json;

namespace ImpactX.Tests.Integration;

public class ApiContractMetadataTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiContractMetadataTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Contract_IsPublicFrozenAndContainsCanonicalRoutes()
    {
        var response = await _client.GetAsync("/api/v1/meta/contract");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("v1", root.GetProperty("apiVersion").GetString());
        Assert.Equal("2026.08.04", root.GetProperty("contractVersion").GetString());
        Assert.Equal("frozen", root.GetProperty("status").GetString());

        var routes = root.GetProperty("routes")
            .EnumerateArray()
            .Select(route => $"{route.GetProperty("method").GetString()} {route.GetProperty("path").GetString()}")
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("POST /api/v1/auth/login", routes);
        Assert.Contains("POST /api/v1/mobile/sync/push", routes);
        Assert.Contains("GET /api/v1/account/export", routes);
        Assert.Contains("GET /api/v1/family-subscriptions/invitations/incoming", routes);
        Assert.Contains(
            "PATCH /api/v1/quick-messages/conversations/{otherPublicProfileId}/read",
            routes);
        Assert.Contains("POST /api/v1/trips/start", routes);
    }

    [Theory]
    [InlineData("web", "vehicles:manage")]
    [InlineData("mobile", "mobile-sync:offline")]
    [InlineData("wearable", "telemetry:write")]
    public async Task ClientCapabilityContract_ReturnsExpectedCapability(string client, string capability)
    {
        var response = await _client.GetAsync($"/api/v1/meta/clients/{client}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var capabilities = json.RootElement.GetProperty("capabilities")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Contains(capability, capabilities);
        Assert.Equal("2026.08.04", json.RootElement.GetProperty("contractVersion").GetString());
    }

    [Fact]
    public async Task UnknownClientCapabilityContract_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/meta/clients/desktop");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Contract_SupportsConditionalGetWithEtag()
    {
        var first = await _client.GetAsync("/api/v1/meta/contract");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.TryGetValues("ETag", out var values));
        var etag = values.Single();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/meta/contract");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task EveryResponse_ExposesFrozenContractHeaders()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("v1", response.Headers.GetValues("X-ImpactX-Api-Version").Single());
        Assert.Equal("2026.08.04", response.Headers.GetValues("X-ImpactX-Contract-Version").Single());
    }

    [Fact]
    public async Task LegacyRoute_ExposesSunsetAndSuccessorMetadata()
    {
        var response = await _client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Deprecation").Single());
        Assert.Equal("true", response.Headers.GetValues("X-ImpactX-Legacy-Route").Single());
        Assert.Equal("Tue, 02 Feb 2027 00:00:00 GMT", response.Headers.GetValues("Sunset").Single());
        Assert.Contains("successor-version", response.Headers.GetValues("Link").Single());
    }

    [Fact]
    public async Task OpenApi_DeclaresFrozenVersionAndClientCapabilities()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Contrato congelado 2026.08.04", content, StringComparison.Ordinal);
        Assert.Contains("Clientes permitidos:", content, StringComparison.Ordinal);
    }
}
