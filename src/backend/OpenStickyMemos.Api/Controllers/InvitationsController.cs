using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Services;

namespace OpenStickyMemos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>
    /// GET /api/invitations/{token} — Obtiene info pública de una invitación (sin auth).
    /// Sirve para mostrar vista previa del proyecto al que invitan.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> GetPublicInfo(string token)
    {
        var info = await _invitationService.GetPublicInfoAsync(token);

        if (info is null)
            return NotFound(new { error = "Invitación no encontrada" });

        return Ok(info);
    }

    /// <summary>
    /// POST /api/invitations/{token}/accept — Acepta una invitación (requiere auth).
    /// </summary>
    [Authorize]
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        var userId = GetUserId();
        var result = await _invitationService.AcceptAsync(token, userId);

        if (result is null)
            return BadRequest(new { error = "No se pudo aceptar la invitación. Puede estar expirada, ya aceptada, o el email no coincide." });

        return Ok(result);
    }

    // ── Helpers ──

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdClaim!);
    }
}
