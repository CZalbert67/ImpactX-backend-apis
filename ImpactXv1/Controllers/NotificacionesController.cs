using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificacionesController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var usuarioId = GetUsuarioId();
        var result = await _notificationService.GetNotificationsAsync(usuarioId);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var usuarioId = GetUsuarioId();
        var count = await _notificationService.GetUnreadCountAsync(usuarioId);
        return Ok(new { noLeidas = count });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> ToggleRead(Guid id, [FromBody] ToggleReadRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _notificationService.ToggleReadAsync(usuarioId, id, request);
        return Ok(new { mensaje = request.Leida ? "Notificación marcada como leída." : "Notificación marcada como no leída." });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var usuarioId = GetUsuarioId();
        await _notificationService.MarkAllAsReadAsync(usuarioId);
        return Ok(new { mensaje = "Todas las notificaciones marcadas como leídas." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _notificationService.DeleteAsync(usuarioId, id);
        return Ok(new { mensaje = "Notificación eliminada." });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        var usuarioId = GetUsuarioId();
        await _notificationService.DeleteAllAsync(usuarioId);
        return Ok(new { mensaje = "Todas las notificaciones eliminadas." });
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
