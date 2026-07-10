using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Services;

namespace OpenStickyMemos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IInvitationService _invitationService;

    public ProjectsController(IProjectService projectService, IInvitationService invitationService)
    {
        _projectService = projectService;
        _invitationService = invitationService;
    }

    /// <summary>
    /// GET /api/projects — Lista los proyectos del usuario autenticado
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var projects = await _projectService.GetUserProjectsAsync(userId);
        return Ok(projects);
    }

    /// <summary>
    /// GET /api/projects/{id} — Obtiene un proyecto por ID (solo miembros)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var project = await _projectService.GetByIdAsync(id, userId);

        if (project is null)
            return NotFound(new { error = "Proyecto no encontrado o no tienes acceso" });

        return Ok(project);
    }

    /// <summary>
    /// POST /api/projects — Crea un nuevo proyecto
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var userId = GetUserId();
        var project = await _projectService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>
    /// PUT /api/projects/{id} — Actualiza un proyecto (solo owner)
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var userId = GetUserId();
        var project = await _projectService.UpdateAsync(id, request, userId);

        if (project is null)
            return NotFound(new { error = "Proyecto no encontrado o no eres el propietario" });

        return Ok(project);
    }

    /// <summary>
    /// DELETE /api/projects/{id} — Elimina un proyecto (solo owner)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _projectService.DeleteAsync(id, userId);

        if (!deleted)
            return NotFound(new { error = "Proyecto no encontrado o no eres el propietario" });

        return NoContent();
    }

    /// <summary>
    /// POST /api/projects/{id}/members — Agrega un miembro por email (solo owner)
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var userId = GetUserId();
        var member = await _projectService.AddMemberAsync(id, request.Email, userId);

        if (member is null)
            return BadRequest(new { error = "No se pudo agregar el miembro. Verifica que el email esté registrado." });

        return Ok(member);
    }

    /// <summary>
    /// DELETE /api/projects/{id}/members/{memberId} — Remueve un miembro (solo owner)
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        var userId = GetUserId();
        var removed = await _projectService.RemoveMemberAsync(id, memberId, userId);

        if (!removed)
            return BadRequest(new { error = "No se pudo remover el miembro" });

        return NoContent();
    }

    /// <summary>
    /// POST /api/projects/{id}/invitations — Genera un link de invitación (solo owner)
    /// </summary>
    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> CreateInvitation(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var userId = GetUserId();
        var invitation = await _invitationService.CreateAsync(id, request, userId);

        if (invitation is null)
            return BadRequest(new { error = "No se pudo crear la invitación. Solo el propietario puede invitar." });

        return Ok(invitation);
    }

    /// <summary>
    /// GET /api/projects/{id}/invitations — Lista invitaciones activas de un proyecto (solo owner)
    /// </summary>
    [HttpGet("{id:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid id)
    {
        var userId = GetUserId();
        var invitations = await _invitationService.GetProjectInvitationsAsync(id, userId);
        return Ok(invitations);
    }

    /// <summary>
    /// DELETE /api/projects/{id}/invitations/{invitationId} — Revoca una invitación pendiente (solo owner)
    /// </summary>
    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId)
    {
        var userId = GetUserId();
        var revoked = await _invitationService.RevokeAsync(invitationId, id, userId);

        if (!revoked)
            return BadRequest(new { error = "No se pudo revocar la invitación" });

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
