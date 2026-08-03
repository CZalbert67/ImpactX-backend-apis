using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class MobileSyncOfflineContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MobileSyncOfflineContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Changes_WithCurrentCursor_ReturnsNoSnapshot()
    {
        using var client = await CreateMobileClientAsync();
        var bootstrap = await client.GetFromJsonAsync<MobileSyncSnapshotDto>("/api/v1/mobile/sync/bootstrap");
        Assert.NotNull(bootstrap);

        var changes = await client.GetFromJsonAsync<MobileSyncChangesDto>(
            $"/api/v1/mobile/sync/changes?cursor={bootstrap!.SyncCursor}");

        Assert.NotNull(changes);
        Assert.False(changes!.HasChanges);
        Assert.False(changes.RequiresBootstrap);
        Assert.Null(changes.Snapshot);
        Assert.Equal(bootstrap.SyncCursor, changes.Cursor);
    }

    [Fact]
    public async Task Push_RepeatedOperationId_IsIdempotent_AndAckPersistsCursor()
    {
        using var client = await CreateMobileClientAsync();
        var bootstrap = await client.GetFromJsonAsync<MobileSyncSnapshotDto>("/api/v1/mobile/sync/bootstrap");
        var operationId = Guid.NewGuid();
        var request = new MobileSyncPushRequest
        {
            ClientInstanceId = $"android-{Guid.NewGuid():N}",
            BaseCursor = bootstrap!.SyncCursor,
            Operations =
            [
                new MobileSyncOperationDto
                {
                    OperationId = operationId,
                    Type = "notification.mark-all-read",
                    CreatedAtUtc = DateTime.UtcNow,
                    Payload = JsonDocument.Parse("{}").RootElement.Clone()
                }
            ]
        };

        var firstResponse = await client.PostAsJsonAsync("/api/v1/mobile/sync/push", request);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<MobileSyncPushResponse>();
        Assert.NotNull(first);
        Assert.Single(first!.Results);
        Assert.Equal("applied", first.Results[0].Result);
        Assert.False(first.Results[0].WasDuplicate);

        var secondResponse = await client.PostAsJsonAsync("/api/v1/mobile/sync/push", request);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<MobileSyncPushResponse>();
        Assert.NotNull(second);
        Assert.True(second!.Results[0].WasDuplicate);

        var ack = await client.PostAsJsonAsync("/api/v1/mobile/sync/ack", new MobileSyncAckRequest
        {
            ClientInstanceId = request.ClientInstanceId,
            Cursor = second.Cursor
        });
        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
    }

    [Fact]
    public async Task Push_ProfileUpdate_AppliesAndIsVisibleInNextSnapshot()
    {
        using var client = await CreateMobileClientAsync();
        var bootstrap = await client.GetFromJsonAsync<MobileSyncSnapshotDto>("/api/v1/mobile/sync/bootstrap");
        var request = new MobileSyncPushRequest
        {
            ClientInstanceId = $"android-{Guid.NewGuid():N}",
            BaseCursor = bootstrap!.SyncCursor,
            Operations =
            [
                new MobileSyncOperationDto
                {
                    OperationId = Guid.NewGuid(),
                    Type = "profile.update",
                    CreatedAtUtc = DateTime.UtcNow,
                    Payload = JsonSerializer.SerializeToElement(new UpdateUserProfileRequest
                    {
                        Nombre = "Perfil sincronizado",
                        Telefono = "+525500000001"
                    })
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/v1/mobile/sync/push", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MobileSyncPushResponse>();
        Assert.NotNull(result);
        Assert.Equal("applied", result!.Results.Single().Result);

        var updated = await client.GetFromJsonAsync<MobileSyncSnapshotDto>("/api/v1/mobile/sync/bootstrap");
        Assert.Equal("Perfil sincronizado", updated!.Profile.Nombre);
        Assert.Equal("+525500000001", updated.Profile.Telefono);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("wearable")]
    public async Task NonMobileClients_CannotUsePushChangesOrAck(string clientType)
    {
        var token = await CreateTokenAsync(clientType);
        using var client = AuthClient(token);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/mobile/sync/changes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/mobile/sync/push", new MobileSyncPushRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/mobile/sync/ack", new MobileSyncAckRequest())).StatusCode);
    }

    private async Task<HttpClient> CreateMobileClientAsync()
        => AuthClient(await CreateTokenAsync("mobile"));

    private async Task<string> CreateTokenAsync(string clientType)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Offline Sync Tester",
            correo = $"offline_sync_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = clientType
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token!;
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
