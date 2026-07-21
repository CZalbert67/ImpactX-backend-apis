using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Test User",
            correo = $"test_{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.RefreshToken);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var email = $"dup_{Guid.NewGuid()}@test.com";

        var first = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "User1",
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "User2",
            correo = email,
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var result = await second.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("El correo ya está registrado.", result.Mensaje);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        var email = $"login_{Guid.NewGuid()}@test.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Login User",
            correo = email,
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            correo = email,
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.RefreshToken);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            correo = "nonexistent@test.com",
            password = "wrong"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RecoverPassword_WithAnyEmail_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/recover-password", new
        {
            correo = $"recover_{Guid.NewGuid()}@test.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token = "invalid-token",
            newPassword = "NewPass123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "old",
            newPassword = "new"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = "some-token"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/auth/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/auth/account");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportAccount_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/account/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FullAuthFlow_RegisterLoginLogout_Works()
    {
        var email = $"flow_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Flow User",
            correo = email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerResult);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            correo = email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginResult);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var sessionsResponse = await _client.GetAsync("/api/auth/sessions");
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);

        var changePasswordResponse = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = password,
            newPassword = "NewPassword456!"
        });
        Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);

        var exportResponse = await _client.GetAsync("/api/auth/account/export");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        var export = await exportResponse.Content.ReadFromJsonAsync<ExportAccountDto>();
        Assert.NotNull(export);
        Assert.Equal("Flow User", export!.Nombre);
        Assert.Equal(email, export.Correo);
    }
}
