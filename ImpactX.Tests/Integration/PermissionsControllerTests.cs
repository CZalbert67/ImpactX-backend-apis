using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class PermissionsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PermissionsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"perm_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Perm Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetPermissions_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPermissions_WithAuth_ReturnsPermissions()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var permissions = await response.Content.ReadFromJsonAsync<PermisosDto>();
        Assert.NotNull(permissions);
    }

    [Fact]
    public async Task UpdateMobilePermissions_WithAuth_ReturnsUpdated()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/permissions/mobile", new
        {
            ubicacion = true,
            notificaciones = true,
            bluetooth = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PermisosPlataformaDto>();
        Assert.NotNull(result);
        Assert.True(result!.Ubicacion);
    }

    [Fact]
    public async Task UpdateWebPermissions_WithAuth_ReturnsUpdated()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/permissions/web", new
        {
            notificaciones = true,
            microfono = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PermisosPlataformaDto>();
        Assert.NotNull(result);
        Assert.True(result!.Notificaciones);
    }

    [Fact]
    public async Task UpdateMobilePermissions_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/permissions/mobile", new { ubicacion = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
