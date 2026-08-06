using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class AutomaticImpactDetectionContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AutomaticImpactDetectionContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SevereTelemetry_IsLabeledServerSideAndCreatesOneImmediateAlert()
    {
        var tokens = await CreateAccountTokensAsync();
        using var wearable = AuthClient(tokens.Wearable);
        using var mobile = AuthClient(tokens.Mobile);
        var (tripId, deviceId) = await StartTripAsync(wearable);
        var eventId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddSeconds(-1);
        var payload = SeverePayload(eventId, timestamp, deviceId);

        var first = await wearable.PostAsJsonAsync(
            $"/api/v1/trips/{tripId}/telemetry",
            payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var telemetryResponse = await mobile.GetAsync(
            $"/api/v1/trips/{tripId}/telemetry?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, telemetryResponse.StatusCode);
        var telemetryJson = await telemetryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var point = telemetryJson.GetProperty("items")[0];
        Assert.True(point.GetProperty("impactCandidate").GetBoolean());
        Assert.Equal("impact_candidate", point.GetProperty("detectionLabel").GetString());
        var severity = point.GetProperty("severityLabel").GetString();
        Assert.True(severity is "severe" or "critical");
        Assert.Equal("impact-rules-v1", point.GetProperty("ruleVersion").GetString());
        Assert.True(point.GetProperty("detectionScore").GetInt32() >= 7);

        var alerts = await mobile.GetAsync("/api/v1/alerts?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, alerts.StatusCode);
        var alertsJson = await alerts.Content.ReadFromJsonAsync<JsonElement>();
        var alertItems = alertsJson.GetProperty("items");
        Assert.Single(alertItems.EnumerateArray());
        var alert = alertItems[0];
        Assert.Equal("Enviada", alert.GetProperty("estado").GetString());
        Assert.True(alert.GetProperty("esBypassCritico").GetBoolean());
        Assert.Equal(eventId, alert.GetProperty("sourceTelemetryEventId").GetGuid());
        Assert.Equal("impact-rules-v1", alert.GetProperty("ruleVersion").GetString());

        var duplicate = await wearable.PostAsJsonAsync(
            $"/api/v1/trips/{tripId}/telemetry",
            payload);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var alertsAfterRetry = await mobile.GetFromJsonAsync<JsonElement>("/api/v1/alerts?pageSize=10");
        Assert.Single(alertsAfterRetry.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ModerateTelemetry_CreatesPendingAlertWithTenSecondWindow()
    {
        var tokens = await CreateAccountTokensAsync();
        using var wearable = AuthClient(tokens.Wearable);
        using var mobile = AuthClient(tokens.Mobile);
        var (tripId, deviceId) = await StartTripAsync(wearable);

        var response = await wearable.PostAsJsonAsync(
            $"/api/v1/trips/{tripId}/telemetry",
            ModeratePayload(Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1), deviceId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var alerts = await mobile.GetFromJsonAsync<JsonElement>("/api/v1/alerts?pageSize=10");
        var alert = alerts.GetProperty("items")[0];
        Assert.Equal("Pendiente", alert.GetProperty("estado").GetString());
        Assert.False(alert.GetProperty("esBypassCritico").GetBoolean());
        Assert.Equal("moderate", alert.GetProperty("severidad").GetString());
        Assert.True(alert.GetProperty("autoSendAtUtc").ValueKind == JsonValueKind.String);
        Assert.InRange(alert.GetProperty("cancellationSecondsRemaining").GetInt32(), 0, 10);
    }

    [Fact]
    public async Task MobileClient_CannotCallLegacyAutomaticDetectEndpoint()
    {
        var tokens = await CreateAccountTokensAsync();
        using var mobile = AuthClient(tokens.Mobile);

        var response = await mobile.PostAsJsonAsync("/api/v1/alerts/detect", new
        {
            lat = 19.43,
            lng = -99.13,
            gForce = 4.0,
            decibeles = 0,
            frecuenciaCardiaca = 120,
            severidad = "severe"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(string Mobile, string Wearable)> CreateAccountTokensAsync()
    {
        using var client = _factory.CreateClient();
        var email = $"impact_auto_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Automatic Impact Tester",
            correo = email,
            password,
            client = "mobile"
        });
        register.EnsureSuccessStatusCode();
        var mobile = await register.Content.ReadFromJsonAsync<AuthResponse>();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password,
            client = "wearable"
        });
        login.EnsureSuccessStatusCode();
        var wearable = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (mobile!.Token!, wearable!.Token!);
    }

    private static async Task<(Guid TripId, string DeviceId)> StartTripAsync(HttpClient client)
    {
        var deviceId = $"GW8-AUTO-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/v1/trips/start", new
        {
            dispositivoId = deviceId
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetGuid(), deviceId);
    }

    private static object SeverePayload(Guid eventId, DateTime timestamp, string deviceId) => new
    {
        schemaVersion = 2,
        batchId = Guid.NewGuid(),
        batchSequence = 1L,
        wearableDeviceId = deviceId,
        wearableModel = "Galaxy Watch 8",
        batteryLevel = 80,
        eventos = new[]
        {
            new
            {
                eventId,
                timestamp,
                sequenceNumber = 1L,
                lat = 19.4326,
                lng = -99.1332,
                velocidad = 92.0,
                gpsAccuracyMeters = 4.0,
                aceleracionX = 37.0,
                aceleracionY = 0.0,
                aceleracionZ = 0.0,
                giroscopioX = 6.5,
                giroscopioY = 0.0,
                giroscopioZ = 0.0,
                desaceleracion = 19.0,
                frecuenciaCardiaca = 150,
                calidadSensor = "high",
                sensorFlags = Array.Empty<string>()
            }
        }
    };

    private static object ModeratePayload(Guid eventId, DateTime timestamp, string deviceId) => new
    {
        schemaVersion = 2,
        batchId = Guid.NewGuid(),
        batchSequence = 1L,
        wearableDeviceId = deviceId,
        wearableModel = "Galaxy Watch 8",
        batteryLevel = 80,
        eventos = new[]
        {
            new
            {
                eventId,
                timestamp,
                sequenceNumber = 1L,
                lat = 19.4326,
                lng = -99.1332,
                velocidad = 30.0,
                gpsAccuracyMeters = 4.0,
                aceleracionX = 22.0,
                aceleracionY = 0.0,
                aceleracionZ = 0.0,
                giroscopioX = 1.0,
                giroscopioY = 0.0,
                giroscopioZ = 0.0,
                desaceleracion = 8.0,
                frecuenciaCardiaca = 110,
                calidadSensor = "medium",
                sensorFlags = Array.Empty<string>()
            }
        }
    };

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
