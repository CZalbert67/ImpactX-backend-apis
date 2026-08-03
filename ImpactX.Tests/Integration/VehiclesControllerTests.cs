using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Tests.Integration;

public class VehiclesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public VehiclesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVehicles_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetVehicleTypes_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/vehicles/types");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicleTypes_ReturnsFullCatalog()
    {
        var registration = await RegisterAsync();
        SetBearer(registration.Token);

        var response = await _client.GetAsync("/api/v1/vehicles/types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var catalog = await response.Content.ReadFromJsonAsync<VehicleTypeCatalogDto>();
        Assert.NotNull(catalog);
        Assert.Equal(
            new[] { "Automovil", "Suv", "Camioneta", "Van", "Camion", "Autobus", "Deportivo" },
            catalog!.TipoVehiculo);
        Assert.Equal(
            new[] { "Ciudad", "Carretera", "Mixto", "TodoTerreno", "Comercial" },
            catalog.UsoPrincipal);
    }

    [Fact]
    public async Task CrudFlow_FreePlan_CreatesReadsUpdatesAndDeletesVehicle()
    {
        var registration = await RegisterAsync();
        SetBearer(registration.Token);

        var create = await _client.PostAsJsonAsync("/api/v1/vehicles", ValidCreateBody());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(created);
        Assert.True(created!.EsPrincipal);
        Assert.Matches("^VEH-[A-Za-z0-9_-]{22}$", created.PublicVehicleId);

        var detail = await _client.GetAsync($"/api/v1/vehicles/{created.PublicVehicleId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var update = await _client.PutAsJsonAsync(
            $"/api/v1/vehicles/{created.PublicVehicleId}",
            new
            {
                tipoVehiculo = "Suv",
                marca = "Honda",
                modelo = "CR-V",
                ano = 2025,
                velocidadPromedio = 72,
                usoPrincipalVehiculo = "Mixto"
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.Equal("Honda", updated!.Marca);

        var delete = await _client.DeleteAsync($"/api/v1/vehicles/{created.PublicVehicleId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var missing = await _client.GetAsync($"/api/v1/vehicles/{created.PublicVehicleId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task FreePlan_SecondVehicle_ReturnsConflict()
    {
        var registration = await RegisterAsync();
        SetBearer(registration.Token);

        var first = await _client.PostAsJsonAsync("/api/v1/vehicles", ValidCreateBody());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/vehicles", ValidCreateBody("Nissan", "Versa"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ForeignVehicle_ReturnsSameNotFoundAsMissing()
    {
        var owner = await RegisterAsync();
        var other = await RegisterAsync();

        SetBearer(owner.Token);
        var create = await _client.PostAsJsonAsync("/api/v1/vehicles", ValidCreateBody());
        var vehicle = await create.Content.ReadFromJsonAsync<VehicleDto>();

        SetBearer(other.Token);
        var foreign = await _client.GetAsync($"/api/v1/vehicles/{vehicle!.PublicVehicleId}");
        var missing = await _client.GetAsync("/api/v1/vehicles/VEH-AAAAAAAAAAAAAAAAAAAAAA");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var foreignProblem = await ReadProblemDetailsAsync(foreign);
        var missingProblem = await ReadProblemDetailsAsync(missing);
        Assert.Equal(missingProblem.Type, foreignProblem.Type);
        Assert.Equal(missingProblem.Title, foreignProblem.Title);
        Assert.Equal(missingProblem.Status, foreignProblem.Status);
        Assert.Equal(missingProblem.Detail, foreignProblem.Detail);
    }

    [Fact]
    public async Task BasicPlan_PrimarySwitchAndDelete_ReassignsDeterministically()
    {
        var registration = await RegisterAsync();
        await SetPlanAsync(registration.UserId, "Basic");
        SetBearer(registration.Token);

        var first = await CreateVehicleAsync("Toyota", "Corolla");
        var second = await CreateVehicleAsync("Honda", "Civic");
        var third = await CreateVehicleAsync("Nissan", "Sentra");

        var switchPrimary = await _client.PatchAsync(
            $"/api/v1/vehicles/{second.PublicVehicleId}/primary",
            null);
        Assert.Equal(HttpStatusCode.NoContent, switchPrimary.StatusCode);

        var afterSwitch = await _client.GetFromJsonAsync<List<VehicleDto>>("/api/v1/vehicles");
        Assert.NotNull(afterSwitch);
        Assert.Single(afterSwitch, vehicle => vehicle.EsPrincipal);
        Assert.True(afterSwitch.Single(vehicle => vehicle.PublicVehicleId == second.PublicVehicleId).EsPrincipal);

        var delete = await _client.DeleteAsync($"/api/v1/vehicles/{second.PublicVehicleId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await _client.GetFromJsonAsync<List<VehicleDto>>("/api/v1/vehicles");
        Assert.NotNull(afterDelete);
        Assert.Equal(2, afterDelete.Count);
        Assert.Single(afterDelete, vehicle => vehicle.EsPrincipal);
        Assert.True(afterDelete.Single(vehicle => vehicle.PublicVehicleId == first.PublicVehicleId).EsPrincipal);
        Assert.DoesNotContain(afterDelete, vehicle => vehicle.PublicVehicleId == third.PublicVehicleId && vehicle.EsPrincipal);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Response_DoesNotExposeInternalGuidOrOwnerUserId()
    {
        var registration = await RegisterAsync();
        SetBearer(registration.Token);

        var response = await _client.PostAsJsonAsync("/api/v1/vehicles", ValidCreateBody());
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("ownerUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"\"id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publicVehicleId", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ProblemDetailsSnapshot> ReadProblemDetailsAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        return new ProblemDetailsSnapshot(
            root.TryGetProperty("type", out var type) ? type.GetString() : null,
            root.TryGetProperty("title", out var title) ? title.GetString() : null,
            root.TryGetProperty("status", out var status) ? status.GetInt32() : null,
            root.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
    }

    private sealed record ProblemDetailsSnapshot(
        string? Type,
        string? Title,
        int? Status,
        string? Detail);

    private async Task<VehicleDto> CreateVehicleAsync(string marca, string modelo)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/vehicles",
            ValidCreateBody(marca, modelo));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VehicleDto>())!;
    }

    private async Task<(string Token, Guid UserId)> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Vehicle Tester",
            correo = $"vehicle_{Guid.NewGuid():N}@test.com",
            password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result!.Token!);
        var claim = jwt.Claims.First(value => value.Type == "nameid"
            || value.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        return (result.Token!, Guid.Parse(claim.Value));
    }

    private async Task SetPlanAsync(Guid userId, string plan)
    {
        await _factory.ExecuteInDbContextAsync(async db =>
        {
            var user = await db.Usuarios.FirstAsync(value => value.Id == userId);
            user.PlanActivo = plan;
            await db.SaveChangesAsync();
        });
    }

    private void SetBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static object ValidCreateBody(string marca = "Toyota", string modelo = "Corolla")
    {
        return new
        {
            tipoVehiculo = "Automovil",
            marca,
            modelo,
            ano = 2024,
            velocidadPromedio = 65,
            usoPrincipalVehiculo = "Ciudad"
        };
    }
}
