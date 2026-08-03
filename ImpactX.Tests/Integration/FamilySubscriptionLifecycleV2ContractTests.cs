using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactX.Tests.Integration;

public sealed class FamilySubscriptionLifecycleV2ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FamilySubscriptionLifecycleV2ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExpiredFamilyPlan_EntersThreeDayGrace_ThenReturnsOwnerToFree()
    {
        var email = $"family_lifecycle_{Guid.NewGuid():N}@test.com";
        using var anonymous = _factory.CreateClient();
        var registration = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Family Lifecycle Tester",
            correo = email,
            password = "Password123!",
            client = "web"
        });
        registration.EnsureSuccessStatusCode();
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        using var client = AuthClient(auth!.Token!);

        var activation = await client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Standard" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);
        var initial = await activation.Content.ReadFromJsonAsync<FamilySubscriptionSummaryDto>();
        Assert.Equal("Standard", initial!.PlanName);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var user = await db.Usuarios.SingleAsync(value => value.Correo == email);
            var family = await db.FamilySubscriptions.SingleAsync(value => value.OwnerUserId == user.Id);
            family.PeriodEndUtc = DateTime.UtcNow.AddMinutes(-1);
            family.NextBillingAtUtc = family.PeriodEndUtc;
            await db.SaveChangesAsync();
        });

        await ProcessLifecycleAsync();
        var grace = await client.GetFromJsonAsync<FamilySubscriptionSummaryDto>(
            "/api/v1/family-subscriptions/current");
        Assert.NotNull(grace);
        Assert.Equal(FamilySubscriptionStatus.PastDue, grace!.Status);
        Assert.NotNull(grace.GraceEndsAtUtc);
        Assert.InRange(
            grace.GraceEndsAtUtc!.Value - grace.PeriodEndUtc,
            TimeSpan.FromHours(71),
            TimeSpan.FromHours(73));

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var user = await db.Usuarios.SingleAsync(value => value.Correo == email);
            var family = await db.FamilySubscriptions.SingleAsync(value => value.OwnerUserId == user.Id);
            family.Status = FamilySubscriptionStatus.PastDue;
            family.GraceEndsAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        await ProcessLifecycleAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.GetAsync("/api/v1/family-subscriptions/current")).StatusCode);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var user = await db.Usuarios.SingleAsync(value => value.Correo == email);
            Assert.Equal("Free", user.PlanActivo);
            var family = await db.FamilySubscriptions.SingleAsync(value => value.OwnerUserId == user.Id);
            Assert.Equal(FamilySubscriptionStatus.Expired, family.Status);
        });
    }

    private async Task ProcessLifecycleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IFamilySubscriptionService>();
        await service.ProcessLifecycleAsync(DateTime.UtcNow);
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
