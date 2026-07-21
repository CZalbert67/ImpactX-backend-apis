using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class MonitorsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MonitorsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string email = null!)
    {
        var emailActual = email ?? $"monitor_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Monitor Tester",
            correo = emailActual,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetMonitors_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/monitors");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonitors_WithAuth_ReturnsEmptyList()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/monitors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var monitors = await response.Content.ReadFromJsonAsync<List<MonitorDto>>();
        Assert.Empty(monitors!);
    }

    [Fact]
    public async Task RevokeMonitor_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/monitors/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
