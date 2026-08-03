using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Models.DTOs.QuickMessages;

namespace ImpactX.Tests.Integration;

public sealed class RelationshipDirectionAndConversationContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RelationshipDirectionAndConversationContractTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FamilyAcceptance_ReportsRealPeopleCount_AndInvitedMemberMonitorsOwner()
    {
        var owner = await RegisterAsync("owner");
        var member = await RegisterAsync("member");
        using var ownerClient = AuthClient(owner.Token);
        using var memberClient = AuthClient(member.Token);

        var activation = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Free" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new
            {
                publicProfileId = member.PublicProfileId,
                createMonitoringRelationship = true
            });
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invite = await inviteResponse.Content
            .ReadFromJsonAsync<CreateFamilyInvitationResponse>();
        Assert.NotNull(invite);

        var accept = await memberClient.PostAsync(
            $"/api/v1/family-subscriptions/invitations/{invite!.Invitation.PublicInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var ownerSummary = await ownerClient
            .GetFromJsonAsync<FamilySubscriptionSummaryDto>(
                "/api/v1/family-subscriptions/current");
        Assert.NotNull(ownerSummary);
        Assert.Equal(1, ownerSummary!.AcceptedMembers);
        Assert.Equal(2, ownerSummary.TotalActivePeople);
        Assert.Equal(2, ownerSummary.TotalPeopleLimit);
        Assert.Equal(0, ownerSummary.AvailableMemberSlots);

        var memberSummary = await memberClient
            .GetFromJsonAsync<FamilySubscriptionSummaryDto>(
                "/api/v1/family-subscriptions/current");
        Assert.NotNull(memberSummary);
        Assert.Equal("Member", memberSummary!.CurrentUserRole.ToString());
        Assert.Equal(2, memberSummary.TotalActivePeople);
        Assert.Equal(owner.PublicProfileId, memberSummary.OwnerPublicProfileId);

        var forbiddenPlanChange = await memberClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/change-plan",
            new ChangeFamilyPlanRequest { PlanName = "Premium" });
        Assert.Equal(HttpStatusCode.NotFound, forbiddenPlanChange.StatusCode);

        var memberRelationships = await memberClient
            .GetFromJsonAsync<List<MonitoringRelationshipDto>>(
                "/api/v1/monitoring-relationships");
        Assert.NotNull(memberRelationships);
        var relationship = Assert.Single(memberRelationships!.Where(value =>
            value.Status.ToString() == "Accepted"));
        Assert.Equal(member.PublicProfileId, relationship.MonitorPublicProfileId);
        Assert.Equal(owner.PublicProfileId, relationship.MonitoredPublicProfileId);

        var monitorView = await memberClient.GetAsync(
            $"/api/v1/monitoring-relationships/{relationship.PublicRelationshipId}/trips");
        Assert.Equal(HttpStatusCode.OK, monitorView.StatusCode);

        var ownerCannotUseMonitorView = await ownerClient.GetAsync(
            $"/api/v1/monitoring-relationships/{relationship.PublicRelationshipId}/trips");
        Assert.Equal(HttpStatusCode.NotFound, ownerCannotUseMonitorView.StatusCode);
    }

    [Fact]
    public async Task OpenConversation_MarksAllIncomingMessagesAsRead()
    {
        var monitored = await RegisterAsync("monitored");
        var monitor = await RegisterAsync("monitor");
        using var monitoredClient = AuthClient(monitored.Token);
        using var monitorClient = AuthClient(monitor.Token);

        var create = await monitoredClient.PostAsJsonAsync(
            "/api/v1/monitoring-relationships/invitations",
            new
            {
                publicProfileId = monitor.PublicProfileId,
                direction = "MonitoredRequestsMonitor",
                permissions = new { sendMessages = true }
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = await create.Content
            .ReadFromJsonAsync<CreateMonitoringInvitationResponse>();
        Assert.NotNull(invitation);

        var accept = await monitorClient.PostAsJsonAsync(
            "/api/v1/monitoring-relationships/invitations/accept",
            new { publicRelationshipId = invitation!.Relationship.PublicRelationshipId });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        for (var index = 0; index < 2; index++)
        {
            var sent = await monitoredClient.PostAsJsonAsync(
                "/api/v1/quick-messages/send",
                new
                {
                    recipientPublicProfileId = monitor.PublicProfileId,
                    publicTemplateId = index == 0 ? "SYS-QM-001" : "SYS-QM-002"
                });
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var unreadBefore = await monitorClient
            .GetFromJsonAsync<JsonElement>("/api/v1/quick-messages/unread-count");
        Assert.Equal(2, unreadBefore.GetProperty("unreadCount").GetInt32());

        var markRead = await monitorClient.PatchAsync(
            $"/api/v1/quick-messages/conversations/{monitored.PublicProfileId}/read",
            null);
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);
        var marked = await markRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, marked.GetProperty("marked").GetInt32());

        var unreadAfter = await monitorClient
            .GetFromJsonAsync<JsonElement>("/api/v1/quick-messages/unread-count");
        Assert.Equal(0, unreadAfter.GetProperty("unreadCount").GetInt32());

        var history = await monitorClient
            .GetFromJsonAsync<List<QuickMessageDto>>(
                $"/api/v1/quick-messages/history?otherPublicProfileId={monitored.PublicProfileId}");
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.All(history, value => Assert.True(value.IsRead));
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = $"Relationship {prefix}",
            correo = $"relationship_{prefix}_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = "web"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new RegisteredUser(auth!.Token!, auth.Usuario!.PublicProfileId);
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record RegisteredUser(string Token, string PublicProfileId);
}
