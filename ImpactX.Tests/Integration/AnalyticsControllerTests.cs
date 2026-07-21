using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class AnalyticsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnalyticsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"analytics_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Analytics Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token ?? string.Empty;
    }

    [Fact]
    public async Task GetDashboard_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/analytics/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_WithAuth_ReturnsDashboard()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/analytics/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.NotNull(result);
        Assert.True(result!.TotalIncidentes >= 0);
    }

    [Fact]
    public async Task GetIncidentTrend_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/analytics/incidents/trend");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetIncidentTrend_WithAuth_ReturnsTrend()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/analytics/incidents/trend?agrupacion=month&meses=6");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<IncidentTrendPointDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetTripSummary_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/analytics/trips/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTripSummary_WithAuth_ReturnsSummary()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/analytics/trips/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TripSummaryDto>();
        Assert.NotNull(result);
        Assert.True(result!.TotalViajes >= 0);
    }

    [Fact]
    public async Task AllAnalyticsEndpoints_Work()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var dashboardResponse = await _client.GetAsync("/api/analytics/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var trendResponse = await _client.GetAsync("/api/analytics/incidents/trend");
        Assert.Equal(HttpStatusCode.OK, trendResponse.StatusCode);

        var tripResponse = await _client.GetAsync("/api/analytics/trips/summary");
        Assert.Equal(HttpStatusCode.OK, tripResponse.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_ReturnsCompleteMetrics()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/analytics/dashboard");
        var result = await response.Content.ReadFromJsonAsync<DashboardDto>();

        Assert.NotNull(result);
        Assert.IsType<int>(result!.TotalIncidentes);
        Assert.IsType<int>(result.ContactosActivos);
        Assert.IsType<int>(result.ViajesRegistrados);
        Assert.IsType<int>(result.DiasDePlan);
    }
}
