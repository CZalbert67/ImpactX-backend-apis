using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class IncidentesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IncidentesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string token, Guid alertaId)> SetupClosedAlertAsync()
    {
        var email = $"inc_{Guid.NewGuid()}@test.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Incident Tester",
            correo = email,
            password = "Password123!"
        });
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var token = auth!.Token ?? string.Empty;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sosResponse = await _client.PostAsJsonAsync("/api/alerts/sos", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "crash",
            canal = "manual",
            modo = "auto",
        });
        var sos = await sosResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        await _client.PostAsJsonAsync($"/api/alerts/{sos!.Id}/close", new
        {
            metodoCierre = "Atendido",
            nota = "Incidente de prueba.",
        });

        return (token, sos.Id);
    }

    [Fact]
    public async Task GetIncidents_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetIncidents_WithAuth_ReturnsList()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
    }

    [Fact]
    public async Task GetIncidentDetail_WithAuth_ReturnsDetail()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var listResponse = await _client.GetAsync("/api/incidents");
        var list = await listResponse.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        var incidentId = list!.First().Id;

        var response = await _client.GetAsync($"/api/incidents/{incidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IncidenteDetailDto>();
        Assert.NotNull(result);
        Assert.Equal("crash", result!.Severidad);
        Assert.Equal("Atendido", result.MetodoCierre);
    }

    [Fact]
    public async Task GetIncidentDetail_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/incidents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MarkFalseAlarm_WithAuth_ReturnsOk()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var listResponse = await _client.GetAsync("/api/incidents");
        var list = await listResponse.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        var incidentId = list!.First().Id;

        var response = await _client.PatchAsJsonAsync($"/api/incidents/{incidentId}/mark-false-alarm", new
        {
            nota = "Falso positivo por sensor.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNote_WithAuth_Updates()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var listResponse = await _client.GetAsync("/api/incidents");
        var list = await listResponse.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        var incidentId = list!.First().Id;

        var response = await _client.PatchAsJsonAsync($"/api/incidents/{incidentId}/note", new
        {
            nota = "Nota actualizada por el usuario.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detailResponse = await _client.GetAsync($"/api/incidents/{incidentId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<IncidenteDetailDto>();
        Assert.Equal("Nota actualizada por el usuario.", detail!.Nota);
    }

    [Fact]
    public async Task GetMapData_WithFreePlan_ReturnsForbidden()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var listResponse = await _client.GetAsync("/api/incidents");
        var list = await listResponse.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        var incidentId = list!.First().Id;

        var response = await _client.GetAsync($"/api/incidents/{incidentId}/map");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/incidents/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_WithFreePlan_ReturnsForbidden()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/incidents/export?formato=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListIncident_WithSeverityFilter_Works()
    {
        var (token, _) = await SetupClosedAlertAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/incidents?severidad=crash");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<IncidenteListItemDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
    }
}
