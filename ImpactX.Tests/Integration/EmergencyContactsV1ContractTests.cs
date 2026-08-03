using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ImpactX.Core.Domain.Enums;
using ImpactX.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Tests.Integration;

public class EmergencyContactsV1ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmergencyContactsV1ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> RegisterAsync(
        string? email = null,
        string clientType = "web")
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        email ??= $"emergency_{suffix}@test.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            registrationVersion = 2,
            nombre = "Emergency Contact Tester",
            username = $"ect_{suffix}",
            correo = email,
            telefono = "+52 771 000 0000",
            password = "Password123!",
            termsAccepted = true,
            privacyAccepted = true,
            client = clientType
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (client, auth);
    }

    [Fact]
    public async Task V1List_DoesNotExposeLegacyExternalContacts()
    {
        var (client, _) = await RegisterAsync();

        var legacy = await client.PostAsJsonAsync("/api/contacts", new
        {
            nombre = "External legacy",
            telefono = "555-1000"
        });
        Assert.Equal(HttpStatusCode.Created, legacy.StatusCode);

        var response = await client.GetAsync("/api/v1/contacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contacts = await response.Content.ReadFromJsonAsync<List<EmergencyContactDto>>();
        Assert.Empty(contacts!);
    }

    [Fact]
    public async Task ExistingUserInvitation_AcceptedByCode_BecomesInternalContact()
    {
        var (ownerClient, owner) = await RegisterAsync();
        var (targetClient, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId,
            relationship = "Hermano",
            priority = "Primary",
            makePrimaryWhenAccepted = true
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();
        Assert.NotNull(invitation);
        Assert.StartsWith("ECT-", invitation!.Contact.PublicContactId, StringComparison.Ordinal);
        Assert.False(Guid.TryParse(invitation.Contact.PublicContactId, out _));
        Assert.Equal(EmergencyContactStatus.Pending, invitation.Contact.Status);
        Assert.False(string.IsNullOrWhiteSpace(invitation.ManualCode));

        var accept = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation.ManualCode
        });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var detail = await ownerClient.GetFromJsonAsync<EmergencyContactDto>(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}");
        Assert.NotNull(detail);
        Assert.Equal(EmergencyContactStatus.Accepted, detail!.Status);
        Assert.Equal(target.Usuario.PublicProfileId, detail.ContactPublicProfileId);
        Assert.Equal("Hermano", detail.Relationship);
        Assert.True(detail.IsPrimary);

        var targetContacts = await targetClient.GetFromJsonAsync<List<EmergencyContactDto>>(
            "/api/v1/contacts");
        Assert.Single(targetContacts!);
        Assert.False(targetContacts![0].IsOwner);
        Assert.Equal(owner.Usuario!.PublicProfileId, targetContacts[0].OwnerPublicProfileId);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Invitation_CannotBeAcceptedByDifferentUser()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();
        var (attackerClient, _) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var response = await attackerClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation!.ManualCode
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmailPreInvitation_CanBeAcceptedAfterRegistration()
    {
        var (ownerClient, _) = await RegisterAsync();
        var email = $"preinvite_{Guid.NewGuid():N}@test.com";

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            email,
            relationship = "Amigo"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var (targetClient, target) = await RegisterAsync(email);
        var pending = await targetClient.GetFromJsonAsync<List<EmergencyContactDto>>(
            "/api/v1/contacts");
        Assert.Single(pending!);
        Assert.Equal(EmergencyContactStatus.Pending, pending![0].Status);

        var accept = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation!.ManualCode
        });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var accepted = await ownerClient.GetFromJsonAsync<EmergencyContactDto>(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}");
        Assert.Equal(target.Usuario!.PublicProfileId, accepted!.ContactPublicProfileId);
        Assert.Equal(EmergencyContactStatus.Accepted, accepted.Status);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task EmailPreInvitation_RequiresManualCode()
    {
        var (ownerClient, _) = await RegisterAsync();
        var email = $"preinvite_proof_{Guid.NewGuid():N}@test.com";
        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new { email });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();
        var (targetClient, _) = await RegisterAsync(email);

        var response = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            publicContactId = invitation!.Contact.PublicContactId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatePendingInvitation_Returns409()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();
        var request = new { publicProfileId = target.Usuario!.PublicProfileId };

        var first = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task BlockedRelationship_PreventsNewInvitation()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (targetClient, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var accept = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation!.ManualCode
        });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var block = await targetClient.PostAsync(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}/block",
            null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        var reinvite = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario.PublicProfileId
        });
        Assert.Equal(HttpStatusCode.Forbidden, reinvite.StatusCode);
    }

    [Fact]
    public async Task OnlyAcceptedContact_CanBeMarkedPrimary()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var response = await ownerClient.PatchAsync(
            $"/api/v1/contacts/{invitation!.Contact.PublicContactId}/primary",
            null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task V1Contract_DoesNotExposePhoneInternalIdsOrCodeHash()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            username = target.Usuario!.Username
        });
        var json = await create.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var contact = document.RootElement.GetProperty("contact");

        Assert.False(contact.TryGetProperty("id", out _));
        Assert.False(contact.TryGetProperty("usuarioId", out _));
        Assert.False(contact.TryGetProperty("contactUserId", out _));
        Assert.False(contact.TryGetProperty("telefono", out _));
        Assert.False(contact.TryGetProperty("invitationCodeHash", out _));
        Assert.True(document.RootElement.TryGetProperty("manualCode", out _));
    }


    [Fact]
    [Trait("Category", "Security")]
    public async Task OwnerBlocksEmailPreInvitation_BlockIsEnforcedInReverseAfterRegistration()
    {
        var (ownerClient, owner) = await RegisterAsync();
        var targetEmail = $"blocked_preinvite_{Guid.NewGuid():N}@test.com";

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            email = targetEmail
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var block = await ownerClient.PostAsync(
            $"/api/v1/contacts/{invitation!.Contact.PublicContactId}/block",
            null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        var (targetClient, _) = await RegisterAsync(targetEmail);
        var reverseInvite = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = owner.Usuario!.PublicProfileId
        });

        Assert.Equal(HttpStatusCode.Forbidden, reverseInvite.StatusCode);
    }

    [Fact]
    public async Task ExpiredInvitation_CannotBeAccepted_AndPersistsExpiredStatus()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (targetClient, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();
        Assert.NotNull(invitation);

        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var entity = await db.ContactosEmergencia.SingleAsync(contact =>
                contact.PublicContactId == invitation!.Contact.PublicContactId);
            entity.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var accept = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation!.ManualCode
        });
        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);

        var detail = await ownerClient.GetFromJsonAsync<EmergencyContactDto>(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}");
        Assert.Equal(EmergencyContactStatus.Expired, detail!.Status);
        Assert.False(detail.IsPrimary);
    }

    [Fact]
    public async Task AcceptedRelationship_CanBeUpdatedAndRevokedByEitherParticipant()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (targetClient, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId,
            relationship = "Amigo",
            priority = "Secondary"
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var accept = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            publicContactId = invitation!.Contact.PublicContactId
        });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var update = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}",
            new { relationship = "Compañero", priority = "Primary" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<EmergencyContactDto>();
        Assert.Equal("Compañero", updated!.Relationship);
        Assert.Equal("Primary", updated.Priority);

        var revoke = await targetClient.DeleteAsync(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var detail = await ownerClient.GetFromJsonAsync<EmergencyContactDto>(
            $"/api/v1/contacts/{invitation.Contact.PublicContactId}");
        Assert.Equal(EmergencyContactStatus.Revoked, detail!.Status);
        Assert.False(detail.IsPrimary);
        Assert.NotNull(detail.RevokedAtUtc);
    }

    [Fact]
    public async Task InvitationCode_IsSingleUse()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (targetClient, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        var first = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation!.ManualCode
        });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var replay = await targetClient.PostAsJsonAsync("/api/v1/contacts/invitations/accept", new
        {
            code = invitation.ManualCode
        });
        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_RequiresExactlyOneTargetIdentifier()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();

        var none = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new { });
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);

        var multiple = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            username = target.Usuario!.Username,
            publicProfileId = target.Usuario.PublicProfileId
        });
        Assert.Equal(HttpStatusCode.BadRequest, multiple.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_CannotTargetSelf()
    {
        var (ownerClient, owner) = await RegisterAsync();

        var response = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = owner.Usuario!.PublicProfileId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task WearableClient_CannotReadOrManageEmergencyContacts()
    {
        var (_, auth) = await RegisterAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.Token);
        var userId = Guid.Parse(jwt.Claims.First(claim =>
            claim.Type == ClaimTypes.NameIdentifier || claim.Type == "nameid").Value);
        var wearable = _factory.CreateClient();
        wearable.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtBuilder.Create(userId, "wearable"));

        var list = await wearable.GetAsync("/api/v1/contacts");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await wearable.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            email = $"wearable_denied_{Guid.NewGuid():N}@test.com"
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }


    [Fact]
    [Trait("Category", "Security")]
    public async Task InvitationManualCode_IsNeverWrittenToApplicationLogs()
    {
        var (ownerClient, _) = await RegisterAsync();
        var (_, target) = await RegisterAsync();

        var create = await ownerClient.PostAsJsonAsync("/api/v1/contacts/invitations", new
        {
            publicProfileId = target.Usuario!.PublicProfileId
        });
        create.EnsureSuccessStatusCode();
        var invitation = await create.Content.ReadFromJsonAsync<CreateEmergencyContactInvitationResponse>();

        Assert.DoesNotContain(
            _factory.LogCapture.LogEntries,
            entry => entry.Contains(invitation!.ManualCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApi_ContainsInternalContactInvitationContract()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/contacts", out var collection));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.False(collection.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/contacts/invitations", out var invitations));
        Assert.True(invitations.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/contacts/invitations/accept", out _));
        Assert.True(paths.TryGetProperty("/api/v1/contacts/{id}", out var detail));
        Assert.True(detail.TryGetProperty("delete", out _));
    }
}
