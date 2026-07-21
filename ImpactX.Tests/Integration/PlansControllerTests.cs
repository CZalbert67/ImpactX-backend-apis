using System.Net;
using System.Net.Http.Json;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Integration;

public class PlansControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PlansControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllPlans_WithoutAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.NotEmpty(plans!);
    }

    [Fact]
    public async Task GetAllPlans_ReturnsSeededPlans()
    {
        var response = await _client.GetAsync("/api/plans");
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.Contains(plans!, p => p.Nombre == "Free");
        Assert.Contains(plans!, p => p.Nombre == "Basic");
        Assert.Contains(plans!, p => p.Nombre == "Premium");
    }
}
