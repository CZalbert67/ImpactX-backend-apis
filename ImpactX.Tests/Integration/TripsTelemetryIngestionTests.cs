using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Tests.Integration;

public class TripsTelemetryIngestionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TripsTelemetryIngestionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpClient client, string token)> RegisterAndGetTokenAsync()
    {
        var email = $"ingest_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Ingest Tester",
            correo = email,
            password = "Password123!",
            client = "wearable"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var client = _factory.CreateClient();
        var token = result!.Token!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private static async Task<ViajeDto> StartTripAsync(HttpClient client, string dispositivoId = "WEAR-ING-001")
    {
        var response = await client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId,
            proposito = "Prueba ingesta",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ViajeDto>())!;
    }

    private static object Evento(Guid eventId, DateTime? timestamp = null, double lat = 19.43, double lng = -99.13) => new
    {
        eventId,
        timestamp = timestamp ?? DateTime.UtcNow,
        lat,
        lng,
        velocidad = 50,
    };

    private static string EventoJson(string eventId, string timestamp, string lat = "19.43", string lng = "-99.13") =>
        $"{{\"eventId\":\"{eventId}\",\"timestamp\":\"{timestamp}\",\"lat\":{lat},\"lng\":{lng},\"velocidad\":50}}";

    [Fact]
    public async Task IngestTelemetry_OwnActiveTrip_SingleEvent_Returns200WithCounts()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid()) }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(viaje.Id.ToString(), body.GetProperty("viajeId").GetString());
        Assert.Equal(1, body.GetProperty("recibidos").GetInt32());
        Assert.Equal(1, body.GetProperty("insertados").GetInt32());
        Assert.Equal(0, body.GetProperty("duplicados").GetInt32());
        Assert.True(body.TryGetProperty("primerEventoUtc", out _));
        Assert.True(body.TryGetProperty("ultimoEventoUtc", out _));
    }

    [Fact]
    public async Task IngestTelemetry_MultipleEvents_InsertsAll()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[]
            {
                Evento(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-3)),
                Evento(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-2)),
                Evento(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1)),
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("recibidos").GetInt32());
        Assert.Equal(3, body.GetProperty("insertados").GetInt32());
        Assert.Equal(0, body.GetProperty("duplicados").GetInt32());
    }

    [Fact]
    public async Task IngestTelemetry_EmptyBatch_Returns400ProblemDetails()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new { eventos = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Cosmos", body);
        Assert.DoesNotContain("StackTrace", body);
    }

    [Fact]
    public async Task IngestTelemetry_101Events_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = Enumerable.Range(0, 101).Select(_ => Evento(Guid.NewGuid())).ToList()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_DuplicateEventIdWithinRequest_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        var eventId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(eventId), Evento(eventId) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_ResendSameBatch_Returns200Inserted0WithoutPhysicalDuplicates()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        var eventIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var body = new { eventos = eventIds.Select(id => Evento(id, timestamp)).ToList() };

        var first = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, firstBody.GetProperty("insertados").GetInt32());

        var second = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", body);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, secondBody.GetProperty("recibidos").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("insertados").GetInt32());
        Assert.Equal(2, secondBody.GetProperty("duplicados").GetInt32());

        var count = await _factory.ExecuteInDbContextAsync(db =>
            db.ViajeTelemetries.CountAsync(t => t.ViajeId == viaje.Id));
        Assert.Equal(2, count);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetry_SameEventIdDifferentPayload_Returns409()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        var eventId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);

        var first = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(eventId, timestamp, lat: 19.43) }
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(eventId, timestamp, lat: 20.0) }
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.DoesNotContain(eventId.ToString(), body);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetry_OtherUsersTrip_Returns404WithoutLeaking()
    {
        var (clientA, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(clientA, "WEAR-ING-002");

        var (clientB, _) = await RegisterAndGetTokenAsync();
        var response = await clientB.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid()) }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("19.43", body);
        Assert.DoesNotContain("usuario", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No tienes permiso", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestTelemetry_NonexistentTrip_Returns404()
    {
        var (client, _) = await RegisterAndGetTokenAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid()) }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_FinishedTrip_Returns409()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        await client.PostAsync($"/api/trips/{viaje.Id}/finish", null);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid()) }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var count = await _factory.ExecuteInDbContextAsync(db =>
            db.ViajeTelemetries.CountAsync(t => t.ViajeId == viaje.Id));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task IngestTelemetry_TimestampWithoutUtcDesignator_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var body = "{\"eventos\":[" + EventoJson(Guid.NewGuid().ToString(), "2026-08-01T10:00:00") + "]}";
        var response = await client.PostAsync($"/api/v1/trips/{viaje.Id}/telemetry",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_TimestampWithNonUtcOffset_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var body = "{\"eventos\":[" + EventoJson(Guid.NewGuid().ToString(), "2026-08-01T10:00:00+02:00") + "]}";
        var response = await client.PostAsync($"/api/v1/trips/{viaje.Id}/telemetry",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_TimestampFutureBeyond5Minutes_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(6)) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetry_NaNPayload_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var body = "{\"eventos\":[" + EventoJson(Guid.NewGuid().ToString(), "2026-08-01T10:00:00Z", lat: "NaN") + "]}";
        var response = await client.PostAsync($"/api/v1/trips/{viaje.Id}/telemetry",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_LatitudeOutOfRange_Returns400()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid(), lat: 91.0) }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestTelemetry_GetTelemetryStillWorks_AfterIngestion()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        var eventId = Guid.NewGuid();

        var ingest = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(eventId) }
        });
        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);

        var get = await client.GetAsync($"/api/v1/trips/{viaje.Id}/telemetry?pageSize=20");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var page = await get.Content.ReadFromJsonAsync<PagedResultDto>();
        Assert.NotNull(page);
        Assert.Single(page!.Items!);
    }

    [Fact]
    public async Task IngestTelemetry_LegacyRoute_AlsoAcceptsBatch()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(Guid.NewGuid()) }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void IngestTelemetry_ExcessivePayload_EnforcedBodySizeLimit()
    {
        // El límite del cuerpo HTTP se aplica a nivel de servidor (Kestrel):
        // [RequestSizeLimit] responde 413 cuando el cuerpo supera los 32 KB
        // (verificado contra Kestrel real). TestServer no aplica
        // MaxRequestBodySize, así que aquí se valida la configuración del endpoint.
        var metodo = typeof(ImpactX.Controllers.TripsController)
            .GetMethod(nameof(ImpactX.Controllers.TripsController.IngestTelemetry))!;
        var atributo = metodo.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false)
            .Cast<RequestSizeLimitAttribute>()
            .Single();
        var metadata = (IRequestSizeLimitMetadata)atributo;
        Assert.Equal(TelemetryIngestionLimits.MaxBodyBytes, metadata.MaxRequestBodySize);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetry_ProblemDetails_DoNotExposeInternals()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new { eventos = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Cosmos", body);
        Assert.DoesNotContain("ViajeTelemetry", body);
        Assert.DoesNotContain("partition", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetry_Logs_DoNotContainSensitivePayload()
    {
        var (client, _) = await RegisterAndGetTokenAsync();
        var viaje = await StartTripAsync(client);
        var eventId = Guid.NewGuid();
        const double lat = 19.432601;
        const double lng = -99.133208;
        var timestamp = DateTime.UtcNow.AddMinutes(-1);

        var response = await client.PostAsJsonAsync($"/api/v1/trips/{viaje.Id}/telemetry", new
        {
            eventos = new[] { Evento(eventId, timestamp, lat, lng) }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var joined = string.Join("\n", _factory.LogCapture.LogEntries);
        Assert.DoesNotContain(lat.ToString(System.Globalization.CultureInfo.InvariantCulture), joined);
        Assert.DoesNotContain(lng.ToString(System.Globalization.CultureInfo.InvariantCulture), joined);
        Assert.DoesNotContain(eventId.ToString(), joined);
        Assert.DoesNotContain(timestamp.ToString("o"), joined);
    }

    [Fact]
    public async Task OpenApi_DocumentsTelemetryGetAndPost()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();

        var pathItem = doc.GetProperty("paths").GetProperty("/api/v1/trips/{id}/telemetry");

        Assert.True(pathItem.TryGetProperty("get", out var getOperation));
        Assert.True(getOperation.TryGetProperty("parameters", out _));

        Assert.True(pathItem.TryGetProperty("post", out var postOperation));
        var description = postOperation.GetProperty("description").GetString();
        Assert.Contains("100", description);
        Assert.Contains("reintentos", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EventId", description);
        Assert.Contains("UTC", description);

        var requestBody = postOperation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Contains("TelemetryBatchRequest", requestBody.GetProperty("$ref").GetString());

        var responses = postOperation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("403", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.True(responses.TryGetProperty("409", out _));
        Assert.True(responses.TryGetProperty("429", out _));
        Assert.True(responses.TryGetProperty("500", out _));

        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("TelemetryBatchRequest", out var batchSchema));
        Assert.True(batchSchema.GetProperty("properties").TryGetProperty("eventos", out _));
        Assert.True(schemas.TryGetProperty("TelemetryEventRequest", out var eventSchema));
        Assert.True(eventSchema.GetProperty("properties").TryGetProperty("eventId", out _));
        Assert.True(eventSchema.GetProperty("properties").TryGetProperty("timestamp", out _));
        Assert.True(schemas.TryGetProperty("TelemetryIngestionResultDto", out var resultSchema));
        var resultProps = resultSchema.GetProperty("properties");
        foreach (var prop in new[] { "viajeId", "recibidos", "insertados", "duplicados", "primerEventoUtc", "ultimoEventoUtc" })
        {
            Assert.True(resultProps.TryGetProperty(prop, out _), $"Falta {prop} en TelemetryIngestionResultDto");
        }
    }

    private sealed class PagedResultDto
    {
        public List<object>? Items { get; set; }
    }
}
