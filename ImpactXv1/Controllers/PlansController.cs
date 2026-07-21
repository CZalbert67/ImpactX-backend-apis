using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPlans()
    {
        var plans = await _planService.GetAllPlansAsync();
        return Ok(plans);
    }
}
