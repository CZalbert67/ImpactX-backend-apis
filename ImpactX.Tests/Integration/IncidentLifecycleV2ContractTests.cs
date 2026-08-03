using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class IncidentLifecycleV2ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IncidentLifecycleV2ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MobileSos_CreatesSingleActiveIncident_ThatCanBeClosed()
    {
        using var client = await CreateAuthenticatedClientAsync("mobile");
        var sosResponse = await client.PostAsJsonAsync("/api/v1/alerts/sos", new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Lugar = "Prueba de incidente",
            Severidad = "severe",
            Canal = "manual",
            Modo = "immediate"
        });
        sosResponse.EnsureSuccessStatusCode();
        var alert = await sosResponse.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.NotNull(alert);

        var active = await client.GetFromJsonAsync<List<IncidenteListItemDto>>("/api/v1/incidents/active");
        var incident = Assert.Single(active!);
        Assert.Equal(alert!.Id, incident.AlertaId);
        Assert.Equal("Enviada", incident.Estado);

        var closeResponse = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incident.Id}/close",
            new IncidentCloseRequest { MetodoCierre = "Atendido", Nota = "Atendido por la red ImpactX" });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<IncidentActionResponse>();
        Assert.NotNull(closed);
        Assert.Equal("Cerrada", closed!.Estado);

        var detail = await client.GetFromJsonAsync<IncidenteDetailDto>($"/api/v1/incidents/{incident.Id}");
        Assert.Equal("Cerrada", detail!.Estado);
        Assert.Equal("Atendido", detail.MetodoCierre);
        Assert.Equal("Atendido por la red ImpactX", detail.Nota);
        Assert.Empty(await client.GetFromJsonAsync<List<IncidenteListItemDto>>("/api/v1/incidents/active") ?? []);
    }

    [Fact]
    public async Task MobileConfirmOk_ClosesIncidentAsFalseAlarm()
    {
        using var client = await CreateAuthenticatedClientAsync("mobile");
        var sosResponse = await client.PostAsJsonAsync("/api/v1/alerts/sos", new SosRequest
        {
            Lat = 20.0,
            Lng = -99.0,
            Severidad = "moderate",
            Canal = "manual",
            Modo = "manual"
        });
        sosResponse.EnsureSuccessStatusCode();

        var incident = Assert.Single(
            await client.GetFromJsonAsync<List<IncidenteListItemDto>>("/api/v1/incidents/active") ?? []);
        var response = await client.PostAsync($"/api/v1/incidents/{incident.Id}/confirm-ok", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IncidentActionResponse>();
        Assert.Equal("FalsaAlarma", result!.Estado);

        var detail = await client.GetFromJsonAsync<IncidenteDetailDto>($"/api/v1/incidents/{incident.Id}");
        Assert.True(detail!.EsFalsaAlarma);
        Assert.Equal("ConfirmacionOk", detail.MetodoCierre);
        Assert.NotNull(detail.ConfirmadaEn);
        Assert.NotNull(detail.CerradaEn);
    }

    [Fact]
    public async Task WebMayReadAndCloseButCannotConfirmOk()
    {
        var account = await CreateAccountTokensAsync();
        using var mobile = AuthClient(account.MobileToken);
        var sosResponse = await mobile.PostAsJsonAsync("/api/v1/alerts/sos", new SosRequest
        {
            Lat = 19.4,
            Lng = -99.2,
            Severidad = "severe",
            Canal = "manual",
            Modo = "immediate"
        });
        sosResponse.EnsureSuccessStatusCode();
        var incident = Assert.Single(
            await mobile.GetFromJsonAsync<List<IncidenteListItemDto>>("/api/v1/incidents/active") ?? []);

        using var web = AuthClient(account.WebToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await web.PostAsync($"/api/v1/incidents/{incident.Id}/confirm-ok", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await web.GetAsync($"/api/v1/incidents/{incident.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await web.PostAsJsonAsync($"/api/v1/incidents/{incident.Id}/close",
                new IncidentCloseRequest { MetodoCierre = "Prueba" })).StatusCode);
    }

    [Fact]
    public async Task WearableCannotUseIncidentManagementSurface()
    {
        using var client = await CreateAuthenticatedClientAsync("wearable");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/incidents/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/v1/incidents/{Guid.NewGuid()}")).StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string clientType)
    {
        var account = await RegisterAsync(clientType);
        return AuthClient(account.Token);
    }

    private async Task<(string MobileToken, string WebToken)> CreateAccountTokensAsync()
    {
        using var anonymous = _factory.CreateClient();
        var email = $"incident_clients_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";
        var registration = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Incident Multi Client Tester",
            correo = email,
            password,
            client = "mobile"
        });
        registration.EnsureSuccessStatusCode();
        var mobile = await registration.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password,
            client = "web"
        });
        login.EnsureSuccessStatusCode();
        var web = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (mobile!.Token!, web!.Token!);
    }

    private async Task<(string Token, string Email)> RegisterAsync(string clientType)
    {
        using var anonymous = _factory.CreateClient();
        var email = $"incident_v2_{Guid.NewGuid():N}@test.com";
        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Incident Lifecycle Tester",
            correo = email,
            password = "Password123!",
            client = clientType
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token!, email);
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
