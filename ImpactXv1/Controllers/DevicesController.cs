using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetDevices(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _deviceService.GetDevicesPagedAsync(usuarioId, pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpPut("fcm-token")]
    [EnableRateLimiting("fcm-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertFcmToken([FromBody] UpsertDeviceRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _deviceService.UpsertFcmTokenAsync(usuarioId, request);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _deviceService.DeleteDeviceAsync(usuarioId, id);
        return NoContent();
    }

    [HttpDelete("")]
    [HttpDelete("fcm-token")]
    [EnableRateLimiting("fcm-token")]
    public async Task<IActionResult> DeleteAllDevices()
    {
        var usuarioId = GetUsuarioId();
        await _deviceService.DeleteAllDevicesAsync(usuarioId);
        return NoContent();
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
