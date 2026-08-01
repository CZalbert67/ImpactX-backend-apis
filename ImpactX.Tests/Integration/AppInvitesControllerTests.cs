using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class AppInvitesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AppInvitesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"invite_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Invite Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetInvites_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/invites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvite_WithAuth_CreatesInvite()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/invites", new
        {
            suggestedUsername = "amigo123",
            relation = "Hermano",
            priority = "Primario",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invite = await response.Content.ReadFromJsonAsync<AppInviteDto>();
        Assert.NotNull(invite);
        Assert.StartsWith("INV-", invite!.Token);
        Assert.Contains(invite.Token, invite.InviteUrl);
        Assert.Equal("Pendiente de registro", invite.Status);
    }

    [Fact]
    public async Task GetInvites_WithAuth_ReturnsCreatedInvites()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/v1/invites", new { suggestedUsername = "amigo1" });
        await _client.PostAsJsonAsync("/api/v1/invites", new { suggestedUsername = "amigo2" });

        var response = await _client.GetAsync("/api/v1/invites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invites = await response.Content.ReadFromJsonAsync<List<AppInviteDto>>();
        Assert.Equal(2, invites!.Count);
    }

    [Fact]
    public async Task AcceptInvite_WithValidToken_MarksAccepted()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/invites", new { suggestedUsername = "amigo" });
        var invite = await createResponse.Content.ReadFromJsonAsync<AppInviteDto>();

        var response = await _client.PostAsJsonAsync("/api/v1/invites/accept", new { token = invite!.Token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<AppInviteDto>();
        Assert.Equal("Aceptado", accepted!.Status);
    }

    [Fact]
    public async Task AcceptInvite_WithUnknownToken_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/invites/accept", new { token = "INV-XXX-0000" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelInvite_ByOwner_Cancels()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/invites", new { suggestedUsername = "amigo" });
        var invite = await createResponse.Content.ReadFromJsonAsync<AppInviteDto>();

        var response = await _client.PostAsync($"/api/v1/invites/{invite!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<AppInviteDto>();
        Assert.Equal("Cancelado", cancelled!.Status);
    }
}
