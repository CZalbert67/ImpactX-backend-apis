using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class ContactsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ContactsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"contact_{Guid.NewGuid()}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Contact Tester",
            correo = email,
            password = "Password123!"
        });
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task GetContacts_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/contacts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateContact_WithAuth_ReturnsCreated()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/contacts", new
        {
            nombre = "Juan Perez",
            telefono = "555-0101",
            parentesco = "Hermano",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<ContactoDto>();
        Assert.NotNull(contact);
        Assert.Equal("Juan Perez", contact!.Nombre);
    }

    [Fact]
    public async Task GetContacts_WithAuth_ReturnsList()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/contacts", new { nombre = "A", telefono = "555-0001" });
        await _client.PostAsJsonAsync("/api/contacts", new { nombre = "B", telefono = "555-0002" });

        var response = await _client.GetAsync("/api/contacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contacts = await response.Content.ReadFromJsonAsync<List<ContactoDto>>();
        Assert.Equal(2, contacts!.Count);
    }

    [Fact]
    public async Task MakePrimary_SetsContactAsPrimary()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/contacts", new
        {
            nombre = "Primary",
            telefono = "555-9999",
            esPrincipal = true,
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ContactoDto>();

        var response = await _client.PatchAsJsonAsync("/api/contacts/make-primary",
            new { contactoId = created!.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ContactoDto>();
        Assert.True(updated!.EsPrincipal);
    }

    [Fact]
    public async Task GetSyncData_ReturnsAllContacts()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/contacts", new { nombre = "Sync1", telefono = "555-1001" });

        var response = await _client.GetAsync("/api/contacts/sync");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sync = await response.Content.ReadFromJsonAsync<SyncContactosResponse>();
        Assert.NotNull(sync);
        Assert.Single(sync!.Contactos);
    }
}
