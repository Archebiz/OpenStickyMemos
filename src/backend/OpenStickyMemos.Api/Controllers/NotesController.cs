using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Services;

namespace OpenStickyMemos.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }

    /// <summary>
    /// GET /api/projects/{projectId}/notes?includeArchived=true — Lista las notas de un proyecto
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, [FromQuery] bool includeArchived = false)
    {
        var userId = GetUserId();
        var notes = await _noteService.GetProjectNotesAsync(projectId, userId, includeArchived);
        return Ok(notes);
    }

    /// <summary>
    /// GET /api/projects/{projectId}/notes/{noteId} — Obtiene una nota por ID
    /// </summary>
    [HttpGet("{noteId:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid noteId)
    {
        var userId = GetUserId();
        var note = await _noteService.GetByIdAsync(noteId, userId);

        if (note is null)
            return NotFound(new { error = "Nota no encontrada o no tienes acceso" });

        return Ok(note);
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes — Crea una nueva nota en el proyecto
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateNoteRequest request)
    {
        var userId = GetUserId();
        var note = await _noteService.CreateAsync(projectId, request, userId);

        if (note is null)
            return BadRequest(new { error = "No se pudo crear la nota. ¿Eres miembro del proyecto?" });

        return CreatedAtAction(nameof(GetById), new { projectId, noteId = note.Id }, note);
    }

    /// <summary>
    /// PUT /api/projects/{projectId}/notes/{noteId} — Actualiza una nota
    /// </summary>
    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid noteId, [FromBody] UpdateNoteRequest request)
    {
        var userId = GetUserId();
        var note = await _noteService.UpdateAsync(noteId, request, userId);

        if (note is null)
            return NotFound(new { error = "Nota no encontrada o no tienes acceso" });

        return Ok(note);
    }

    /// <summary>
    /// PATCH /api/projects/{projectId}/notes/{noteId}/position — Actualiza solo la posición
    /// </summary>
    [HttpPatch("{noteId:guid}/position")]
    public async Task<IActionResult> UpdatePosition(Guid projectId, Guid noteId, [FromBody] UpdateNotePositionRequest request)
    {
        var userId = GetUserId();
        var note = await _noteService.UpdatePositionAsync(noteId, request, userId);

        if (note is null)
            return NotFound(new { error = "Nota no encontrada o no tienes acceso" });

        return Ok(note);
    }

    /// <summary>
    /// DELETE /api/projects/{projectId}/notes/{noteId} — Elimina una nota.
    /// El owner la elimina directamente; un miembro crea una solicitud que el owner debe aprobar.
    /// </summary>
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid noteId)
    {
        var userId = GetUserId();
        var result = await _noteService.DeleteAsync(noteId, userId);

        if (result.Deleted)
            return NoContent();

        if (result.RequestCreated)
            return Accepted(result);

        return NotFound(new { error = result.Message });
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes/{noteId}/archive — Archiva una nota.
    /// El owner la archiva directamente; un miembro solicita aprobación al owner.
    /// </summary>
    [HttpPost("{noteId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid projectId, Guid noteId)
    {
        var userId = GetUserId();
        var result = await _noteService.ArchiveAsync(noteId, userId);

        if (result.Note is null && !result.RequestCreated)
            return NotFound(new { error = result.Message });

        return Ok(result);
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes/{noteId}/unarchive — Restaura una nota archivada.
    /// </summary>
    [HttpPost("{noteId:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid projectId, Guid noteId)
    {
        var userId = GetUserId();
        var result = await _noteService.UnarchiveAsync(noteId, userId);

        if (result.Note is null)
            return NotFound(new { error = result.Message });

        return Ok(result);
    }

    /// <summary>
    /// PATCH /api/projects/{projectId}/notes/{noteId}/move — Mueve una nota a otro proyecto
    /// (requiere ser miembro del proyecto origen y destino).
    /// </summary>
    [HttpPatch("{noteId:guid}/move")]
    public async Task<IActionResult> Move(Guid projectId, Guid noteId, [FromBody] MoveNoteRequest request)
    {
        var userId = GetUserId();
        var note = await _noteService.MoveAsync(noteId, request.TargetProjectId, userId);

        if (note is null)
            return BadRequest(new { error = "No se pudo mover la nota. Debes ser miembro de ambos proyectos." });

        return Ok(note);
    }

    /// <summary>
    /// GET /api/projects/{projectId}/notes/requests — Lista solicitudes de archivo/eliminación.
    /// El owner ve todas; un miembro ve solo las propias.
    /// </summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetActionRequests(Guid projectId)
    {
        var userId = GetUserId();
        var requests = await _noteService.GetActionRequestsAsync(projectId, userId);
        return Ok(requests);
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes/requests/{requestId}/approve — Aprobación del owner
    /// </summary>
    [HttpPost("requests/{requestId:guid}/approve")]
    public async Task<IActionResult> ApproveActionRequest(Guid projectId, Guid requestId)
    {
        var userId = GetUserId();
        var approved = await _noteService.ApproveActionRequestAsync(requestId, userId);

        if (!approved)
            return BadRequest(new { error = "No se pudo aprobar la solicitud" });

        return NoContent();
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes/requests/{requestId}/reject — Rechazo del owner
    /// </summary>
    [HttpPost("requests/{requestId:guid}/reject")]
    public async Task<IActionResult> RejectActionRequest(Guid projectId, Guid requestId)
    {
        var userId = GetUserId();
        var rejected = await _noteService.RejectActionRequestAsync(requestId, userId);

        if (!rejected)
            return BadRequest(new { error = "No se pudo rechazar la solicitud" });

        return NoContent();
    }

    /// <summary>
    /// POST /api/projects/{projectId}/notes/requests/{requestId}/cancel — Cancela la propia solicitud
    /// </summary>
    [HttpPost("requests/{requestId:guid}/cancel")]
    public async Task<IActionResult> CancelActionRequest(Guid projectId, Guid requestId)
    {
        var userId = GetUserId();
        var cancelled = await _noteService.CancelActionRequestAsync(requestId, userId);

        if (!cancelled)
            return BadRequest(new { error = "No se pudo cancelar la solicitud" });

        return NoContent();
    }

    // ── Helpers ──

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdClaim!);
    }
}
