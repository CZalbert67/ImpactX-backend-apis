using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Models.DTOs.QuickMessages;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Tests.Integration;

public class BackendCompletionContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BackendCompletionContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task WebClient_CanReadTrips_ButCannotControlTripsOrTelemetry()
    {
        var web = await RegisterAsync("web");
        SetBearer(web.Token);

        var read = await _client.GetAsync("/api/v1/trips/active");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var start = await _client.PostAsJsonAsync("/api/v1/trips/start", new
        {
            dispositivoId = "WEB-NOT-ALLOWED"
        });
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    [Fact]
    public async Task WearableClient_CanStartTrip_AndAuditDoesNotMarkMobileFallback()
    {
        var wearable = await RegisterAsync("wearable");
        SetBearer(wearable.Token);

        var response = await _client.PostAsJsonAsync("/api/v1/trips/start", new
        {
            dispositivoId = "WEARABLE-001"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("wearable", json.GetProperty("controlClient").GetString());
        Assert.False(json.GetProperty("mobileFallbackUsed").GetBoolean());
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task MobileClient_CannotStartTrips_Returns403()
    {
        var mobile = await RegisterAsync("mobile");
        SetBearer(mobile.Token);

        var response = await _client.PostAsJsonAsync("/api/v1/trips/start", new
        {
            dispositivoId = "MOBILE-NOT-ALLOWED"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WearableClient_CanStartPauseResumeAndFinishTrip()
    {
        var wearable = await RegisterAsync("wearable");
        SetBearer(wearable.Token);

        var start = await _client.PostAsJsonAsync("/api/v1/trips/start", new
        {
            dispositivoId = "WEARABLE-FLOW-001"
        });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);

        var viaje = await start.Content.ReadFromJsonAsync<JsonElement>();
        var tripId = viaje.GetProperty("id").GetGuid();
        Assert.Equal("wearable", viaje.GetProperty("controlClient").GetString());
        Assert.False(viaje.GetProperty("mobileFallbackUsed").GetBoolean());

        var pause = await _client.PostAsync($"/api/v1/trips/{tripId}/pause", null);
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);

        var resume = await _client.PostAsync($"/api/v1/trips/{tripId}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);

        var finish = await _client.PostAsync($"/api/v1/trips/{tripId}/finish", null);
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);
        var finished = await finish.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Finalizado", finished.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task FamilySubscription_InvitationAcceptance_InheritsPlanAndVehicleQuota()
    {
        var owner = await RegisterAsync("web");
        var member = await RegisterAsync("web");

        SetBearer(owner.Token);
        var activation = await _client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new { planName = "Basic" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);

        var invite = await _client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new
            {
                publicProfileId = member.PublicProfileId,
                createMonitoringRelationship = false
            });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var invitation = await invite.Content.ReadFromJsonAsync<CreateFamilyInvitationResponse>();
        Assert.NotNull(invitation);
        Assert.False(string.IsNullOrWhiteSpace(invitation!.ManualCode));

        SetBearer(member.Token);
        var accept = await _client.PostAsync(
            $"/api/v1/family-subscriptions/invitations/{invitation.Invitation.PublicInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var current = await _client.GetFromJsonAsync<FamilySubscriptionSummaryDto>(
            "/api/v1/family-subscriptions/current");
        Assert.NotNull(current);
        Assert.Equal("Standard", current!.PlanName);
        Assert.Equal(3, current.VehicleLimitPerUser);

        for (var index = 0; index < 3; index++)
        {
            var vehicle = await _client.PostAsJsonAsync("/api/v1/vehicles", VehicleBody(index));
            Assert.Equal(HttpStatusCode.Created, vehicle.StatusCode);
        }

        var fourth = await _client.PostAsJsonAsync("/api/v1/vehicles", VehicleBody(4));
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task MonitoringConsentAndQuickMessages_RequireAcceptedPermissionedRelationship()
    {
        var monitor = await RegisterAsync("web");
        var monitored = await RegisterAsync("web");

        SetBearer(monitor.Token);
        var create = await _client.PostAsJsonAsync(
            "/api/v1/monitoring-relationships/invitations",
            new
            {
                publicProfileId = monitored.PublicProfileId,
                permissions = new
                {
                    sendMessages = true,
                    viewIncidents = true,
                    viewMedicalProfile = true
                }
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = await create.Content.ReadFromJsonAsync<CreateMonitoringInvitationResponse>();
        Assert.NotNull(invitation);
        Assert.False(invitation!.Relationship.Permissions.ViewMedicalProfile);

        SetBearer(monitored.Token);
        var accept = await _client.PostAsJsonAsync(
            "/api/v1/monitoring-relationships/invitations/accept",
            new { publicRelationshipId = invitation.Relationship.PublicRelationshipId });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        SetBearer(monitor.Token);
        var recipients = await _client.GetFromJsonAsync<List<QuickMessageRecipientDto>>(
            "/api/v1/quick-messages/recipients");
        Assert.NotNull(recipients);
        var recipient = Assert.Single(recipients!);
        Assert.Equal(monitored.PublicProfileId, recipient.RecipientPublicProfileId);
        Assert.Equal(invitation.Relationship.PublicRelationshipId, recipient.PublicRelationshipId);

        var medicalBeforeConsent = await _client.GetAsync(
            $"/api/v1/monitoring-relationships/{invitation.Relationship.PublicRelationshipId}/medical-profile");
        Assert.Equal(HttpStatusCode.Forbidden, medicalBeforeConsent.StatusCode);

        SetBearer(monitored.Token);
        var withoutConsent = await _client.PatchAsJsonAsync(
            $"/api/v1/monitoring-relationships/{invitation.Relationship.PublicRelationshipId}/permissions",
            PermissionBody(viewMedical: true, confirmMedical: false));
        Assert.Equal(HttpStatusCode.BadRequest, withoutConsent.StatusCode);

        var withConsent = await _client.PatchAsJsonAsync(
            $"/api/v1/monitoring-relationships/{invitation.Relationship.PublicRelationshipId}/permissions",
            PermissionBody(viewMedical: true, confirmMedical: true));
        Assert.Equal(HttpStatusCode.OK, withConsent.StatusCode);
        var updated = await withConsent.Content.ReadFromJsonAsync<MonitoringRelationshipDto>();
        Assert.True(updated!.Permissions.ViewMedicalProfile);

        SetBearer(monitor.Token);
        var medicalAfterConsent = await _client.GetAsync(
            $"/api/v1/monitoring-relationships/{invitation.Relationship.PublicRelationshipId}/medical-profile");
        Assert.Equal(HttpStatusCode.OK, medicalAfterConsent.StatusCode);

        var send = await _client.PostAsJsonAsync("/api/v1/quick-messages/send", new
        {
            recipientPublicProfileId = monitored.PublicProfileId,
            publicTemplateId = "SYS-QM-001"
        });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        var message = await send.Content.ReadFromJsonAsync<QuickMessageDto>();
        Assert.NotNull(message);
        Assert.Equal("Estoy bien", message!.Text);

        SetBearer(monitored.Token);
        var unread = await _client.GetFromJsonAsync<JsonElement>("/api/v1/quick-messages/unread-count");
        Assert.Equal(1, unread.GetProperty("unreadCount").GetInt32());

        var markRead = await _client.PatchAsync(
            $"/api/v1/quick-messages/{message.PublicMessageId}/read",
            null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var unreadAfter = await _client.GetFromJsonAsync<JsonElement>("/api/v1/quick-messages/unread-count");
        Assert.Equal(0, unreadAfter.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task QuickMessage_SendWithoutAcceptedRelationship_ReturnsNotFoundOrForbidden()
    {
        var sender = await RegisterAsync("web");
        var recipient = await RegisterAsync("web");
        SetBearer(sender.Token);

        var send = await _client.PostAsJsonAsync("/api/v1/quick-messages/send", new
        {
            recipientPublicProfileId = recipient.PublicProfileId,
            publicTemplateId = "SYS-QM-001"
        });

        Assert.True(send.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
    }

    private async Task<RegisteredUser> RegisterAsync(string client)
    {
        var email = $"backend_{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Backend Completion Tester",
            correo = email,
            password = "Password123!",
            client
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new RegisteredUser(
            auth!.Token!,
            auth.Usuario!.PublicProfileId,
            auth.Usuario.Username);
    }

    private void SetBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object VehicleBody(int index)
    {
        return new
        {
            tipoVehiculo = "Automovil",
            marca = $"Marca {index}",
            modelo = $"Modelo {index}",
            ano = 2024,
            velocidadPromedio = 60 + index,
            usoPrincipalVehiculo = "Mixto"
        };
    }

    private static object PermissionBody(bool viewMedical, bool confirmMedical)
    {
        return new
        {
            viewRoutes = true,
            viewLocation = true,
            viewEmergencyLocation = true,
            viewIncidents = true,
            receiveCriticalAlerts = true,
            viewMedicalProfile = viewMedical,
            confirmMedicalConsent = confirmMedical,
            sendMessages = true,
            viewTelemetry = true,
            receiveNotifications = true
        };
    }

    private sealed record RegisteredUser(string Token, string PublicProfileId, string Username);
}
