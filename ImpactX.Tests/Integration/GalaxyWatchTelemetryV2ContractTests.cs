using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class GalaxyWatchTelemetryV2ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GalaxyWatchTelemetryV2ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Mobile, string Wearable, string Web)> CreateAccountTokensAsync()
    {
        using var client = _factory.CreateClient();
        var email = $"gw8_v2_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Galaxy Watch Contract Tester",
            correo = email,
            password,
            client = "mobile"
        });
        register.EnsureSuccessStatusCode();
        var mobile = await register.Content.ReadFromJsonAsync<AuthResponse>();

        async Task<string> Login(string clientType)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                identifier = email,
                password,
                client = clientType
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token!;
        }

        return (mobile!.Token!, await Login("wearable"), await Login("web"));
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object PairPayload(string deviceId, string model = "Galaxy Watch 8") => new
    {
        dispositivoId = deviceId,
        nombre = "Galaxy Watch 8 de integración",
        modelo = model,
        fabricante = "Samsung",
        plataforma = "WearOS",
        appVersion = "2.0.0-test",
        versionSistemaOperativo = "WearOS-test",
        versionFirmware = "FW-test",
        capacidadesSensores = new[]
        {
            "accelerometer", "gyroscope", "gps", "heart_rate", "hrv", "spo2"
        }
    };

    private static object EnrichedBatch(
        Guid eventId,
        Guid batchId,
        DateTime timestampUtc,
        string wearableDeviceId) => new
    {
        schemaVersion = TelemetrySchema.EnrichedVersion,
        batchId,
        batchSequence = 1L,
        capturedOffline = true,
        wearableDeviceId,
        wearableModel = "Galaxy Watch 8",
        wearableAppVersion = "2.0.0-test",
        wearableOsVersion = "WearOS-test",
        wearableFirmwareVersion = "FW-test",
        batteryLevel = 78,
        clockOffsetMilliseconds = 25L,
        eventos = new[]
        {
            new
            {
                eventId,
                timestamp = timestampUtc,
                sequenceNumber = 1L,
                lat = 19.4326,
                lng = -99.1332,
                velocidad = 42.5,
                altitud = 2240.0,
                heading = 90.0,
                gpsAccuracyMeters = 4.0,
                aceleracionX = 3.0,
                aceleracionY = 4.0,
                aceleracionZ = 0.0,
                magnitudAceleracion = 999.0,
                giroscopioX = 0.1,
                giroscopioY = 0.2,
                giroscopioZ = 0.3,
                desaceleracion = 2.2,
                frecuenciaCardiaca = 86,
                hrvMilisegundos = 44.0,
                spo2Porcentaje = 98.0,
                pitch = 1.0,
                roll = -2.0,
                yaw = 90.0,
                calidadSensor = "high",
                sensorFlags = new[] { "gps_degraded", "heart_rate_available" }
            }
        }
    };

    [Fact]
    public async Task Pair_NonTargetDevice_Returns400()
    {
        var tokens = await CreateAccountTokensAsync();
        using var mobile = AuthClient(tokens.Mobile);

        var response = await mobile.PostAsJsonAsync(
            "/api/v1/wearable/pair",
            PairPayload("OTHER-001", "Other Watch"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Pair_WebClient_Returns403()
    {
        var tokens = await CreateAccountTokensAsync();
        using var web = AuthClient(tokens.Web);

        var response = await web.PostAsJsonAsync(
            "/api/v1/wearable/pair",
            PairPayload("GW8-WEB-DENIED"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HeartbeatAndDiagnostics_RespectClientCapabilitiesAndPersistStatus()
    {
        var tokens = await CreateAccountTokensAsync();
        using var mobile = AuthClient(tokens.Mobile);
        using var wearable = AuthClient(tokens.Wearable);
        using var web = AuthClient(tokens.Web);
        var deviceId = $"GW8-HB-{Guid.NewGuid():N}";

        var pair = await mobile.PostAsJsonAsync("/api/v1/wearable/pair", PairPayload(deviceId));
        pair.EnsureSuccessStatusCode();
        var pairResponse = await pair.Content.ReadFromJsonAsync<PairResponse>();
        Assert.True(pairResponse!.ExpiresAtUtc > DateTime.UtcNow);

        var confirm = await mobile.PostAsJsonAsync(
            "/api/v1/wearable/pair/confirm",
            new { token = pairResponse.Token });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var mobileHeartbeat = await mobile.PostAsJsonAsync("/api/v1/wearable/heartbeat", new
        {
            dispositivoId = deviceId,
            modelo = "Galaxy Watch 8",
            fabricante = "Samsung",
            plataforma = "WearOS",
            nivelBateria = 80,
            timestampUtc = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.Forbidden, mobileHeartbeat.StatusCode);

        var heartbeat = await wearable.PostAsJsonAsync("/api/v1/wearable/heartbeat", new
        {
            dispositivoId = deviceId,
            modelo = "Galaxy Watch 8",
            fabricante = "Samsung",
            plataforma = "WearOS",
            appVersion = "2.1.0-test",
            versionSistemaOperativo = "WearOS-test-2",
            versionFirmware = "FW-test-2",
            nivelBateria = 79,
            cargando = false,
            desfaseRelojMilisegundos = 25L,
            capacidadesSensores = new[] { "gps", "accelerometer", "gyroscope", "heart_rate" },
            timestampUtc = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);

        var report = await wearable.PostAsJsonAsync("/api/v1/wearable/sensors/diagnostics", new
        {
            dispositivoId = deviceId,
            sensoresDisponibles = new[] { "accelerometer", "gyroscope", "gps", "heart_rate" },
            sensoresNoDisponibles = new[] { "spo2" },
            calidadGeneral = "medium",
            timestampUtc = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);

        var diagnostics = await web.GetFromJsonAsync<SensorDiagnosticsDto>(
            "/api/v1/wearable/sensors/diagnostics");
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics!.Acelerometro);
        Assert.True(diagnostics.Giroscopio);
        Assert.True(diagnostics.Gps);
        Assert.Equal("medium", diagnostics.CalidadGeneral);
        Assert.Contains("spo2", diagnostics.SensoresNoDisponibles);
        Assert.Equal(79, diagnostics.NivelBateria);
    }

    [Fact]
    public async Task EnrichedTelemetry_RoundTrip_IsIdempotentAndReadableByWeb()
    {
        var tokens = await CreateAccountTokensAsync();
        using var mobile = AuthClient(tokens.Mobile);
        using var wearable = AuthClient(tokens.Wearable);
        using var web = AuthClient(tokens.Web);
        var deviceId = $"GW8-TEL-{Guid.NewGuid():N}";

        var pair = await mobile.PostAsJsonAsync(
            "/api/v1/wearable/pair",
            PairPayload(deviceId));
        pair.EnsureSuccessStatusCode();
        var pairResponse = await pair.Content.ReadFromJsonAsync<PairResponse>();
        var confirm = await mobile.PostAsJsonAsync(
            "/api/v1/wearable/pair/confirm",
            new { token = pairResponse!.Token });
        confirm.EnsureSuccessStatusCode();

        var start = await wearable.PostAsJsonAsync(
            "/api/v1/trips/start",
            new { dispositivoId = deviceId });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var trip = await start.Content.ReadFromJsonAsync<ViajeDto>();
        Assert.NotNull(trip);

        var eventId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddSeconds(-2);
        var batch = EnrichedBatch(eventId, batchId, timestamp, deviceId);

        var first = await wearable.PostAsJsonAsync(
            $"/api/v1/trips/{trip!.Id}/telemetry",
            batch);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<TelemetryIngestionResultDto>();
        Assert.Equal(1, firstResult!.Insertados);
        Assert.Equal(batchId, firstResult.BatchId);
        Assert.Equal(TelemetrySchema.EnrichedVersion, firstResult.SchemaVersion);
        Assert.True(firstResult.CapturedOffline);
        Assert.Equal(1L, firstResult.PrimeraSecuencia);

        var duplicate = await wearable.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/telemetry",
            batch);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateResult = await duplicate.Content.ReadFromJsonAsync<TelemetryIngestionResultDto>();
        Assert.Equal(0, duplicateResult!.Insertados);
        Assert.Equal(1, duplicateResult.Duplicados);

        var read = await web.GetAsync($"/api/v1/trips/{trip.Id}/telemetry?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var document = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        var item = document.RootElement.GetProperty("items")[0];

        Assert.Equal(TelemetrySchema.EnrichedVersion, item.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Galaxy Watch 8", item.GetProperty("wearableModel").GetString());
        Assert.Equal(1L, item.GetProperty("sequenceNumber").GetInt64());
        Assert.Equal(5d, item.GetProperty("magnitudAceleracion").GetDouble(), 8);
        Assert.Equal("high", item.GetProperty("calidadSensor").GetString());
        Assert.True(item.GetProperty("capturedOffline").GetBoolean());
    }

    [Fact]
    public async Task OpenApi_ContainsGalaxyWatchOperationalRoutesAndTelemetryV2Description()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        foreach (var path in new[]
                 {
                     "/api/v1/wearable/heartbeat",
                     "/api/v1/wearable/sensors/diagnostics"
                 })
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Falta {path}.");
        }

        var heartbeatResponses = paths
            .GetProperty("/api/v1/wearable/heartbeat")
            .GetProperty("post")
            .GetProperty("responses");
        Assert.True(heartbeatResponses.TryGetProperty("403", out _));

        var telemetry = paths
            .GetProperty("/api/v1/trips/{id}/telemetry")
            .GetProperty("post");
        var description = telemetry.GetProperty("description").GetString();
        Assert.Contains("256 KiB", description);
        Assert.Contains("Galaxy Watch 8", description);
        Assert.Contains("versión 2", description);
    }
}
