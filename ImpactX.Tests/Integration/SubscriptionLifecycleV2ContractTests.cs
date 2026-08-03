using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public sealed class SubscriptionLifecycleV2ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubscriptionLifecycleV2ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewAccount_HasPermanentFreeSubscription_NotTrial()
    {
        using var client = await CreateClientAsync();
        var current = await client.GetFromJsonAsync<SuscripcionDto>("/api/v1/subscriptions");

        Assert.NotNull(current);
        Assert.Equal("Free", current!.PlanNombre);
        Assert.Equal("Activa", current.Estado);
        Assert.Null(current.TrialFin);
        Assert.Null(current.Fin);
    }

    [Fact]
    public async Task ActivateStandard_CreatesCompletedSimulatedPayment_AndCanRenew()
    {
        using var client = await CreateClientAsync();
        var activateResponse = await client.PostAsJsonAsync("/api/v1/subscriptions/activate", new ChangePlanRequest
        {
            PlanNombre = "Standard",
            BillingCycle = "Monthly",
            MetodoPago = "Simulated"
        });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = await activateResponse.Content.ReadFromJsonAsync<SubscriptionPaymentResultDto>();
        Assert.NotNull(activated);
        Assert.Equal("Standard", activated!.Subscription.PlanNombre);
        Assert.Equal("Completado", activated.Payment.Estado);
        Assert.Equal(99m, activated.Payment.Monto);

        var renewResponse = await client.PostAsJsonAsync("/api/v1/subscriptions/renew", new RenewSubscriptionRequest());
        Assert.Equal(HttpStatusCode.OK, renewResponse.StatusCode);
        var renewed = await renewResponse.Content.ReadFromJsonAsync<SubscriptionPaymentResultDto>();
        Assert.NotNull(renewed);
        Assert.True(renewed!.Subscription.Fin > activated.Subscription.Fin);
    }

    [Fact]
    public async Task EffectiveSubscription_ExposesFrontendEntitlements()
    {
        using var client = await CreateClientAsync();
        var effective = await client.GetFromJsonAsync<EffectiveSubscriptionDto>("/api/v1/subscriptions/effective");

        Assert.NotNull(effective);
        Assert.Equal("Free", effective!.PlanNombre);
        Assert.Equal(1, effective.VehicleLimit);
        Assert.Equal(1, effective.InvitedMemberLimit);
        Assert.False(effective.ExportEnabled);
    }

    [Fact]
    public async Task Wearable_CannotManageSubscriptions()
    {
        var token = await CreateTokenAsync("wearable");
        using var client = AuthClient(token);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/subscriptions/effective")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/subscriptions/activate", new ChangePlanRequest
            {
                PlanNombre = "Standard"
            })).StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync()
        => AuthClient(await CreateTokenAsync("mobile"));

    private async Task<string> CreateTokenAsync(string clientType)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Subscription Lifecycle Tester",
            correo = $"subscription_v2_{Guid.NewGuid():N}@test.com",
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
