using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class RoutesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RoutesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"route_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Route Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetFrequentRoutes_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/routes/frequent");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFrequentRoutes_WithAuth_ReturnsEmpty()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/routes/frequent");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routes = await response.Content.ReadFromJsonAsync<List<RutaDto>>();
        Assert.Empty(routes!);
    }

    [Fact]
    public async Task CreateFrequentRoute_WithAuth_ReturnsCreated()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/routes/frequent", new
        {
            nombre = "Casa-Trabajo",
            origen = "Casa",
            origenLat = 19.43,
            origenLng = -99.13,
            destino = "Oficina",
            destinoLat = 19.45,
            destinoLng = -99.15,
            distanciaKm = 5.0,
            duracionEstimadaMin = 20,
            esFrecuente = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var route = await response.Content.ReadFromJsonAsync<RutaDto>();
        Assert.NotNull(route);
        Assert.Equal("Casa-Trabajo", route!.Nombre);
    }

    [Fact]
    public async Task GetHistory_WithAuth_ReturnsList()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/routes/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var routes = await response.Content.ReadFromJsonAsync<List<RutaDto>>();
        Assert.NotNull(routes);
    }

    [Fact]
    public async Task SelectToday_WithAuth_ReturnsUpdated()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/routes/frequent", new
        {
            nombre = "Ruta Hoy",
            origen = "A",
            origenLat = 19.43,
            origenLng = -99.13,
            destino = "B",
            destinoLat = 19.44,
            destinoLng = -99.14,
            distanciaKm = 2.0,
            duracionEstimadaMin = 10,
        });
        var created = await createResponse.Content.ReadFromJsonAsync<RutaDto>();

        var patchResponse = await _client.PatchAsJsonAsync("/api/routes/select-today",
            new { rutaId = created!.Id });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updated = await patchResponse.Content.ReadFromJsonAsync<RutaDto>();
        Assert.True(updated!.SeleccionadaHoy);
    }
}
