using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class SubscriptionControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SubscriptionControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"subs_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Sub Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetCurrentSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/subscription");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentSubscription_WithAuth_ReturnsFreeStatus()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/subscription");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SuscripcionDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetSubscriptionHistory_WithAuth_ReturnsList()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/subscription/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<List<SuscripcionDto>>();
        Assert.NotNull(history);
    }

    [Fact]
    public async Task ChangePlan_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/subscription/change-plan",
            new { planNombre = "Premium" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/subscription/cancel",
            new { motivo = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPayments_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/subscription/payments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
