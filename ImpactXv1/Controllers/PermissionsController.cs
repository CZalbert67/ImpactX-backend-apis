using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba1.Models.DTOs;
using Prueba1.Services;

namespace Prueba1.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var usuarioId = GetUsuarioId();
        var permissions = await _permissionService.GetPermissionsAsync(usuarioId);
        return Ok(permissions);
    }

    [HttpPut("mobile")]
    public async Task<IActionResult> UpdateMobilePermissions([FromBody] UpdatePermissionsRequest request)
    {
        var usuarioId = GetUsuarioId();
        var permissions = await _permissionService.UpdateMobilePermissionsAsync(usuarioId, request);
        return Ok(permissions);
    }

    [HttpPut("web")]
    public async Task<IActionResult> UpdateWebPermissions([FromBody] UpdatePermissionsRequest request)
    {
        var usuarioId = GetUsuarioId();
        var permissions = await _permissionService.UpdateWebPermissionsAsync(usuarioId, request);
        return Ok(permissions);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
