using System.Net;
using System.Net.Http.Json;
using ImpactX.Infrastructure.Data;
using ImpactX.Models.DTOs;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monitor = ImpactX.Core.Domain.Monitor;

namespace ImpactX.Tests.Integration;

public class MonitorsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MonitorsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid UserId)> RegisterAndGetTokenAsync(string email = null!)
    {
        var emailActual = email ?? $"monitor_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Monitor Tester",
            correo = emailActual,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result!.Token!);
        var userId = Guid.Parse(jwt.Claims.First(c => c.Type == "nameid" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        return (result.Token!, userId);
    }

    private async Task<Guid> SeedMonitorAsync(Monitor monitor)
    {
        await _factory.ExecuteInDbContextAsync(async db =>
        {
            db.Set<Monitor>().Add(monitor);
            await db.SaveChangesAsync();
        });
        return monitor.Id;
    }

    private async Task<Monitor?> GetMonitorByIdAsync(Guid monitorId)
    {
        return await _factory.ExecuteInDbContextAsync(async db =>
        {
            return await db.Set<Monitor>().FindAsync(monitorId);
        });
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetMonitors_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/monitors");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonitors_WithAuth_ReturnsEmptyList()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/monitors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var monitors = await response.Content.ReadFromJsonAsync<List<MonitorDto>>();
        Assert.Empty(monitors!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RevokeMonitor_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/monitors/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AcceptInvitation_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = "ANY" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RejectInvitation_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/monitors/invite/reject", new { token = "ANY" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitation_WithoutAuth_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/monitors/invite/details", new { token = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AcceptInvitation_EmptyToken_ReturnsBadRequest()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RejectInvitation_EmptyToken_ReturnsBadRequest()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/reject", new { token = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetInvitation_EmptyToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/monitors/invite/details", new { token = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AcceptInvitation_WithRealPendingInvitation_ActivatesMonitor()
    {
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoToken, invitadoId) = await RegisterAndGetTokenAsync($"invited_{Guid.NewGuid()}@test.com");
        var tokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = tokenInvitacion,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invitadoToken);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = tokenInvitacion });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var persisted = await GetMonitorByIdAsync(monitorId);
        Assert.NotNull(persisted);
        Assert.Equal("Activo", persisted!.Estado);
        Assert.Equal(invitadoId.ToString(), persisted.ProfileId);
        Assert.Equal(propietarioId, persisted.UsuarioId);
        Assert.NotNull(persisted.ConfirmadoEn);
        Assert.Null(persisted.TokenInvitacion);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AcceptInvitation_ByDifferentUser_ReturnsForbidden()
    {
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoLegitimoToken, invitadoLegitimoId) = await RegisterAndGetTokenAsync($"legitimate_{Guid.NewGuid()}@test.com");
        var (atacanteToken, atacanteId) = await RegisterAndGetTokenAsync($"attacker_{Guid.NewGuid()}@test.com");
        var tokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoLegitimoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = tokenInvitacion,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", atacanteToken);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = tokenInvitacion });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var persisted = await GetMonitorByIdAsync(monitorId);
        Assert.NotNull(persisted);
        Assert.Equal("Pendiente", persisted!.Estado);
        Assert.Equal(invitadoLegitimoId.ToString(), persisted.ProfileId);
        Assert.NotNull(persisted.TokenInvitacion);
        Assert.Null(persisted.ConfirmadoEn);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AcceptInvitation_SecondAttempt_DoesNotDuplicateMonitor()
    {
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoToken, invitadoId) = await RegisterAndGetTokenAsync($"invited_{Guid.NewGuid()}@test.com");
        var tokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = tokenInvitacion,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invitadoToken);

        var firstResponse = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = tokenInvitacion });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = tokenInvitacion });
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);

        var persisted = await GetMonitorByIdAsync(monitorId);
        Assert.NotNull(persisted);
        Assert.Equal("Activo", persisted!.Estado);
        Assert.Equal(invitadoId.ToString(), persisted.ProfileId);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RejectInvitation_WithRealPendingInvitation_RejectsMonitor()
    {
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoToken, invitadoId) = await RegisterAndGetTokenAsync($"invited_{Guid.NewGuid()}@test.com");
        var tokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = tokenInvitacion,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invitadoToken);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/reject", new { token = tokenInvitacion });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var persisted = await GetMonitorByIdAsync(monitorId);
        Assert.NotNull(persisted);
        Assert.Equal("Rechazado", persisted!.Estado);
        Assert.Null(persisted.TokenInvitacion);
        Assert.Equal(propietarioId, persisted.UsuarioId);
        Assert.Null(persisted.ConfirmadoEn);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RejectInvitation_ByDifferentUser_ReturnsForbidden()
    {
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoLegitimoToken, invitadoLegitimoId) = await RegisterAndGetTokenAsync($"legitimate_{Guid.NewGuid()}@test.com");
        var (atacanteToken, atacanteId) = await RegisterAndGetTokenAsync($"attacker_{Guid.NewGuid()}@test.com");
        var tokenInvitacion = Guid.NewGuid().ToString("N")[..12].ToUpper();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoLegitimoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = tokenInvitacion,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", atacanteToken);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/reject", new { token = tokenInvitacion });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var persisted = await GetMonitorByIdAsync(monitorId);
        Assert.NotNull(persisted);
        Assert.Equal("Pendiente", persisted!.Estado);
        Assert.Equal(invitadoLegitimoId.ToString(), persisted.ProfileId);
        Assert.NotNull(persisted.TokenInvitacion);
        Assert.Null(persisted.ConfirmadoEn);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InvitationToken_DoesNotAppearInRequestLogs()
    {
        var fakeToken = "TEST_ONLY_INVITATION_TOKEN_URL_DO_NOT_LOG";
        var (propietarioToken, propietarioId) = await RegisterAndGetTokenAsync($"owner_{Guid.NewGuid()}@test.com");
        var (invitadoToken, invitadoId) = await RegisterAndGetTokenAsync($"invited_{Guid.NewGuid()}@test.com");
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = propietarioId,
            ProfileId = invitadoId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = fakeToken,
            Expiracion = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow,
        };
        await SeedMonitorAsync(monitor);

        _factory.LogCapture.LogEntries.Clear();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invitadoToken);

        var response = await _client.PostAsJsonAsync("/api/monitors/invite/accept", new { token = fakeToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var allLogs = string.Join("\n", _factory.LogCapture.LogEntries);
        Assert.DoesNotContain(fakeToken, allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("TEST_ONLY", allLogs, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task MonitorsController_DoesNotUseTokenInRoute()
    {
        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            var routePattern = (endpoint as RouteEndpoint)?.RoutePattern;
            if (routePattern == null) continue;

            var rawText = routePattern.RawText ?? string.Empty;

            if (rawText.Contains("monitors/invite", StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain("{token}", rawText, StringComparison.OrdinalIgnoreCase);
            }
        }

        var allInviteRoutes = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("monitors/invite") == true)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.Contains(allInviteRoutes, r => r!.Contains("monitors/invite/details"));
        Assert.Contains(allInviteRoutes, r => r!.Contains("monitors/invite/accept"));
        Assert.Contains(allInviteRoutes, r => r!.Contains("monitors/invite/reject"));

        var anyTokenRoute = allInviteRoutes.Any(r => r!.Contains("{token}", StringComparison.OrdinalIgnoreCase));
        Assert.False(anyTokenRoute, "Ninguna ruta de invitación debe contener {token}");
    }
}
