using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class SettingsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SettingsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"user_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Settings Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetSettings_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_WithAuth_ReturnsSettings()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.NotNull(settings);
        Assert.False(settings.TwoFactorEnabled);
    }

    [Fact]
    public async Task UpdateSettings_WithAuth_Updates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync("/api/settings", new
        {
            idioma = "en",
            unidadVelocidad = "mph",
            notificacionesPush = false,
            notificacionesEmail = false,
            compartirUbicacion = false,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.Equal("en", settings!.Idioma);
        Assert.Equal("mph", settings.UnidadVelocidad);
        Assert.False(settings.NotificacionesPush);
        Assert.False(settings.NotificacionesEmail);
        Assert.False(settings.CompartirUbicacion);
    }

    [Fact]
    public async Task Setup2Fa_WithAuth_ReturnsSecretAndUri()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync("/api/settings/2fa/setup", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setup = await response.Content.ReadFromJsonAsync<Setup2FaResponse>();
        Assert.NotNull(setup);
        Assert.NotEmpty(setup.Secret);
        Assert.NotEmpty(setup.QrCodeUri);
        Assert.NotEmpty(setup.ManualKey);
        Assert.Contains("otpauth://totp/", setup.QrCodeUri);
    }

    [Fact]
    public async Task Enable2Fa_WithoutSetup_ReturnsConflict()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/settings/2fa/enable", new
        {
            code = "123456"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Full2FaFlow_SetupEnableDisable_Succeeds()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Setup
        var setupResponse = await _client.PostAsync("/api/settings/2fa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setup = await setupResponse.Content.ReadFromJsonAsync<Setup2FaResponse>();

        // Compute valid TOTP code
        var code = ComputeTotpCode(setup!.Secret);

        // Enable
        var enableResponse = await _client.PostAsJsonAsync("/api/settings/2fa/enable", new
        {
            code
        });
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

        // Verify settings show 2FA enabled
        var getResponse = await _client.GetAsync("/api/settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.True(settings!.TwoFactorEnabled);

        // Disable
        var disableRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/settings/2fa")
        {
            Content = JsonContent.Create(new { code })
        };
        var disableResponse = await _client.SendAsync(disableRequest);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        // Verify settings show 2FA disabled
        getResponse = await _client.GetAsync("/api/settings");
        settings = await getResponse.Content.ReadFromJsonAsync<SettingsResponseDto>();
        Assert.False(settings!.TwoFactorEnabled);
    }

    [Fact]
    public async Task Enable2Fa_WithInvalidCode_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsync("/api/settings/2fa/setup", null);

        var response = await _client.PostAsJsonAsync("/api/settings/2fa/enable", new
        {
            code = "000000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSettings_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new { idioma = "en" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ComputeTotpCode(string secret)
    {
        var secretBytes = Services.SettingsService.FromBase32(secret);
        var timeCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        var timeBytes = BitConverter.GetBytes(timeCounter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new System.Security.Cryptography.HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) |
                     ((hash[offset + 1] & 0xFF) << 16) |
                     ((hash[offset + 2] & 0xFF) << 8) |
                     (hash[offset + 3] & 0xFF);

        var otp = binary % 1_000_000;
        return otp.ToString("D6");
    }
}
