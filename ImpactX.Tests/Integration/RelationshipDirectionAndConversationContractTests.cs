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
    public async Task GroupAcceptance_SuspendsPersonalFree_ConnectsAllMembers_AndLeaveRestoresFree()
    {
        var owner = await RegisterAsync("owner");
        var member = await RegisterAsync("member");
        using var ownerClient = AuthClient(owner.Token);
        using var memberClient = AuthClient(member.Token);

        var ownerActivation = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Free" });
        Assert.Equal(HttpStatusCode.Created, ownerActivation.StatusCode);
        var ownerFree = await ownerActivation.Content
            .ReadFromJsonAsync<FamilySubscriptionSummaryDto>();
        Assert.NotNull(ownerFree);

        var memberActivation = await memberClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Free" });
        Assert.Equal(HttpStatusCode.Created, memberActivation.StatusCode);
        var memberPersonalFree = await memberActivation.Content
            .ReadFromJsonAsync<FamilySubscriptionSummaryDto>();
        Assert.NotNull(memberPersonalFree);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new { publicProfileId = member.PublicProfileId });
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
        Assert.True(ownerSummary.CanManagePlan);
        Assert.True(ownerSummary.CanInviteMembers);
        Assert.False(ownerSummary.CanLeaveGroup);
        Assert.Equal(1, ownerSummary.VehicleLimitPerUser);

        var memberSummary = await memberClient
            .GetFromJsonAsync<FamilySubscriptionSummaryDto>(
                "/api/v1/family-subscriptions/current");
        Assert.NotNull(memberSummary);
        Assert.Equal("Member", memberSummary!.CurrentUserRole.ToString());
        Assert.Equal(ownerSummary.PublicSubscriptionId, memberSummary.PublicSubscriptionId);
        Assert.NotEqual(memberPersonalFree!.PublicSubscriptionId, memberSummary.PublicSubscriptionId);
        Assert.Equal(2, memberSummary.TotalActivePeople);
        Assert.Equal(owner.PublicProfileId, memberSummary.OwnerPublicProfileId);
        Assert.False(memberSummary.CanManagePlan);
        Assert.False(memberSummary.CanInviteMembers);
        Assert.True(memberSummary.CanLeaveGroup);
        Assert.Equal(1, memberSummary.VehicleLimitPerUser);

        var forbiddenPlanChange = await memberClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/change-plan",
            new ChangeFamilyPlanRequest { PlanName = "Premium" });
        Assert.Equal(HttpStatusCode.Conflict, forbiddenPlanChange.StatusCode);

        var memberRelationships = await memberClient
            .GetFromJsonAsync<List<MonitoringRelationshipDto>>(
                "/api/v1/monitoring-relationships");
        Assert.NotNull(memberRelationships);
        Assert.Equal(2, memberRelationships!.Count(value =>
            value.Status.ToString() == "Accepted"));
        var memberMonitorsOwner = Assert.Single(memberRelationships, value =>
            value.MonitorPublicProfileId == member.PublicProfileId
            && value.MonitoredPublicProfileId == owner.PublicProfileId);

        var deniedByDefault = await memberClient.GetAsync(
            $"/api/v1/monitoring-relationships/{memberMonitorsOwner.PublicRelationshipId}/trips");
        Assert.Equal(HttpStatusCode.Forbidden, deniedByDefault.StatusCode);

        var access = await ownerClient.GetFromJsonAsync<List<FamilyMemberAccessDto>>(
            "/api/v1/family-subscriptions/members/access");
        var memberAccess = Assert.Single(access!);
        Assert.Equal(member.PublicProfileId, memberAccess.ViewerPublicProfileId);
        Assert.True(memberAccess.Permissions.SendMessages);
        Assert.True(memberAccess.Permissions.ReceiveCriticalAlerts);
        Assert.False(memberAccess.Permissions.ViewRoutes);

        var permissions = await ownerClient.PutAsJsonAsync(
            $"/api/v1/family-subscriptions/members/{member.PublicProfileId}/access",
            new UpdateFamilyMemberAccessRequest
            {
                ViewRoutes = true,
                ViewLocation = false,
                ViewEmergencyLocation = true,
                ViewIncidents = true,
                ReceiveCriticalAlerts = true,
                ViewMedicalProfile = false,
                SendMessages = true,
                ViewTelemetry = false,
                ReceiveNotifications = true,
                ConfirmMedicalConsent = false,
                SosPriority = 1
            });
        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);

        var updatedAccess = await permissions.Content
            .ReadFromJsonAsync<FamilyMemberAccessDto>();
        Assert.NotNull(updatedAccess);
        Assert.Equal(
            memberMonitorsOwner.PublicRelationshipId,
            updatedAccess!.PublicRelationshipId);
        Assert.True(updatedAccess.Permissions.ViewRoutes);

        var persistedAccessList = await ownerClient
            .GetFromJsonAsync<List<FamilyMemberAccessDto>>(
                "/api/v1/family-subscriptions/members/access");
        var persistedAccess = Assert.Single(persistedAccessList!, value =>
            value.PublicRelationshipId
                == memberMonitorsOwner.PublicRelationshipId);
        Assert.True(persistedAccess.Permissions.ViewRoutes);

        var refreshedMemberRelationships = await memberClient
            .GetFromJsonAsync<List<MonitoringRelationshipDto>>(
                "/api/v1/monitoring-relationships");
        var refreshedMemberMonitorsOwner = Assert.Single(refreshedMemberRelationships!, value =>
                value.PublicRelationshipId
                    == memberMonitorsOwner.PublicRelationshipId);
        Assert.True(refreshedMemberMonitorsOwner.Permissions.ViewRoutes);

        var memberNotifications = await memberClient
            .GetFromJsonAsync<List<NotificacionDto>>("/api/v1/notifications");
        Assert.Contains(memberNotifications!, notification =>
            notification.Evento == "SosContactDesignated"
            && notification.PublicRelationshipId == memberMonitorsOwner.PublicRelationshipId
            && notification.DeepLink == "/app/contacts");

        var monitorView = await memberClient.GetAsync(
            $"/api/v1/monitoring-relationships/{memberMonitorsOwner.PublicRelationshipId}/trips");
        Assert.Equal(HttpStatusCode.OK, monitorView.StatusCode);

        var telemetryStillDenied = await memberClient.GetAsync(
            $"/api/v1/monitoring-relationships/{memberMonitorsOwner.PublicRelationshipId}"
            + $"/trips/{Guid.NewGuid()}/telemetry");
        Assert.Equal(HttpStatusCode.Forbidden, telemetryStillDenied.StatusCode);

        var ownerCannotUseMonitorView = await ownerClient.GetAsync(
            $"/api/v1/monitoring-relationships/{memberMonitorsOwner.PublicRelationshipId}/trips");
        Assert.Equal(HttpStatusCode.NotFound, ownerCannotUseMonitorView.StatusCode);

        var leave = await memberClient.PostAsync(
            "/api/v1/family-subscriptions/leave",
            null);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        var restored = await memberClient
            .GetFromJsonAsync<FamilySubscriptionSummaryDto>(
                "/api/v1/family-subscriptions/current");
        Assert.NotNull(restored);
        Assert.Equal(memberPersonalFree.PublicSubscriptionId, restored!.PublicSubscriptionId);
        Assert.Equal("Owner", restored.CurrentUserRole.ToString());
        Assert.Equal(1, restored.TotalActivePeople);
        Assert.Equal(2, restored.TotalPeopleLimit);
    }

    [Fact]
    public async Task OwnerCanRevokePendingGroupInvitation_AndReleaseReservedSlot()
    {
        var owner = await RegisterAsync("revoke_owner");
        var target = await RegisterAsync("revoke_target");
        using var ownerClient = AuthClient(owner.Token);
        using var targetClient = AuthClient(target.Token);

        var activation = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new ActivateFamilySubscriptionRequest { PlanName = "Free" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);

        var create = await ownerClient.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new { publicProfileId = target.PublicProfileId });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var response = await create.Content.ReadFromJsonAsync<CreateFamilyInvitationResponse>();
        Assert.NotNull(response);

        var reserved = await ownerClient.GetFromJsonAsync<FamilySubscriptionSummaryDto>(
            "/api/v1/family-subscriptions/current");
        Assert.NotNull(reserved);
        Assert.Equal(1, reserved!.PendingInvitationCount);
        Assert.Equal(0, reserved.AvailableMemberSlots);

        var revoke = await ownerClient.DeleteAsync(
            $"/api/v1/family-subscriptions/invitations/{response!.Invitation.PublicInvitationId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var released = await ownerClient.GetFromJsonAsync<FamilySubscriptionSummaryDto>(
            "/api/v1/family-subscriptions/current");
        Assert.NotNull(released);
        Assert.Equal(0, released!.PendingInvitationCount);
        Assert.Equal(1, released.AvailableMemberSlots);

        var incoming = await targetClient.GetFromJsonAsync<List<IncomingFamilyInvitationDto>>(
            "/api/v1/family-subscriptions/invitations/incoming");
        Assert.Empty(incoming!);

        var notifications = await targetClient.GetFromJsonAsync<List<NotificacionDto>>(
            "/api/v1/notifications");
        Assert.Contains(notifications!, value => value.Evento == "GroupInvitationRevoked");
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
