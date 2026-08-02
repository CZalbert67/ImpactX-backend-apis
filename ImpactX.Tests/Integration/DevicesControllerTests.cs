using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class DevicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DevicesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"device_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Device Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    private void SetBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string UniqueToken(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

    private static object ValidDeviceBody(string deviceId, string token, string? name = null) => new
    {
        deviceId,
        platform = "Android",
        token,
        name,
    };

    [Fact]
    public async Task GetDevices_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpsertFcmToken_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-1")));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDevice_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1/devices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAllDevices_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_CreatesDeviceAndListOnlyOwnDevices()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();

        SetBearer(tokenA);
        var putA = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-a", UniqueToken("token-a")));
        Assert.Equal(HttpStatusCode.NoContent, putA.StatusCode);

        var listA = await _client.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.OK, listA.StatusCode);
        var devicesA = await listA.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.NotNull(devicesA);
        Assert.Single(devicesA!);
        Assert.Equal("phone-a", devicesA![0].DeviceId);

        SetBearer(tokenB);
        var listB = await _client.GetAsync("/api/v1/devices");
        var devicesB = await listB.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.NotNull(devicesB);
        Assert.Empty(devicesB!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeleteDevice_OfAnotherUser_ReturnsNotFound()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();

        SetBearer(tokenA);
        var putA = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-a", UniqueToken("token-a")));
        Assert.Equal(HttpStatusCode.NoContent, putA.StatusCode);
        var listA = await _client.GetAsync("/api/v1/devices");
        var devicesA = await listA.Content.ReadFromJsonAsync<List<DeviceDto>>();
        var deviceIdA = devicesA![0].Id;

        SetBearer(tokenB);
        var deleteB = await _client.DeleteAsync($"/api/v1/devices/{deviceIdA}");

        Assert.Equal(HttpStatusCode.NotFound, deleteB.StatusCode);

        SetBearer(tokenA);
        var listAfter = await _client.GetAsync("/api/v1/devices");
        var devicesAfter = await listAfter.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Single(devicesAfter!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_DoesNotHonorUsuarioIdFromBody()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();
        var userBId = await GetUserIdAsync(tokenB);

        SetBearer(tokenA);
        var putA = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", new
        {
            deviceId = "phone-a",
            platform = "Android",
            token = UniqueToken("token-a"),
            usuarioId = userBId,
        });
        Assert.Equal(HttpStatusCode.NoContent, putA.StatusCode);

        var listA = await _client.GetAsync("/api/v1/devices");
        var devicesA = await listA.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Single(devicesA!);
        Assert.Equal("phone-a", devicesA![0].DeviceId);

        SetBearer(tokenB);
        var listB = await _client.GetAsync("/api/v1/devices");
        var devicesB = await listB.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Empty(devicesB!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_ReplaceTokenAndNoDuplicates()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        var first = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-v1")));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-v2")));
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var third = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("watch-1", UniqueToken("token-v2")));
        Assert.Equal(HttpStatusCode.NoContent, third.StatusCode);

        var list = await _client.GetAsync("/api/v1/devices");
        var devices = await list.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.NotNull(devices);
        Assert.Equal(2, devices!.Count);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeviceList_NeverContainsFcmToken()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        var secretToken = "secret-fcm-" + Guid.NewGuid().ToString("N");
        var put = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", secretToken));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var list = await _client.GetAsync("/api/v1/devices");
        var body = await list.Content.ReadAsStringAsync();

        Assert.DoesNotContain(secretToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenFcm", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeviceOperations_DoNotLogFcmToken()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        var secretToken = "log-secret-fcm-" + Guid.NewGuid().ToString("N");
        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", secretToken));
        await _client.GetAsync("/api/v1/devices");
        await _client.DeleteAsync("/api/v1/devices");

        foreach (var entry in _factory.LogCapture.LogEntries)
        {
            Assert.DoesNotContain(secretToken, entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DeleteDevice_OwnDevice_ReturnsNoContent()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-1")));
        var list = await _client.GetAsync("/api/v1/devices");
        var devices = await list.Content.ReadFromJsonAsync<List<DeviceDto>>();

        var delete = await _client.DeleteAsync($"/api/v1/devices/{devices![0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listAfter = await _client.GetAsync("/api/v1/devices");
        var devicesAfter = await listAfter.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Empty(devicesAfter!);
    }

    [Fact]
    public async Task DeleteAllDevices_RevokesEverything()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-1")));
        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("watch-1", UniqueToken("token-2")));

        var delete = await _client.DeleteAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await _client.GetAsync("/api/v1/devices");
        var devices = await list.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Empty(devices!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_ClearsLegacyFcmToken()
    {
        var (email, token) = await RegisterAndGetTokenAsyncWithEmail();
        SetBearer(token);

        var legacy = await _client.PutAsJsonAsync("/api/users/me/fcm-token", new { token = "legacy-token-123" });
        Assert.Equal(HttpStatusCode.NoContent, legacy.StatusCode);

        var put = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-v1")));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var usuario = db.Usuarios.Single(u => u.Correo == email);
            Assert.Null(usuario.FcmToken);
        });
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeleteAllDevices_ClearsLegacyFcmToken()
    {
        var (email, token) = await RegisterAndGetTokenAsyncWithEmail();
        SetBearer(token);

        await _client.PutAsJsonAsync("/api/users/me/fcm-token", new { token = "legacy-token-123" });
        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-v1")));

        var delete = await _client.DeleteAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var usuario = db.Usuarios.Single(u => u.Correo == email);
            Assert.Null(usuario.FcmToken);
        });
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DeleteLastDevice_ClearsLegacyFcmToken()
    {
        var (email, token) = await RegisterAndGetTokenAsyncWithEmail();
        SetBearer(token);

        await _client.PutAsJsonAsync("/api/users/me/fcm-token", new { token = "legacy-token-123" });
        await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", UniqueToken("token-v1")));
        var list = await _client.GetAsync("/api/v1/devices");
        var devices = await list.Content.ReadFromJsonAsync<List<DeviceDto>>();

        var delete = await _client.DeleteAsync($"/api/v1/devices/{devices![0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var usuario = db.Usuarios.Single(u => u.Correo == email);
            Assert.Null(usuario.FcmToken);
        });
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_WithTokenOfAnotherUser_ReturnsConflict()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();

        SetBearer(tokenA);
        var putA = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-a", "token-shared-123"));
        Assert.Equal(HttpStatusCode.NoContent, putA.StatusCode);

        SetBearer(tokenB);
        var putB = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-b", "token-shared-123"));
        Assert.Equal(HttpStatusCode.Conflict, putB.StatusCode);

        var listB = await _client.GetAsync("/api/v1/devices");
        var devicesB = await listB.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Empty(devicesB!);

        SetBearer(tokenA);
        var listA = await _client.GetAsync("/api/v1/devices");
        var devicesA = await listA.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.Single(devicesA!);
        Assert.Equal("phone-a", devicesA![0].DeviceId);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UpsertFcmToken_NewDevice_TokenUsedBySameUserDevice_DeactivatesPrevious()
    {
        var token = await RegisterAndGetTokenAsync();
        SetBearer(token);

        var first = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("watch-1", "token-shared-v1"));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await _client.PutAsJsonAsync("/api/v1/devices/fcm-token", ValidDeviceBody("phone-1", "token-shared-v1"));
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var list = await _client.GetAsync("/api/v1/devices");
        var devices = await list.Content.ReadFromJsonAsync<List<DeviceDto>>();
        Assert.NotNull(devices);
        Assert.Equal(2, devices!.Count);
        var watch = devices.Single(d => d.DeviceId == "watch-1");
        Assert.False(watch.Activo);
    }

    private async Task<(string Email, string Token)> RegisterAndGetTokenAsyncWithEmail()
    {
        var email = $"device_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Device Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (email, result!.Token!);
    }

    private Task<Guid> GetUserIdAsync(string token)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var userId = Guid.Parse(jwt.Claims.First(c => c.Type == "nameid" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        return Task.FromResult(userId);
    }
}
