using System.Net;
using System.Net.Http.Json;
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
        return result!.Token;
    }

    [Fact]
    public async Task GetWearable_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/wearable");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
            nombre = "Apple Watch",
            modelo = "Series 9",
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
            nombre = "Samsung Watch",
            modelo = "Galaxy 6",
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
    public async Task UpdateBattery_WithAuth_Updates()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await PairAndCompleteFlowAsync();

        var response = await _client.PatchAsJsonAsync("/api/wearable/battery", new { nivel = 85 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wearable = await response.Content.ReadFromJsonAsync<WearableDto>();
        Assert.Equal(85, wearable!.NivelBateria);
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
            nombre = "Test Watch",
            modelo = "Test Model",
        });
        var pairResult = await pairResponse.Content.ReadFromJsonAsync<PairResponse>();
        await _client.PostAsJsonAsync("/api/wearable/pair/confirm", new
        {
            token = pairResult!.Token,
        });
    }
}
