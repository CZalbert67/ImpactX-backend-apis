using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Core.Identity;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class RegistrationOnboardingV2ContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RegistrationOnboardingV2ContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private static object CompleteRegistration(
        string email,
        string username,
        string client = "web",
        bool termsAccepted = true,
        bool privacyAccepted = true,
        string password = "Password123!")
    {
        return new
        {
            registrationVersion = RegistrationContract.CurrentVersion,
            nombre = "Usuario Registro Completo",
            username,
            correo = email,
            telefono = "+52 773 123 4567",
            password,
            termsAccepted,
            privacyAccepted,
            locationIncidentConsent = false,
            drivingPatternConsent = false,
            client
        };
    }

    private static string UniqueUsername(string prefix = "user")
    {
        return $"{prefix}_{Guid.NewGuid():N}"[..20];
    }

    [Fact]
    public async Task RegistrationContract_IsPublicAndDescribesCurrentRequirements()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/v1/auth/registration-contract");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadFromJsonAsync<RegistrationContractDto>();
        Assert.NotNull(contract);
        Assert.Equal(RegistrationContract.CurrentVersion, contract.ContractVersion);
        Assert.Equal(RegistrationContract.TermsVersion, contract.TermsVersion);
        Assert.Equal(RegistrationContract.PrivacyNoticeVersion, contract.PrivacyNoticeVersion);
        Assert.Contains("username", contract.RequiredFields);
        Assert.Contains("telefono", contract.RequiredFields);
        Assert.Contains("termsAccepted", contract.RequiredFields);
        Assert.Contains("privacyAccepted", contract.RequiredFields);
        Assert.Equal(new[] { "web", "mobile" }, contract.SupportedClients);
        Assert.True(contract.ConfirmPasswordIsClientOnly);
    }

    [Fact]
    public async Task OpenApi_ContainsRegistrationContractAndLegalAcceptanceRoutes()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/registration-contract", out var contractPath));
        Assert.True(contractPath.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/register", out var registerPath));
        Assert.True(registerPath.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/profile/onboarding/legal-acceptance", out var legalPath));
        Assert.True(legalPath.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task CompleteRegistration_ValidPayload_PersistsIdentityAndLegalAcceptance()
    {
        using var client = NewClient();
        var username = UniqueUsername();
        var email = $"registration_v2_{Guid.NewGuid():N}@test.com";

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration(email, username));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.True(auth.Success);
        Assert.Equal(username, auth.Usuario!.Username);
        Assert.Equal("+52 773 123 4567", auth.Usuario.Telefono);
        Assert.NotNull(auth.Usuario.Onboarding);
        Assert.Equal(RegistrationContract.CurrentVersion, auth.Usuario.Onboarding.RegistrationContractVersion);
        Assert.Equal(3, auth.Usuario.Onboarding.CurrentStep);
        Assert.Equal("Pending", auth.Usuario.Onboarding.Status);
        Assert.True(auth.Usuario.Onboarding.TermsAccepted);
        Assert.True(auth.Usuario.Onboarding.PrivacyAccepted);
        Assert.Equal(RegistrationContract.TermsVersion, auth.Usuario.Onboarding.TermsVersion);
        Assert.Equal(RegistrationContract.PrivacyNoticeVersion, auth.Usuario.Onboarding.PrivacyNoticeVersion);
        Assert.NotNull(auth.Usuario.Onboarding.TermsAcceptedAtUtc);
        Assert.NotNull(auth.Usuario.Onboarding.PrivacyAcceptedAtUtc);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task CompleteRegistration_WithoutLegalAcceptance_Returns400(
        bool termsAccepted,
        bool privacyAccepted)
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration(
                $"registration_legal_{Guid.NewGuid():N}@test.com",
                UniqueUsername(),
                termsAccepted: termsAccepted,
                privacyAccepted: privacyAccepted));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_MissingUsernameAndPhone_Returns400()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            registrationVersion = RegistrationContract.CurrentVersion,
            nombre = "Usuario Incompleto",
            correo = $"registration_missing_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            termsAccepted = true,
            privacyAccepted = true,
            client = "web"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_WeakPassword_Returns400()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration(
                $"registration_password_{Guid.NewGuid():N}@test.com",
                UniqueUsername(),
                password: "password123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_WearableCannotCreateAccount()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration(
                $"registration_wearable_{Guid.NewGuid():N}@test.com",
                UniqueUsername(),
                client: "wearable"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_DuplicateOrReservedUsername_Returns409()
    {
        using var client = NewClient();
        var username = UniqueUsername("duplicate");

        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration($"registration_first_{Guid.NewGuid():N}@test.com", username));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration($"registration_second_{Guid.NewGuid():N}@test.com", username));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var reserved = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CompleteRegistration($"registration_reserved_{Guid.NewGuid():N}@test.com", "admin"));
        Assert.Equal(HttpStatusCode.Conflict, reserved.StatusCode);
    }

    [Fact]
    public async Task LegacyAccount_CanAcceptCurrentLegalContract_AndCannotRevokePrivacyFromOnboarding()
    {
        using var client = NewClient();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Usuario Legacy",
            correo = $"registration_legacy_{Guid.NewGuid():N}@test.com",
            password = "Password123!",
            client = "web"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token!);

        var accept = await client.PostAsJsonAsync(
            "/api/v1/profile/onboarding/legal-acceptance",
            new
            {
                contractVersion = RegistrationContract.CurrentVersion,
                termsAccepted = true,
                privacyAccepted = true
            });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var onboarding = await accept.Content.ReadFromJsonAsync<OnboardingDto>();
        Assert.Equal(RegistrationContract.CurrentVersion, onboarding!.RegistrationContractVersion);
        Assert.True(onboarding.TermsAccepted);
        Assert.True(onboarding.PrivacyAccepted);

        var revoke = await client.PutAsJsonAsync(
            "/api/v1/profile/onboarding",
            new { privacyAccepted = false });
        Assert.Equal(HttpStatusCode.BadRequest, revoke.StatusCode);

        var secondAccept = await client.PostAsJsonAsync(
            "/api/v1/profile/onboarding/legal-acceptance",
            new
            {
                contractVersion = RegistrationContract.CurrentVersion,
                termsAccepted = true,
                privacyAccepted = true
            });
        Assert.Equal(HttpStatusCode.OK, secondAccept.StatusCode);
    }
}
