using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Services;

namespace OpenStickyMemos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// GET /api/notifications — Obtiene las notificaciones del usuario autenticado
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 30)
    {
        var userId = GetUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, limit);
        return Ok(notifications);
    }

    /// <summary>
    /// GET /api/notifications/unread-count — Obtiene la cantidad de notificaciones no leídas
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new UnreadNotificationsCountResponse { Count = count });
    }

    /// <summary>
    /// PATCH /api/notifications/{id}/read — Marca una notificación como leída
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        var marked = await _notificationService.MarkAsReadAsync(id, userId);
        if (!marked) return NotFound(new { error = "Notificación no encontrada" });
        return NoContent();
    }

    /// <summary>
    /// POST /api/notifications/read-all — Marca todas las notificaciones como leídas
    /// </summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdClaim!);
    }
}
