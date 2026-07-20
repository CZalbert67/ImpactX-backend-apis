using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"user_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "User Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WithAuth_ReturnsProfile()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
    }

    [Fact]
    public async Task UpdateProfile_WithAuth_UpdatesName()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/users/me", new
        {
            nombre = "Updated Name",
            telefono = "555-1234",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal("Updated Name", profile!.Nombre);
        Assert.Equal("555-1234", profile.Telefono);
    }

    [Fact]
    public async Task GetPreferences_WithAuth_ReturnsPreferences()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/me/preferences");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prefs = await response.Content.ReadFromJsonAsync<UserPreferencesDto>();
        Assert.NotNull(prefs);
    }

    [Fact]
    public async Task UpdatePreferences_WithAuth_Updates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/users/me/preferences", new
        {
            notificacionesPush = true,
            idioma = "en",
            unidadVelocidad = "mph",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prefs = await response.Content.ReadFromJsonAsync<UserPreferencesDto>();
        Assert.True(prefs!.NotificacionesPush);
        Assert.Equal("en", prefs.Idioma);
    }

    [Fact]
    public async Task GetDriverProfile_WithAuth_ReturnsProfile()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/driver-profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var driver = await response.Content.ReadFromJsonAsync<DriverProfileDto>();
        Assert.NotNull(driver);
    }

    [Fact]
    public async Task UpdateDriverProfile_WithAuth_Updates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/users/driver-profile", new
        {
            tipoVehiculo = "SUV",
            marca = "Toyota",
            modelo = "RAV4",
            anio = 2022,
            color = "Negro",
            placa = "XYZ-789",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var driver = await response.Content.ReadFromJsonAsync<DriverProfileDto>();
        Assert.Equal("SUV", driver!.TipoVehiculo);
        Assert.Equal("Toyota", driver.Marca);
    }

    [Fact]
    public async Task GetMedicalProfile_WithAuth_ReturnsProfile()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/driver-profile/medical");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var medical = await response.Content.ReadFromJsonAsync<MedicalProfileDto>();
        Assert.NotNull(medical);
    }

    [Fact]
    public async Task UpdateMedicalProfile_WithAuth_Updates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/users/driver-profile/medical", new
        {
            tipoSangre = "O+",
            alergias = "Ninguna",
            condiciones = "Ninguna",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var medical = await response.Content.ReadFromJsonAsync<MedicalProfileDto>();
        Assert.Equal("O+", medical!.TipoSangre);
    }

    [Fact]
    public async Task SearchUsers_WithAuth_ReturnsResults()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/search?q=test");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();
        Assert.NotNull(results);
    }

    [Fact]
    public async Task SearchUsers_WithShortQuery_ReturnsEmpty()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/search?q=a");
        var results = await response.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();
        Assert.Empty(results!);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/users/me", new { nombre = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
