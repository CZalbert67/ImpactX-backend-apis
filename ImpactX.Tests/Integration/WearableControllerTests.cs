using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class WearableControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WearableControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"wear_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Wear Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token!;
    }

    private async Task<(string MobileToken, string WearableToken)> RegisterAndGetClientTokensAsync()
    {
        var email = $"wear_multi_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";

        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Wear Multi Tester",
            correo = email,
            password,
            client = "mobile"
        });
        register.EnsureSuccessStatusCode();
        var mobile = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            identifier = email,
            password,
            client = "wearable"
        });
        login.EnsureSuccessStatusCode();
        var wearable = await login.Content.ReadFromJsonAsync<AuthResponse>();

        return (mobile!.Token!, wearable!.Token!);
    }

    [Fact]
    public async Task GetWearable_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/wearable");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetWearableAll_LegacyWithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/wearable/all");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetWearableAll_V1WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/wearable/all");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWearableAll_V1AndLegacy_ReturnSameStatusAndJsonStructure()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var legacy = await _client.GetAsync("/api/wearable/all?pageSize=10");
        var v1 = await _client.GetAsync("/api/v1/wearable/all?pageSize=10");

        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.Equal(legacy.StatusCode, v1.StatusCode);

        var legacyDoc = JsonDocument.Parse(await legacy.Content.ReadAsStringAsync());
        var v1Doc = JsonDocument.Parse(await v1.Content.ReadAsStringAsync());

        var legacyProps = legacyDoc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        var v1Props = v1Doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(legacyProps, v1Props);
        Assert.Contains("items", legacyProps);
        Assert.Contains("pageSize", legacyProps);
        Assert.Contains("hasMoreResults", legacyProps);
        Assert.Equal(
            legacyDoc.RootElement.GetProperty("pageSize").GetInt32(),
            v1Doc.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task OpenApi_ContainsWearableAllV1Route()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/wearable/all", out var pathItem));
        Assert.True(pathItem.TryGetProperty("get", out _));
    }

    [Fact]
    public async Task OpenApi_WearableAll_DocumentsPaginationParameters()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pathItem = doc.RootElement.GetProperty("paths").GetProperty("/api/v1/wearable/all");
        var operation = pathItem.GetProperty("get");
        var parameters = operation.GetProperty("parameters");

        var pageSize = parameters.EnumerateArray().First(p => p.GetProperty("name").GetString() == "pageSize");
        var token = parameters.EnumerateArray().First(p => p.GetProperty("name").GetString() == "continuationToken");

        Assert.Equal("query", pageSize.GetProperty("in").GetString());
        Assert.Equal("query", token.GetProperty("in").GetString());
        Assert.Equal(JsonValueKind.Object, pageSize.GetProperty("schema").ValueKind);
        Assert.Equal(JsonValueKind.Object, token.GetProperty("schema").ValueKind);
    }

    [Fact]
    public async Task OpenApi_DoesNotContainPluralWearableV1Route()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToList();

        Assert.DoesNotContain(paths, p => p.Name.Contains("/api/v1/wearables", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetWearable_WithoutLinkedDevice_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/wearable");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PairWearable_WithAuth_ReturnsToken()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/wearable/pair", new
        {
            dispositivoId = "WATCH-001",
            nombre = "Galaxy Watch 8",
            modelo = "Galaxy Watch 8",
            fabricante = "Samsung",
            plataforma = "WearOS",
            capacidadesSensores = new[] { "accelerometer", "gyroscope", "gps", "heart_rate" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PairResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result!.Token);
    }

    [Fact]
    public async Task PairAndConfirm_FullFlow()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var pairResponse = await _client.PostAsJsonAsync("/api/wearable/pair", new
        {
            dispositivoId = "WATCH-002",
            nombre = "Galaxy Watch 8",
            modelo = "Galaxy Watch 8",
            fabricante = "Samsung",
            plataforma = "WearOS",
        });
        var pairResult = await pairResponse.Content.ReadFromJsonAsync<PairResponse>();

        var confirmResponse = await _client.PostAsJsonAsync("/api/wearable/pair/confirm", new
        {
            token = pairResult!.Token,
        });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var wearable = await confirmResponse.Content.ReadFromJsonAsync<WearableDto>();
        Assert.Equal("Vinculado", wearable!.Estado);
    }

    [Fact]
    public async Task GetBatteryDiagnostics_WithAuth_ReturnsDiagnostics()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await PairAndCompleteFlowAsync();
        var response = await _client.GetAsync("/api/wearable/sensors/diagnostics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var diagnostics = await response.Content.ReadFromJsonAsync<SensorDiagnosticsDto>();
        Assert.NotNull(diagnostics);
    }

    [Fact]
    public async Task UpdateBattery_WithWearableClient_Updates()
    {
        var tokens = await RegisterAndGetClientTokensAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.MobileToken);

        await PairAndCompleteFlowAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.WearableToken);

        var response = await _client.PatchAsJsonAsync("/api/v1/wearable/battery", new
        {
            nivel = 85,
            cargando = true
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wearable = await response.Content.ReadFromJsonAsync<WearableDto>();
        Assert.Equal(85, wearable!.NivelBateria);
        Assert.True(wearable.Cargando);
    }

    [Fact]
    public async Task Calibrate_WithAuth_Calibrates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await PairAndCompleteFlowAsync();

        var response = await _client.PostAsJsonAsync("/api/wearable/calibration", new
        {
            acelerometro = true,
            giroscopio = true,
            gps = true,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wearable = await response.Content.ReadFromJsonAsync<WearableDto>();
        Assert.True(wearable!.Calibrado);
    }

    [Fact]
    public async Task Unlink_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/wearable/unlink");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unlink_WithAuth_Unlinks()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await PairAndCompleteFlowAsync();

        var response = await _client.DeleteAsync("/api/wearable/unlink");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync("/api/wearable");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private async Task PairAndCompleteFlowAsync()
    {
        var pairResponse = await _client.PostAsJsonAsync("/api/wearable/pair", new
        {
            dispositivoId = $"WATCH-{Guid.NewGuid():N}",
            nombre = "Galaxy Watch 8",
            modelo = "Galaxy Watch 8",
            fabricante = "Samsung",
            plataforma = "WearOS",
            capacidadesSensores = new[] { "accelerometer", "gyroscope", "gps", "heart_rate", "hrv", "spo2" },
        });
        var pairResult = await pairResponse.Content.ReadFromJsonAsync<PairResponse>();
        await _client.PostAsJsonAsync("/api/wearable/pair/confirm", new
        {
            token = pairResult!.Token,
        });
    }
}
