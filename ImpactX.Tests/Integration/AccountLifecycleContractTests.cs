using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class AccountLifecycleContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountLifecycleContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportRetentionAndConsentRevocation_AreAvailableToMobile()
    {
        var account = await CreateAccountAsync("mobile");
        using var client = AuthClient(account.Token);

        var exportResponse = await client.GetAsync("/api/v1/account/export");
        exportResponse.EnsureSuccessStatusCode();
        var export = await exportResponse.Content.ReadFromJsonAsync<AccountExportV2Dto>();
        Assert.NotNull(export);
        Assert.Equal(2, export!.ContractVersion);
        Assert.Equal(account.Email, export.Profile.Correo);
        Assert.NotNull(export.EffectiveSubscription);

        var retention = await client.GetFromJsonAsync<AccountRetentionDto>("/api/v1/account/retention");
        Assert.NotNull(retention);
        Assert.Equal(90, retention!.TripsAndTelemetryDays);
        Assert.Equal(365, retention.AlertsAndIncidentsDays);
        Assert.Equal(30, retention.NotificationsDays);

        var revoke = await client.PostAsJsonAsync("/api/v1/account/consents/revoke", new RevokeConsentsRequest
        {
            RevokeLocationIncidentConsent = true,
            RevokeDrivingPatternConsent = true,
            RemoveMedicalProfile = true
        });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
    }

    [Fact]
    public async Task Delete_RequiresPasswordAndExactConfirmation_ThenAnonymizesAccount()
    {
        var account = await CreateAccountAsync("web");
        using var client = AuthClient(account.Token);

        var rejected = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/account")
        {
            Content = JsonContent.Create(new DeleteAccountV2Request
            {
                Password = account.Password,
                Confirmation = "delete"
            })
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/account")
        {
            Content = JsonContent.Create(new DeleteAccountV2Request
            {
                Password = account.Password,
                Confirmation = "DELETE",
                Reason = "Prueba de contrato"
            })
        });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var result = await deleted.Content.ReadFromJsonAsync<DeleteAccountV2Response>();
        Assert.NotNull(result);
        Assert.True(result!.Deleted);
        Assert.True(result.IdentityAnonymized);

        using var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = account.Email,
            password = account.Password,
            client = "web"
        });
        var loginResult = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(loginResult!.Success);
    }

    [Fact]
    public async Task Wearable_CannotExportOrDeleteAccount()
    {
        var account = await CreateAccountAsync("wearable");
        using var client = AuthClient(account.Token);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/account/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/account")
            {
                Content = JsonContent.Create(new DeleteAccountV2Request
                {
                    Password = account.Password,
                    Confirmation = "DELETE"
                })
            })).StatusCode);
    }

    private async Task<(string Token, string Email, string Password)> CreateAccountAsync(string clientType)
    {
        using var client = _factory.CreateClient();
        var email = $"account_v2_{Guid.NewGuid():N}@test.com";
        const string password = "Password123!";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Account Lifecycle Tester",
            correo = email,
            password,
            client = clientType
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token!, email, password);
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
