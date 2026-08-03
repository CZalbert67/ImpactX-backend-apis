using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;

namespace ImpactX.Tests.Integration;

public sealed class FamilyInvitationLiveUpdatesContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public FamilyInvitationLiveUpdatesContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IncomingInvitation_IsVisibleToTarget_AndPendingInvitesReserveCapacity()
    {
        var owner = await RegisterAsync("owner");
        var firstTarget = await RegisterAsync("first");
        var secondTarget = await RegisterAsync("second");
        var thirdTarget = await RegisterAsync("third");

        using var ownerClient = AuthClient(owner.Token);
        var activation = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Standard" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);
        var summary = await activation.Content.ReadFromJsonAsync<FamilySubscriptionSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.InvitedMemberLimit);
        Assert.Equal(2, summary.AvailableMemberSlots);

        var firstInvitation = await CreateInvitationAsync(ownerClient, firstTarget.PublicProfileId);
        await CreateInvitationAsync(ownerClient, secondTarget.PublicProfileId);

        var overCapacity = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new
            {
                publicProfileId = thirdTarget.PublicProfileId,
                createMonitoringRelationship = false
            });
        Assert.Equal(HttpStatusCode.Conflict, overCapacity.StatusCode);

        using var targetClient = AuthClient(firstTarget.Token);
        var incoming = await targetClient.GetFromJsonAsync<List<IncomingFamilyInvitationDto>>(
            "/api/v1/family-subscriptions/invitations/incoming");
        Assert.NotNull(incoming);
        var invitation = Assert.Single(incoming!);
        Assert.Equal(firstInvitation.Invitation.PublicInvitationId, invitation.PublicInvitationId);
        Assert.Equal(owner.PublicProfileId, invitation.OwnerPublicProfileId);
        Assert.Equal("Standard", invitation.PlanName);

        var accept = await targetClient.PostAsync(
            $"/api/v1/family-subscriptions/invitations/{invitation.PublicInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var afterAccept = await targetClient.GetFromJsonAsync<List<IncomingFamilyInvitationDto>>(
            "/api/v1/family-subscriptions/invitations/incoming");
        Assert.NotNull(afterAccept);
        Assert.Empty(afterAccept!);

        var ownerSummary = await ownerClient.GetFromJsonAsync<FamilySubscriptionSummaryDto>(
            "/api/v1/family-subscriptions/current");
        Assert.NotNull(ownerSummary);
        Assert.Equal(1, ownerSummary!.AcceptedMembers);
        Assert.Equal(2, ownerSummary.InvitedMemberLimit);
        Assert.Equal(0, ownerSummary.AvailableMemberSlots);
    }

    private async Task<CreateFamilyInvitationResponse> CreateInvitationAsync(
        HttpClient client,
        string publicProfileId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new
            {
                publicProfileId,
                createMonitoringRelationship = false
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateFamilyInvitationResponse>())!;
    }

    private async Task<(string Token, string PublicProfileId)> RegisterAsync(string prefix)
    {
        using var anonymous = _factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = $"Family {prefix}",
            correo = $"family_{prefix}_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = "web"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token!, auth.Usuario!.PublicProfileId);
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
