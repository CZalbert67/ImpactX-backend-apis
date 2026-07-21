using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class AlertasControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AlertasControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"alert_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Alert Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task Detect_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "bump",
            gForce = 2.5,
            decibeles = 85.0,
            frecuenciaCardiaca = 95.0,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Detect_WithAuth_ReturnsCreatedAlert()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "crash",
            gForce = 5.0,
            decibeles = 110.0,
            frecuenciaCardiaca = 120.0,
            viajeId = "trip-001",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.NotNull(result);
        Assert.Equal("Impacto", result!.Tipo);
        Assert.Equal("crash", result.Severidad);
        Assert.Equal("Pendiente", result.Estado);
        Assert.Equal("trip-001", result.ViajeId);
    }

    [Fact]
    public async Task SendSos_WithAuth_ReturnsSosAlert()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/alerts/sos", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "severe",
            canal = "manual",
            modo = "manual",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.NotNull(result);
        Assert.Equal("SOS", result!.Tipo);
        Assert.Equal("Enviada", result.Estado);
    }

    [Fact]
    public async Task SendSos_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/alerts/sos", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "severe",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithAuth_ReturnsAlert()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var detectResponse = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "bump",
            gForce = 2.5,
            decibeles = 85.0,
            frecuenciaCardiaca = 95.0,
        });
        var created = await detectResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        var response = await _client.GetAsync($"/api/alerts/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.NotNull(result);
        Assert.Equal("bump", result!.Severidad);
    }

    [Fact]
    public async Task GetStatus_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/alerts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmOk_WithPendingAlert_ReturnsOk()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var detectResponse = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "bump",
            gForce = 2.5,
            decibeles = 85.0,
            frecuenciaCardiaca = 95.0,
        });
        var created = await detectResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        var response = await _client.PostAsync($"/api/alerts/{created!.Id}/confirm-ok", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConfirmOkResponse>();
        Assert.NotNull(result);
        Assert.True(result!.EsFalsaAlarma);
    }

    [Fact]
    public async Task BypassCritical_WithPendingAlert_Activates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var detectResponse = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "crash",
            gForce = 5.0,
            decibeles = 110.0,
            frecuenciaCardiaca = 120.0,
        });
        var created = await detectResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        var response = await _client.PostAsync($"/api/alerts/{created!.Id}/bypass-critical", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlertActionResponse>();
        Assert.NotNull(result);
        Assert.Equal("Activa", result!.Estado);
    }

    [Fact]
    public async Task CloseAlert_WithAtendido_ClosesAndCreatesIncident()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sosResponse = await _client.PostAsJsonAsync("/api/alerts/sos", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "severe",
            canal = "manual",
            modo = "immediate",
        });
        var created = await sosResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        var closeResponse = await _client.PostAsJsonAsync($"/api/alerts/{created!.Id}/close", new
        {
            metodoCierre = "Atendido",
            nota = "Usuario atendido.",
        });

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var result = await closeResponse.Content.ReadFromJsonAsync<AlertActionResponse>();
        Assert.NotNull(result);
        Assert.Contains("incidente registrado", result!.Mensaje, StringComparison.OrdinalIgnoreCase);

        var getResponse = await _client.GetAsync($"/api/alerts/{created.Id}");
        var closed = await getResponse.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.Equal("Cerrada", closed!.Estado);
    }

    [Fact]
    public async Task Retry_WithPendingAlert_Retries()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sosResponse = await _client.PostAsJsonAsync("/api/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "crash",
            gForce = 5.0,
            decibeles = 110.0,
            frecuenciaCardiaca = 120.0,
        });
        var created = await sosResponse.Content.ReadFromJsonAsync<AlertStatusDto>();

        var response = await _client.PostAsync($"/api/alerts/{created!.Id}/retry", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AlertActionResponse>();
        Assert.NotNull(result);
        Assert.Equal("Enviada", result!.Estado);
    }

    [Fact]
    public async Task SyncOffline_WithAlerts_Syncs()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/alerts/sync-offline", new
        {
            alertas = new object[]
            {
                new
                {
                    lat = 19.43,
                    lng = -99.13,
                    severidad = "crash",
                    tipo = "SOS",
                    gForce = "5.5",
                    creadoEn = DateTime.UtcNow.AddHours(-1),
                },
                new
                {
                    lat = 19.44,
                    lng = -99.14,
                    severidad = "bump",
                    tipo = "Impacto",
                    creadoEn = DateTime.UtcNow.AddMinutes(-30),
                },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AlertStatusDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.All(result, r => Assert.Equal("Enviada", r.Estado));
        Assert.All(result, r => Assert.True(r.EsOffline));
    }

    [Fact]
    public async Task FullAlertLifecycle_Works()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sosResponse = await _client.PostAsJsonAsync("/api/alerts/sos", new
        {
            lat = 19.43,
            lng = -99.13,
            severidad = "severe",
            canal = "manual",
            modo = "immediate",
        });
        var sos = await sosResponse.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.Equal("Enviada", sos!.Estado);
        Assert.True(sos.EsBypassCritico);

        var statusResponse = await _client.GetAsync($"/api/alerts/{sos.Id}");
        var status = await statusResponse.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.NotNull(status);

        var closeResponse = await _client.PostAsJsonAsync($"/api/alerts/{sos.Id}/close", new
        {
            metodoCierre = "Atendido",
        });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var getClosedResponse = await _client.GetAsync($"/api/alerts/{sos.Id}");
        var closed = await getClosedResponse.Content.ReadFromJsonAsync<AlertStatusDto>();
        Assert.Equal("Cerrada", closed!.Estado);
    }

    [Fact]
    public async Task GetStatus_NotFound_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/alerts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
