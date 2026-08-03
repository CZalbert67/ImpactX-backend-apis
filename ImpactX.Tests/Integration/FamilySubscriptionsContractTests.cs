using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;

namespace ImpactX.Tests.Integration;

public class FamilySubscriptionsContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FamilySubscriptionsContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private void SetBearer(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<(string Token, string PublicProfileId)> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Family Contract Tester",
            correo = $"family_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = "web"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token!, auth.Usuario!.PublicProfileId);
    }

    [Fact]
    public async Task GetCurrent_WithoutSubscription_Returns204()
    {
        var user = await RegisterAsync();
        SetBearer(user.Token);

        var response = await _client.GetAsync("/api/v1/family-subscriptions/current");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetInvitations_WithoutSubscription_Returns200EmptyArray()
    {
        var user = await RegisterAsync();
        SetBearer(user.Token);

        var response = await _client.GetAsync("/api/v1/family-subscriptions/invitations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<List<FamilyInvitationDto>>();
        Assert.NotNull(invitations);
        Assert.Empty(invitations!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetInvitations_AsMember_DoesNotExposeOwnerInvitations()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var pendingTarget = await RegisterAsync();

        SetBearer(owner.Token);
        var activation = await _client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/activate",
            new { planName = "Basic" });
        Assert.Equal(HttpStatusCode.Created, activation.StatusCode);

        var memberInvite = await _client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new { publicProfileId = member.PublicProfileId, createMonitoringRelationship = false });
        memberInvite.EnsureSuccessStatusCode();
        var memberInvitation = await memberInvite.Content.ReadFromJsonAsync<CreateFamilyInvitationResponse>();

        var pendingInvite = await _client.PostAsJsonAsync(
            "/api/v1/family-subscriptions/invitations",
            new { publicProfileId = pendingTarget.PublicProfileId, createMonitoringRelationship = false });
        Assert.Equal(HttpStatusCode.Created, pendingInvite.StatusCode);

        SetBearer(member.Token);
        var accept = await _client.PostAsync(
            $"/api/v1/family-subscriptions/invitations/{memberInvitation!.Invitation.PublicInvitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var memberList = await _client.GetAsync("/api/v1/family-subscriptions/invitations");
        Assert.Equal(HttpStatusCode.OK, memberList.StatusCode);
        var invitations = await memberList.Content.ReadFromJsonAsync<List<FamilyInvitationDto>>();
        Assert.Empty(invitations!);
    }
}
