using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class MobileSyncBootstrapContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MobileSyncBootstrapContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MobileClient_ReceivesCompleteReadOnlyBootstrapSnapshot()
    {
        var tokens = await CreateAccountTokensAsync();
        using var client = AuthClient(tokens.Mobile);

        var response = await client.GetAsync("/api/v1/mobile/sync/bootstrap");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<MobileSyncSnapshotDto>();
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.ContractVersion);
        Assert.NotEqual(Guid.Empty, snapshot.SnapshotId);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.SyncCursor));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Profile.PublicProfileId));
        Assert.NotNull(snapshot.EffectiveSubscription);
        Assert.Equal("Free", snapshot.EffectiveSubscription.PlanNombre);
        Assert.NotNull(snapshot.Permissions);
        Assert.NotNull(snapshot.Vehicles);
        Assert.NotNull(snapshot.EmergencyContacts);
        Assert.NotNull(snapshot.MonitoringRelationships);
        Assert.NotNull(snapshot.ActiveIncidents);
        Assert.NotNull(snapshot.QuickMessageTemplates);
        Assert.NotNull(snapshot.QuickMessageRecipients);
        Assert.Equal("wearable", snapshot.OfflineContract.TelemetryWriter);
        Assert.False(snapshot.OfflineContract.MobileMayControlTrip);
        Assert.True(snapshot.OfflineContract.MobileMayReadTripState);
        Assert.Equal("operationId/eventId", snapshot.OfflineContract.IdempotencyKey);
        Assert.True(snapshot.OfflineContract.MaxTelemetryEventsPerBatch > 0);
        Assert.True(snapshot.OfflineContract.MaxTelemetryBodyBytes > 0);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("wearable")]
    public async Task NonMobileClients_CannotUseMobileBootstrap(string clientType)
    {
        var tokens = await CreateAccountTokensAsync();
        var token = clientType == "web" ? tokens.Web : tokens.Wearable;
        using var client = AuthClient(token);

        var response = await client.GetAsync("/api/v1/mobile/sync/bootstrap");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_MobileBootstrap_Documents403()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/v1/mobile/sync/bootstrap", json, StringComparison.Ordinal);
        Assert.Contains("\"403\"", json, StringComparison.Ordinal);
    }

    private async Task<(string Mobile, string Wearable, string Web)> CreateAccountTokensAsync()
    {
        using var client = _factory.CreateClient();
        var email = $"mobile_sync_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Mobile Sync Tester",
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
}
