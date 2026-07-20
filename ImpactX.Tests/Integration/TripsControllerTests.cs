using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class TripsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TripsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"trip_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Trip Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task StartTrip_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/trips/start", new { dispositivoId = "DEV-001" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartTrip_WithAuth_CreatesActiveTrip()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId = "WEAR-001",
            proposito = "Trabajo",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var trip = await response.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.NotNull(trip);
        Assert.Equal("Activo", trip!.Estado);
    }

    [Fact]
    public async Task GetActiveTrip_WithoutActiveTrip_ReturnsMessage()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/trips/active");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StartPauseResumeFinishTrip_FullFlow()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var startResponse = await _client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId = "WEAR-002",
            proposito = "Personal",
        });
        var trip = await startResponse.Content.ReadFromJsonAsync<ViajeDto>();

        var pauseResponse = await _client.PostAsync($"/api/trips/{trip!.Id}/pause", null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);
        var paused = await pauseResponse.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.Equal("Pausado", paused!.Estado);

        var resumeResponse = await _client.PostAsync($"/api/trips/{trip.Id}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        var resumed = await resumeResponse.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.Equal("Activo", resumed!.Estado);

        await _client.PostAsync($"/api/trips/{trip.Id}/finish", null);
    }

    [Fact]
    public async Task PauseTrip_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync($"/api/trips/{Guid.NewGuid()}/pause", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FinishTrip_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync($"/api/trips/{Guid.NewGuid()}/finish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTelemetry_WithAuth_SavesPoints()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var startResponse = await _client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId = "WEAR-003",
        });
        var trip = await startResponse.Content.ReadFromJsonAsync<ViajeDto>();

        var telemetryResponse = await _client.PatchAsJsonAsync($"/api/trips/{trip!.Id}/telemetry", new
        {
            puntos = new[]
            {
                new { lat = 19.43, lng = -99.13, velocidad = 50, timestamp = DateTime.UtcNow },
                new { lat = 19.44, lng = -99.14, velocidad = 60, timestamp = DateTime.UtcNow },
            }
        });

        Assert.Equal(HttpStatusCode.OK, telemetryResponse.StatusCode);
    }
}
