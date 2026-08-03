using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImpactX.Tests.Integration;

public class ApiContractV1Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiContractV1Tests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiV1Json_Returns200()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllOpenApiPaths_StartWithApiV1()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToList();
        Assert.NotEmpty(paths);

        foreach (var path in paths)
        {
            Assert.StartsWith("/api/v1/", path.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task OpenApiDocument_DoesNotContainLegacyPaths()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToList();

        foreach (var path in paths)
        {
            Assert.DoesNotContain("api/Auth", path.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("invite/{token}", path.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAuthRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/auth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsProfileRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/profile"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsMonitorRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/monitors"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/routes"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsTripsRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/trips"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAlertRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/alerts"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsIncidentRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/incidents"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsNotificationRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/notifications"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsDeviceRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/devices"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsPlansRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/plans"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsSubscriptionRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, p => p.Contains("/api/v1/subscriptions"));
    }

    [Fact]
    public async Task OpenApiDocument_DoesNotContainExpireSubscription()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.DoesNotContain(paths, p => p.Contains("/api/v1/subscriptions/expire"));
    }

    [Fact]
    public async Task OpenApiDocument_ContainsVehicleRoutes()
    {
        var paths = await GetOpenApiPathsAsync();
        Assert.Contains(paths, path => path == "/api/v1/vehicles");
        Assert.Contains(paths, path => path == "/api/v1/vehicles/{publicVehicleId}");
        Assert.Contains(paths, path => path == "/api/v1/vehicles/{publicVehicleId}/primary");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsSecurityScheme()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var components = doc.RootElement.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task AllOpenApiV1Paths_AreLowercase()
    {
        var paths = await GetOpenApiPathsAsync();
        foreach (var path in paths)
        {
            var segmentAfterV1 = path.Replace("/api/v1/", "");
            var withoutParams = System.Text.RegularExpressions.Regex.Replace(segmentAfterV1, @"\{[^}]+\}", "");
            Assert.False(withoutParams.Any(char.IsUpper), $"Path '{path}' contains uppercase");
        }
    }

    [Fact]
    public async Task OpenApiAuthPaths_AreLowercase()
    {
        var paths = await GetOpenApiPathsAsync();
        var authPaths = paths.Where(p => p.Contains("/api/v1/auth")).ToList();
        Assert.NotEmpty(authPaths);
        foreach (var path in authPaths)
        {
            var withoutParams = System.Text.RegularExpressions.Regex.Replace(path, @"\{[^}]+\}", "");
            Assert.False(withoutParams.Any(char.IsUpper), $"Auth path '{path}' contains uppercase");
        }
    }

    private static string GetPathMethod(string fullPath)
    {
        return fullPath.Contains("post") ? "post" : "get";
    }

    [Fact]
    public async Task OpenApi_BearerSecurity_OnProtectedOperations()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var protectedRoutes = new[]
        {
            ("/api/v1/profile", "get"),
            ("/api/v1/monitors", "get"),
            ("/api/v1/trips/active", "get")
        };

        foreach (var (route, method) in protectedRoutes)
        {
            var pathItem = paths.GetProperty(route);
            var operation = pathItem.GetProperty(method);
            Assert.True(operation.TryGetProperty("security", out var security),
                $"'{route}' {method} is missing 'security' requirement");
            var firstReq = security[0];
            Assert.True(firstReq.TryGetProperty("Bearer", out _),
                $"'{route}' {method} does not require Bearer");
        }
    }

    [Fact]
    public async Task OpenApi_AnonymousOperations_DoNotRequireBearer()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var anonymousRoutes = new[]
        {
            ("/api/v1/auth/register", "post"),
            ("/api/v1/auth/login", "post"),
            ("/api/v1/auth/recover-password", "post"),
            ("/api/v1/monitors/invite/details", "post")
        };

        foreach (var (route, method) in anonymousRoutes)
        {
            var pathItem = paths.GetProperty(route);
            var operation = pathItem.GetProperty(method);
            var hasSecurity = operation.TryGetProperty("security", out var security);
            if (hasSecurity && security.ValueKind == JsonValueKind.Array && security.GetArrayLength() > 0)
            {
                foreach (var req in security.EnumerateArray())
                {
                    Assert.False(req.TryGetProperty("Bearer", out _),
                        $"'{route}' {method} should NOT require Bearer but does");
                }
            }
        }
    }

    [Fact]
    public async Task OpenApiDocument_UsesCamelCase()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"securitySchemes\"", json);
        Assert.Contains("\"openapi\"", json);
    }

    [Fact]
    public async Task LegacyRoutes_StillRespond()
    {
        var response = await _client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyRoutes_ReturnDeprecationHeaders()
    {
        var response = await _client.GetAsync("/api/plans");
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.True(response.Headers.Contains("Warning"));
        Assert.True(response.Headers.Contains("Link"));

        var deprecation = response.Headers.GetValues("Deprecation").First();
        Assert.Equal("true", deprecation);

        var warning = response.Headers.GetValues("Warning").First();
        Assert.Contains("Deprecated API", warning);

        var link = response.Headers.GetValues("Link").First();
        Assert.Contains("successor-version", link);
    }

    [Fact]
    public async Task V1Routes_DoNotReturnDeprecationHeaders()
    {
        var response = await _client.GetAsync("/api/v1/plans");
        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Warning"));
        Assert.False(response.Headers.Contains("Link"));
    }

    [Fact]
    public async Task HealthEndpoint_DoesNotReturnDeprecationHeaders()
    {
        var response = await _client.GetAsync("/health");
        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Warning"));
        Assert.False(response.Headers.Contains("Link"));
    }

    [Fact]
    public async Task V1Auth_Register_Works()
    {
        var email = $"contract_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Contract Tester",
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task V1Auth_DuplicateRegister_Returns409ProblemDetails()
    {
        var email = $"dup_{Guid.NewGuid()}@test.com";
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Dup Tester",
            correo = email,
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Dup Tester",
            correo = email,
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);

        var body = await secondResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Contains("conflict", root.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));
        var hasDetail = root.TryGetProperty("detail", out var detail);
        if (hasDetail)
        {
            Assert.Null(detail.GetString());
        }
    }

    [Fact]
    public async Task LegacyAuth_DuplicateRegister_ReturnsConflictObject()
    {
        var email = $"legacydup_{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            nombre = "Legacy Dup Tester",
            correo = email,
            password = "Password123!"
        });

        var secondResponse = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            nombre = "Legacy Dup Tester",
            correo = email,
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.StartsWith("application/json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task V1Plans_ReturnsPlans()
    {
        var response = await _client.GetAsync("/api/v1/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyHealth_NoDeprecation()
    {
        var response = await _client.GetAsync("/health");
        Assert.False(response.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task OpenApi_ProblemDetailsResponseTypes_Include500()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoutes = new[]
        {
            "/api/v1/auth/register"
        };

        foreach (var route in testRoutes)
        {
            var pathItem = paths.GetProperty(route);
            foreach (var method in pathItem.EnumerateObject())
            {
                var operation = method.Value;
                var responses = operation.GetProperty("responses");
                Assert.True(responses.TryGetProperty("500", out _),
                    $"'{route}' {method.Name} is missing '500' response");
            }
        }
    }

    [Fact]
    public async Task OpenApi_ProtectedRoutes_Include401And403()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/profile";
        var pathItem = paths.GetProperty(testRoute);
        var operation = pathItem.GetProperty("get");
        var responses = operation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("401", out _),
            $"'{testRoute}' get is missing '401' response");
        Assert.True(responses.TryGetProperty("403", out _),
            $"'{testRoute}' get is missing '403' response");
    }

    [Fact]
    public async Task OpenApi_AnonymousRoutes_DoNotInclude401()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/auth/register";
        var pathItem = paths.GetProperty(testRoute);
        var operation = pathItem.GetProperty("post");
        var responses = operation.GetProperty("responses");

        var has401 = responses.TryGetProperty("401", out _);
        var has403 = responses.TryGetProperty("403", out _);

        Assert.False(has401, $"'{testRoute}' post should NOT include '401' response");
        Assert.False(has403, $"'{testRoute}' post should NOT include '403' response");
    }

    [Fact]
    public async Task OpenApi_FromBodyRoutes_Include400()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/auth/login";
        var pathItem = paths.GetProperty(testRoute);
        var operation = pathItem.GetProperty("post");
        var responses = operation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("400", out _),
            $"'{testRoute}' post is missing '400' response");
    }

    [Fact]
    public async Task OpenApi_RouteWithPathParameter_Includes404()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/contacts/{id}";
        var pathItem = paths.GetProperty(testRoute);
        var operation = pathItem.GetProperty("delete");
        var responses = operation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("404", out _),
            $"'{testRoute}' delete is missing '404' response");
    }

    [Fact]
    public async Task OpenApi_ProblemDetailsSchema_Exists()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hasComponents = root.TryGetProperty("components", out var components);
        Assert.True(hasComponents);

        var hasSchemas = components.TryGetProperty("schemas", out var schemas);
        Assert.True(hasSchemas);

        Assert.True(schemas.TryGetProperty("ProblemDetails", out var problemSchema));
        Assert.Equal("object", problemSchema.GetProperty("type").GetString());
        Assert.True(problemSchema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("type", out _));
        Assert.True(properties.TryGetProperty("title", out _));
        Assert.True(properties.TryGetProperty("status", out _));
        Assert.True(properties.TryGetProperty("detail", out _));
        Assert.True(properties.TryGetProperty("instance", out _));
    }

    [Fact]
    public async Task OpenApi_RegisterRoute_Includes409()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/auth/register";
        Assert.True(paths.TryGetProperty(testRoute, out var pathItem));
        Assert.True(pathItem.TryGetProperty("post", out var operation));

        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("409", out var conflictResponse));
    }

    [Fact]
    public async Task OpenApi_ConflictResponse_UsesProblemDetails()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var pathItem = paths.GetProperty("/api/v1/auth/register");
        var operation = pathItem.GetProperty("post");
        var responses = operation.GetProperty("responses");
        var conflictResponse = responses.GetProperty("409");
        var content = conflictResponse.GetProperty("content");
        var problemJson = content.GetProperty("application/problem+json");
        var schema = problemJson.GetProperty("schema");

        Assert.Equal("#/components/schemas/ProblemDetails", schema.GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task OpenApi_RouteWithoutConflict_DoesNotInclude409()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/plans";
        Assert.True(paths.TryGetProperty(testRoute, out var pathItem));
        Assert.True(pathItem.TryGetProperty("get", out var operation));

        var responses = operation.GetProperty("responses");
        var has409 = responses.TryGetProperty("409", out _);
        Assert.False(has409, $"'{testRoute}' get should NOT include '409' response");
    }

    [Fact]
    public async Task OpenApi_DevicesFcmToken_Includes409ProblemDetails()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/devices/fcm-token";
        Assert.True(paths.TryGetProperty(testRoute, out var pathItem));
        Assert.True(pathItem.TryGetProperty("put", out var operation));

        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("409", out var conflictResponse));
        var content = conflictResponse.GetProperty("content");
        var problemJson = content.GetProperty("application/problem+json");
        var schema = problemJson.GetProperty("schema");

        Assert.Equal("#/components/schemas/ProblemDetails", schema.GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task OpenApi_RateLimitedRoutes_Include429()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var testRoute = "/api/v1/auth/register";
        var pathItem = paths.GetProperty(testRoute);
        var operation = pathItem.GetProperty("post");
        var responses = operation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("429", out _),
            $"'{testRoute}' post is missing '429' response");
    }

    private async Task<List<string>> GetOpenApiPathsAsync()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("paths").EnumerateObject()
            .Select(p => p.Name)
            .ToList();
    }
}

public class ApiContractV1CorsTests : IClassFixture<ApiContractV1CorsTests.CorsTestFactory>
{
    private readonly HttpClient _client;

    public ApiContractV1CorsTests(CorsTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    public class CorsTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("UseCosmosDb", "false");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
            builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
            builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
            builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1000");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        }
    }

    [Fact]
    public async Task AllowedOrigin_ReturnsAccessControlAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task DisallowedOrigin_DoesNotReturnCorsHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Add("Origin", "https://evil.example.com");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_AllowedOrigin_Works()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/plans");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ActualResponse_ExposesPaginationHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/contacts");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var expose = response.Headers.GetValues("Access-Control-Expose-Headers").SingleOrDefault();
        Assert.NotNull(expose);
        Assert.Contains("X-Continuation-Token", expose, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Correlation-Id", expose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_NoOrigins_CorsClosed()
    {
        using var prodFactory = new ProductionCorsClosedFactory();
        var client = prodFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Add("Origin", "https://attacker.com");
        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public class ProductionCorsClosedFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("UseCosmosDb", "false");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
            builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1000");
            // Not setting Cors:AllowedOrigins — empty in Production
        }
    }
}

public class ApiContractV1SecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiContractV1SecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UnauthenticatedRequest_Returns401ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out var title));
        Assert.Equal("Unauthorized", title.GetString());
        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(401, status.GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotFoundWithAuth_Returns404ProblemDetails()
    {
        var (token, _) = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/contacts/00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out var title));
        Assert.Equal("Not Found", title.GetString());
        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(404, status.GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));
        Assert.False(root.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InternalServerError_DoesNotExposeInternals()
    {
        var response = await _client.GetAsync("/api/v1/trips/active");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("stackTrace", out _));
        Assert.False(root.TryGetProperty("exceptionType", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ValidRequest_IncludesCorrelationId()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");
        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.Equal("test-correlation-123", correlationId);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Request_WithoutCorrelationId_GetsOne()
    {
        var response = await _client.GetAsync("/api/v1/plans");
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    private async Task<(string Token, Guid UserId)> RegisterAndGetTokenAsync(string email = null!)
    {
        var emailActual = email ?? $"security_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "Security Tester",
            correo = emailActual,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result!.Token!);
        var userId = Guid.Parse(jwt.Claims.First(c => c.Type == "nameid" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        return (result.Token!, userId);
    }
}
