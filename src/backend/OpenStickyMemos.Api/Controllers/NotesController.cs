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
    /// GET /api/projects/{projectId}/notes — Lista las notas de un proyecto
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId)
    {
        var userId = GetUserId();
        var notes = await _noteService.GetProjectNotesAsync(projectId, userId);
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
    /// DELETE /api/projects/{projectId}/notes/{noteId} — Elimina una nota
    /// </summary>
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid noteId)
    {
        var userId = GetUserId();
        var deleted = await _noteService.DeleteAsync(noteId, userId);

        if (!deleted)
            return NotFound(new { error = "Nota no encontrada o no tienes acceso" });

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
