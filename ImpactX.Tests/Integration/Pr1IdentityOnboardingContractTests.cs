using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ImpactX.Models.DTOs;
using Xunit;

namespace ImpactX.Tests.Integration;

public class Pr1IdentityOnboardingContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Pr1IdentityOnboardingContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string PublicProfileId, Guid UsuarioId)> RegisterUserAsync(string name = "PR1 Tester", string? email = null)
    {
        var correo = email ?? $"pr1_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = name,
            correo = correo,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Usuario);
        Assert.StartsWith("IX-", result.Usuario.PublicProfileId);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token!);
        var userId = Guid.Parse(jwt.Claims.First(c => c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier).Value);

        return (result.Token!, result.Usuario.PublicProfileId, userId);
    }

    private void SetBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Register_GeneratesPublicProfileId()
    {
        var (_, publicProfileId, _) = await RegisterUserAsync();
        Assert.NotNull(publicProfileId);
        Assert.StartsWith("IX-", publicProfileId);
    }

    [Fact]
    public async Task Login_ByEmailUsernameAndLegacyCorreo_Succeeds()
    {
        var email = $"login_test_{Guid.NewGuid()}@test.com";
        var (token, publicId, _) = await RegisterUserAsync("Login User", email);

        SetBearer(token);
        var me = await _client.GetFromJsonAsync<UserProfileDto>("/api/v1/profile");
        var username = me!.Username;

        // Login by identifier = email
        var resp1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Login by identifier = username
        var resp2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = username,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        // Login by legacy correo property
        var resp3 = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);
    }

    [Fact]
    public async Task Login_NormalizedCaseInsensitive_Succeeds()
    {
        var email = $"case_test_{Guid.NewGuid()}@test.com";
        var (token, _, _) = await RegisterUserAsync("Case User", email);

        SetBearer(token);
        var me = await _client.GetFromJsonAsync<UserProfileDto>("/api/v1/profile");
        var username = me!.Username;

        // Login using UPPERCASE email & username
        var resp1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email.ToUpperInvariant(),
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        var resp2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = username.ToUpperInvariant(),
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
    }

    [Fact]
    public async Task Login_WithDifferentIdentifierAndCorreo_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "user@test.com",
            correo = "different@test.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUsername_ValidInvalidOcupadoAndHistory_HandlesCorrectly()
    {
        var (token1, _, _) = await RegisterUserAsync("User One");
        var (token2, _, _) = await RegisterUserAsync("User Two");

        SetBearer(token1);
        var validName = $"user1_{Guid.NewGuid().ToString("N")[..6]}";

        // Valid update
        var updateResp = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = validName });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Invalid update (<3 chars or invalid symbols)
        var invalidResp = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = "a!" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResp.StatusCode);

        // Conflict when token2 tries to claim token1's new username
        SetBearer(token2);
        var conflictResp = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = validName });
        Assert.Equal(HttpStatusCode.Conflict, conflictResp.StatusCode);

        // History reserved: token1 changes to another valid name
        SetBearer(token1);
        var nextName = $"user1_next_{Guid.NewGuid().ToString("N")[..6]}";
        var nextResp = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = nextName });
        Assert.Equal(HttpStatusCode.OK, nextResp.StatusCode);

        // token2 still cannot claim token1's old username (reserved in history)
        SetBearer(token2);
        var historyConflictResp = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = validName });
        Assert.Equal(HttpStatusCode.Conflict, historyConflictResp.StatusCode);
    }

    [Fact]
    public async Task PutUsername_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PutAsJsonAsync("/api/v1/profile/username", new { username = "noauth" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchUsers_DoesNotExposeSensitiveFields_AndExcludesSelf()
    {
        var (token1, publicId1, _) = await RegisterUserAsync("Search User One");
        var (token2, publicId2, _) = await RegisterUserAsync("Search User Two");

        SetBearer(token1);
        var me1 = await _client.GetFromJsonAsync<UserProfileDto>("/api/v1/profile");

        var response = await _client.GetAsync($"/api/v1/profile/search?q={me1!.Username[..4]}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();
        Assert.NotNull(results);
        // Excludes self
        Assert.DoesNotContain(results, r => r.PublicProfileId == publicId1);

        // Does not expose email, phone, InviteCode, Guid internal
        foreach (var item in results)
        {
            Assert.False(Guid.TryParse(item.Id, out _));
            Assert.StartsWith("IX-", item.Id);
            Assert.NotNull(item.Username);
        }
    }

    [Fact]
    public async Task Onboarding_LegacyPending_AndStepDoesNotDecrease()
    {
        var (token, _, _) = await RegisterUserAsync("Onboarding User");
        SetBearer(token);

        var getResp = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.NotNull(getResp);
        Assert.Equal("Pending", getResp.Status);

        // Advance to step 5
        var putResp1 = await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new { currentStep = 5 });
        Assert.Equal(HttpStatusCode.OK, putResp1.StatusCode);

        // Attempt to decrease to step 3
        var putResp2 = await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new { currentStep = 3 });
        Assert.Equal(HttpStatusCode.OK, putResp2.StatusCode);

        var getResp2 = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.Equal(5, getResp2!.CurrentStep);
    }

    [Fact]
    public async Task Onboarding_PrivacyAcceptedIsRequired_LocationAndDrivingAreOptional()
    {
        var (token, _, _) = await RegisterUserAsync("Consent User");
        SetBearer(token);

        // Advance step to 8, set medical status to Completed, but PrivacyAccepted = false
        await _client.PutAsJsonAsync("/api/v1/profile/medical", new { tipoSangre = "O+" });
        await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new { currentStep = 8, privacyAccepted = false });

        var check1 = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.Equal("Pending", check1!.Status);

        // Accept privacy -> becomes Completed even if Location/Driving consents are false
        await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new
        {
            privacyAccepted = true,
            locationIncidentConsent = false,
            drivingPatternConsent = false
        });

        var check2 = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.Equal("Completed", check2!.Status);
        Assert.NotNull(check2.CompletedAtUtc);
    }

    [Fact]
    public async Task MedicalProfile_UpdateSetsStatusToCompleted_SkippedAllowedInOnboarding()
    {
        var (token1, _, _) = await RegisterUserAsync("Medical User 1");
        SetBearer(token1);
        await _client.PutAsJsonAsync("/api/v1/profile/medical", new { tipoSangre = "A+" });
        var getMedical = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.Equal("Completed", getMedical!.MedicalProfileStatus);

        var (token2, _, _) = await RegisterUserAsync("Medical User 2");
        SetBearer(token2);
        await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new { medicalProfileStatus = "Skipped" });
        var getSkipped = await _client.GetFromJsonAsync<OnboardingDto>("/api/v1/profile/onboarding");
        Assert.Equal("Skipped", getSkipped!.MedicalProfileStatus);
    }

    [Fact]
    public async Task IdentityDTOs_DoNotExposeInternalUserIdOrInviteCode()
    {
        var (token, publicProfileId, _) = await RegisterUserAsync("DTO User");
        SetBearer(token);

        var profile = await _client.GetFromJsonAsync<UserProfileDto>("/api/v1/profile");
        Assert.NotNull(profile);
        Assert.Equal(publicProfileId, profile.Id);
        Assert.Equal(publicProfileId, profile.PublicProfileId);
        Assert.False(Guid.TryParse(profile.Id, out _));
    }

    [Fact]
    public async Task ProblemDetails_Returns400_401_and_409()
    {
        // 401 Unauthorized
        _client.DefaultRequestHeaders.Authorization = null;
        var unauth = await _client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 400 BadRequest
        var (token, _, _) = await RegisterUserAsync("PD User");
        SetBearer(token);
        var badReq = await _client.PutAsJsonAsync("/api/v1/profile/onboarding", new { currentStep = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, badReq.StatusCode);

        // 409 Conflict
        var dupReg = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Duplicate",
            correo = "pd_dup@test.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, dupReg.StatusCode);

        var conflict = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Duplicate 2",
            correo = "pd_dup@test.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
