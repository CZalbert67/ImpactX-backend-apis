using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class PaginationContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaginationContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private async Task<string> RegisterAndGetTokenAsync(HttpClient? client = null)
    {
        var target = client ?? _client;
        var email = $"pagination_{Guid.NewGuid()}@test.com";
        var response = await target.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Pagination Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    private async Task CreateContactsAsync(HttpClient client, string token, int count)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        for (int i = 0; i < count; i++)
        {
            await client.PostAsJsonAsync("/api/contacts", new
            {
                nombre = $"Contacto {i}",
                telefono = $"555-{i:0000}",
            });
        }
    }

    [Fact]
    public async Task LegacyList_WithPageSize_ReturnsListBodyAndContinuationHeader()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        await CreateContactsAsync(client, token, 3);

        var first = await client.GetAsync("/api/contacts?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<List<ContactoDto>>();
        Assert.Equal(2, firstBody!.Count);

        Assert.True(first.Headers.Contains(PagedResultHttp.ContinuationHeader),
            "La primera página con más resultados debe exponer X-Continuation-Token.");
        var tokenHeader = first.Headers.GetValues(PagedResultHttp.ContinuationHeader).Single();
        Assert.NotEmpty(tokenHeader);

        var second = await client.GetAsync($"/api/contacts?pageSize=2&continuationToken={Uri.EscapeDataString(tokenHeader)}");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondBody = await second.Content.ReadFromJsonAsync<List<ContactoDto>>();
        Assert.Single(secondBody!);
        Assert.False(second.Headers.Contains(PagedResultHttp.ContinuationHeader),
            "La última página no debe exponer X-Continuation-Token.");
    }

    [Fact]
    public async Task LegacyList_DefaultPageSize_NoHeaderWhenSinglePage()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        await CreateContactsAsync(client, token, 1);

        var response = await client.GetAsync("/api/contacts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contacts = await response.Content.ReadFromJsonAsync<List<ContactoDto>>();
        Assert.Single(contacts!);
        Assert.False(response.Headers.Contains(PagedResultHttp.ContinuationHeader));
    }

    [Fact]
    public async Task LegacyList_JsonShape_IsPlainArrayWithoutPaginationFields()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        await CreateContactsAsync(client, token, 3);

        var response = await client.GetAsync("/api/contacts?pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType!.ToString());

        var raw = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", raw.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"items\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"pageSize\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"hasMoreResults\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"continuationToken\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task LegacyList_InvalidPageSize_ReturnsBadRequest(string query)
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/contacts?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("continuationToken=not-a-valid-token")]
    [InlineData("continuationToken=AAAAAAAAAAAAAAAAAAAAAA%3D%3D")]
    public async Task LegacyList_InvalidContinuationToken_ReturnsBadRequest(string query)
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/contacts?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NewPagedEndpoint_Trips_ReturnsPagedResultBody()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/trips?pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Items);
        Assert.Equal(10, body.PageSize);
    }

    [Fact]
    public async Task TripsTelemetry_OtherUsersTrip_ReturnsNotFound()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var start = await client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId = "WEAR-PAG-001",
            proposito = "Prueba paginación",
        });
        var viaje = await start.Content.ReadFromJsonAsync<ViajeDto>();
        var viajeId = viaje!.Id;

        var otroToken = await RegisterAndGetTokenAsync();
        var otroClient = NewClient();
        otroClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otroToken);

        var response = await otroClient.GetAsync($"/api/trips/{viajeId}/telemetry");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MonitorsList_PageSizeApplied()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/monitors?pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var monitors = await response.Content.ReadFromJsonAsync<List<MonitorDto>>();
        Assert.NotNull(monitors);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task ContinuationToken_WithCrLf_RejectedAndNotEchoed()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            $"/api/contacts?continuationToken={Uri.EscapeDataString("b2Zmc2V0OjEK\r\nX-Injected: 1")}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\r\n", body);
        Assert.DoesNotContain("X-Injected", body);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task ContinuationToken_ExceedingMaxLength_ReturnsBadRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var longToken = new string('A', 2049);
        var response = await client.GetAsync($"/api/contacts?continuationToken={longToken}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task InvalidContinuationToken_ReturnsProblemDetailsWithoutToken()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/contacts?continuationToken=YQ%3D%3D");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("YQ%3D%3D", body);
        Assert.DoesNotContain("YQ==", body);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task ValidContinuationToken_HeaderHasNoCrLf()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        await CreateContactsAsync(client, token, 3);

        var first = await client.GetAsync("/api/contacts?pageSize=2");
        var tokenHeader = first.Headers.GetValues(PagedResultHttp.ContinuationHeader).Single();

        Assert.DoesNotContain('\r', tokenHeader);
        Assert.DoesNotContain('\n', tokenHeader);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task ContinuationTokenFromAnotherUser_LeaksNoForeignData()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var clientA = NewClient();
        await CreateContactsAsync(clientA, tokenA, 3);
        var first = await clientA.GetAsync("/api/contacts?pageSize=2");
        var tokenHeader = first.Headers.GetValues(PagedResultHttp.ContinuationHeader).Single();

        var tokenB = await RegisterAndGetTokenAsync();
        var clientB = NewClient();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenB);

        var response = await clientB.GetAsync(
            $"/api/contacts?pageSize=2&continuationToken={Uri.EscapeDataString(tokenHeader)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contacts = await response.Content.ReadFromJsonAsync<List<ContactoDto>>();
        Assert.Empty(contacts!);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task TripsTelemetry_OtherUsersTrip_DoesNotRevealOwner()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var start = await client.PostAsJsonAsync("/api/trips/start", new
        {
            dispositivoId = "WEAR-PAG-SEC-001",
            proposito = "Prueba seguridad",
        });
        var viaje = await start.Content.ReadFromJsonAsync<ViajeDto>();

        var otroToken = await RegisterAndGetTokenAsync();
        var otroClient = NewClient();
        otroClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otroToken);

        var response = await otroClient.GetAsync($"/api/trips/{viaje!.Id}/telemetry");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("propietario", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otro usuario", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PagedResultDto
    {
        public List<object>? Items { get; set; }
        public string? ContinuationToken { get; set; }
        public bool HasMoreResults { get; set; }
        public int PageSize { get; set; }
    }
}
