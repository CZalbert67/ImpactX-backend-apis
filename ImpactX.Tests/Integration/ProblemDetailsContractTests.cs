using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImpactX.Tests.Integration;

public class ProblemDetailsContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ErrorResponse_HasContentType_ApplicationProblemJson()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BadRequest_Returns400WithValidationType()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Test"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var doc = await ParseProblemDetails(response);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        var type = doc.RootElement.GetProperty("type").GetString();
        Assert.Contains("validation", type, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthorized_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_Returns403()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/v1/incidents/{Guid.NewGuid()}/map");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var doc = await ParseProblemDetails(response);
        Assert.Equal(403, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("forbidden", doc.RootElement.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotFound_Returns404()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(
            $"/api/v1/contacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var doc = await ParseProblemDetails(response);
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("not found", doc.RootElement.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Conflict_Returns409()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"conflict_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Conflict Tester",
            correo = email,
            password = "Password123!"
        });

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Conflict Tester",
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var doc = await ParseProblemDetails(response);
        Assert.Equal(409, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("conflict", doc.RootElement.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProblemDetails_HasTraceId()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        var doc = await ParseProblemDetails(response);
        Assert.True(doc.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    [Fact]
    public async Task ProblemDetails_HasCorrelationId()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        var doc = await ParseProblemDetails(response);
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out var corrId));
        Assert.False(string.IsNullOrWhiteSpace(corrId.GetString()));
    }

    [Fact]
    public async Task ProblemDetails_HasInstance()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        var doc = await ParseProblemDetails(response);
        Assert.True(doc.RootElement.TryGetProperty("instance", out var instance));
        Assert.Equal("/api/v1/profile", instance.GetString());
    }

    [Fact]
    public async Task Unauthorized_DoesNotContainStackTrace()
    {
        var response = await _client.GetAsync("/api/v1/trips/active");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_DoesNotExposeInternalMessage()
    {
        var response = await _client.GetAsync("/api/v1/trips/active");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthorized_HidesDetailedMessage()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        var doc = await ParseProblemDetails(response);
        var type = doc.RootElement.GetProperty("type").GetString();
        Assert.Contains("unauthorized", type, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidLogin_Returns401ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            correo = "nonexistent@test.com",
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var doc = await ParseProblemDetails(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("unauthorized", doc.RootElement.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidRecoverPassword_Returns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/recover-password", new
        {
            correo = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await ParseProblemDetails(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("validation", doc.RootElement.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidResetPassword_Returns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = "",
            newPassword = "123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await ParseProblemDetails(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("validation", doc.RootElement.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuccessResponse_NotTransformedToProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotEqual("application/problem+json", contentType);
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task ValidationProblemDetails_IncludesErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task ProblemDetailsUses_CamelCaseJson()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"traceId\"", body);
        Assert.Contains("\"correlationId\"", body);
        Assert.DoesNotContain("\"TraceId\"", body);
        Assert.DoesNotContain("\"CorrelationId\"", body);
    }

    private static async Task<JsonDocument> ParseProblemDetails(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private async Task<(string Token, Guid UserId)> RegisterAndGetTokenAsync()
    {
        var email = $"pd_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "PD Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (result!.Token!, result!.Usuario!.Id);
    }
}
