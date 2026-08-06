using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Core.Wearables;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class TripControlExclusivityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TripControlExclusivityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Token, Guid UserId)> RegisterAsync(string clientType)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Trip Capability Tester",
            correo = $"tripcap_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = clientType
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth!.Token!);
        var userIdClaim = jwt.Claims.First(claim =>
            claim.Type is "nameid" or ClaimTypes.NameIdentifier);
        return (auth.Token!, Guid.Parse(userIdClaim.Value));
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task AddLinkedWearableAsync(Guid userId, string deviceId)
    {
        await _factory.ExecuteInDbContextAsync(async db =>
        {
            db.Wearables.Add(new Wearable
            {
                UsuarioId = userId,
                DispositivoId = deviceId,
                Nombre = "Galaxy Watch8 de prueba",
                Modelo = WearableProductPolicy.TargetModel,
                Fabricante = WearableProductPolicy.TargetManufacturer,
                Plataforma = WearableProductPolicy.TargetPlatform,
                Estado = "Vinculado",
                Connected = true,
            });
            await db.SaveChangesAsync();
        });
    }

    private static object TelemetryBatch(
        Guid eventId,
        string? wearableDeviceId = null,
        bool capturedOffline = false,
        DateTime? timestamp = null) => new
    {
        schemaVersion = 2,
        batchId = Guid.NewGuid(),
        batchSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        capturedOffline,
        wearableDeviceId,
        wearableModel = WearableProductPolicy.TargetModel,
        batteryLevel = 80,
        eventos = new[]
        {
            new
            {
                eventId,
                timestamp = timestamp ?? DateTime.UtcNow,
                sequenceNumber = 1L,
                lat = 19.4326,
                lng = -99.1332,
                velocidad = 25d,
                gpsAccuracyMeters = 8d,
                aceleracionX = 0.1d,
                aceleracionY = 0.2d,
                aceleracionZ = 9.8d,
                magnitudAceleracion = 9.81d,
                giroscopioX = 0.01d,
                giroscopioY = 0.02d,
                giroscopioZ = 0.01d,
                frecuenciaCardiaca = 82,
                calidadSensor = "high"
            }
        }
    };

    [Fact]
    [Trait("Category", "Security")]
    public async Task WebClient_CanReadButCannotControlTrip()
    {
        var account = await RegisterAsync("web");
        using var client = AuthClient(account.Token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/trips")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/trips/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/trips/start", new { dispositivoId = "NOT-WEARABLE" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/trips/{Guid.NewGuid()}/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/trips/{Guid.NewGuid()}/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/trips/{Guid.NewGuid()}/finish", null)).StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task MobileWithoutLinkedWearable_CannotControlTrip()
    {
        var account = await RegisterAsync("mobile");
        using var client = AuthClient(account.Token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/trips")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/trips/start", new { dispositivoId = "UNLINKED" })).StatusCode);
    }

    [Fact]
    public async Task MobileRelay_WithOwnLinkedGalaxyWatch8_CanControlTripAndIngestBatchTelemetry()
    {
        const string deviceId = "GW8-MOBILE-RELAY-001";
        var account = await RegisterAsync("mobile");
        await AddLinkedWearableAsync(account.UserId, deviceId);
        using var client = AuthClient(account.Token);

        var start = await client.PostAsJsonAsync(
            "/api/v1/trips/start",
            new { dispositivoId = deviceId, proposito = "Relay desde Galaxy Watch8" });

        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var trip = await start.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.NotNull(trip);
        Assert.Equal("wearable", trip!.ControlClient);
        Assert.True(trip.MobileFallbackUsed);
        Assert.False(string.IsNullOrWhiteSpace(trip.FallbackReason));

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/resume", null)).StatusCode);
        var telemetry = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            TelemetryBatch(Guid.NewGuid(), deviceId));
        Assert.Equal(HttpStatusCode.OK, telemetry.StatusCode);
        var telemetryResult = await telemetry.Content.ReadFromJsonAsync<TelemetryIngestionResultDto>();
        Assert.NotNull(telemetryResult);
        Assert.Equal(1, telemetryResult!.Insertados);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                $"/api/v1/trips/{trip.Id}/telemetry",
                TelemetryBatch(Guid.NewGuid(), "GW8-NOT-OWNED"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/finish", null)).StatusCode);

        var offlineAfterFinish = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            TelemetryBatch(
                Guid.NewGuid(),
                deviceId,
                capturedOffline: true,
                timestamp: trip.Inicio.AddSeconds(1)));
        Assert.Equal(HttpStatusCode.OK, offlineAfterFinish.StatusCode);

        var onlineAfterFinish = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            TelemetryBatch(Guid.NewGuid(), deviceId, capturedOffline: false));
        Assert.Equal(HttpStatusCode.Conflict, onlineAfterFinish.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task MobileRelay_WithDifferentDeviceId_IsRejected()
    {
        var account = await RegisterAsync("mobile");
        await AddLinkedWearableAsync(account.UserId, "GW8-OWNED");
        using var client = AuthClient(account.Token);

        var response = await client.PostAsJsonAsync(
            "/api/v1/trips/start",
            new { dispositivoId = "GW8-NOT-OWNED" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task TokenWithoutClientClaim_CanReadButCannotControlTrip()
    {
        var account = await RegisterAsync("web");
        using var client = AuthClient(TestJwtBuilder.Create(account.UserId));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/trips")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/trips/start", new { dispositivoId = "NO-CLAIM" })).StatusCode);
    }

    [Fact]
    public async Task WearableClient_CanControlTripAndIngestTelemetryIdempotently()
    {
        var account = await RegisterAsync("wearable");
        using var client = AuthClient(account.Token);

        var start = await client.PostAsJsonAsync(
            "/api/v1/trips/start",
            new { dispositivoId = "GALAXY-WATCH-8" });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var trip = await start.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.NotNull(trip);
        Assert.Equal("wearable", trip!.ControlClient);
        Assert.False(trip.MobileFallbackUsed);
        Assert.Null(trip.FallbackReason);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/resume", null)).StatusCode);

        var eventId = Guid.NewGuid();
        var batch = TelemetryBatch(eventId, "GALAXY-WATCH-8");
        var first = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            batch);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<TelemetryIngestionResultDto>();
        Assert.Equal(1, firstResult!.Insertados);

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            batch);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateResult = await duplicate.Content.ReadFromJsonAsync<TelemetryIngestionResultDto>();
        Assert.Equal(0, duplicateResult!.Insertados);
        Assert.Equal(1, duplicateResult.Duplicados);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/trips/{trip.Id}/finish", null)).StatusCode);
    }

    [Fact]
    public async Task OpenApi_RestrictedTripWrites_Declare403()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var operations = new[]
        {
            (Path: "/api/v1/trips/start", Method: "post"),
            (Path: "/api/v1/trips/{id}/pause", Method: "post"),
            (Path: "/api/v1/trips/{id}/resume", Method: "post"),
            (Path: "/api/v1/trips/{id}/finish", Method: "post"),
            (Path: "/api/v1/trips/{id}/telemetry", Method: "post"),
            (Path: "/api/v1/trips/{id}/telemetry", Method: "patch")
        };

        foreach (var operation in operations)
        {
            var responses = paths
                .GetProperty(operation.Path)
                .GetProperty(operation.Method)
                .GetProperty("responses");
            Assert.True(responses.TryGetProperty("403", out _),
                $"{operation.Method.ToUpperInvariant()} {operation.Path} no documenta 403.");
        }
    }
}
