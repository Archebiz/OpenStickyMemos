using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Hubs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface IInvitationService
{
    /// <summary>
    /// Crea una invitación y devuelve el link completo.
    /// </summary>
    Task<InvitationResponse?> CreateAsync(Guid projectId, CreateInvitationRequest request, Guid requesterId);

    /// <summary>
    /// Lista las invitaciones activas de un proyecto.
    /// </summary>
    Task<List<InvitationResponse>> GetProjectInvitationsAsync(Guid projectId, Guid requesterId);

    /// <summary>
    /// Obtiene info pública de una invitación por token (sin auth).
    /// </summary>
    Task<InvitationPublicResponse?> GetPublicInfoAsync(string token);

    /// <summary>
    /// Acepta una invitación. El usuario debe estar autenticado.
    /// </summary>
    Task<InvitationResponse?> AcceptAsync(string token, Guid userId);

    /// <summary>
    /// Revoca (elimina) una invitación pendiente.
    /// </summary>
    Task<bool> RevokeAsync(Guid invitationId, Guid projectId, Guid requesterId);
}

public class InvitationService : IInvitationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotesHub> _hubContext;
    private readonly IConfiguration _configuration;

    public InvitationService(AppDbContext db, IHubContext<NotesHub> hubContext, IConfiguration configuration)
    {
        _db = db;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    public async Task<InvitationResponse?> CreateAsync(Guid projectId, CreateInvitationRequest request, Guid requesterId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null || project.OwnerId != requesterId)
            return null; // Solo el owner puede invitar

        // Generar token seguro
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var invitation = new ProjectInvitation
        {
            ProjectId = projectId,
            InvitedEmail = request.InvitedEmail?.ToLowerInvariant(),
            Token = token,
            CreatedById = requesterId,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays)
        };

        _db.ProjectInvitations.Add(invitation);
        await _db.SaveChangesAsync();

        var createdBy = await _db.Users.FindAsync(requesterId);

        var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:4200";

        return new InvitationResponse
        {
            Id = invitation.Id,
            ProjectId = projectId,
            ProjectName = project.Name,
            InvitedEmail = invitation.InvitedEmail,
            Token = token,
            InvitationLink = $"{baseUrl.TrimEnd('/')}/invite/{token}",
            CreatedById = requesterId,
            CreatedByName = createdBy?.DisplayName ?? "",
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            IsAccepted = false
        };
    }

    public async Task<List<InvitationResponse>> GetProjectInvitationsAsync(Guid projectId, Guid requesterId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null || project.OwnerId != requesterId)
            return new List<InvitationResponse>();

        var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:4200";

        return await _db.ProjectInvitations
            .Where(i => i.ProjectId == projectId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationResponse
            {
                Id = i.Id,
                ProjectId = i.ProjectId,
                ProjectName = project.Name,
                InvitedEmail = i.InvitedEmail,
                Token = i.Token,
                InvitationLink = $"{baseUrl.TrimEnd('/')}/invite/{i.Token}",
                CreatedById = i.CreatedById,
                CreatedByName = i.CreatedBy.DisplayName,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                IsAccepted = i.IsAccepted,
                AcceptedByUserId = i.AcceptedByUserId,
                AcceptedAt = i.AcceptedAt
            })
            .ToListAsync();
    }

    public async Task<InvitationPublicResponse?> GetPublicInfoAsync(string token)
    {
        var invitation = await _db.ProjectInvitations
            .Include(i => i.Project)
            .Include(i => i.CreatedBy)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null)
            return null;

        return new InvitationPublicResponse
        {
            ProjectId = invitation.ProjectId,
            ProjectName = invitation.Project.Name,
            ProjectDescription = invitation.Project.Description,
            InvitedEmail = invitation.InvitedEmail,
            CreatedByName = invitation.CreatedBy.DisplayName,
            ExpiresAt = invitation.ExpiresAt,
            IsExpired = invitation.ExpiresAt < DateTime.UtcNow,
            IsAccepted = invitation.IsAccepted
        };
    }

    public async Task<InvitationResponse?> AcceptAsync(string token, Guid userId)
    {
        var invitation = await _db.ProjectInvitations
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null)
            return null;

        if (invitation.IsAccepted)
            return null; // Ya fue aceptada

        if (invitation.ExpiresAt < DateTime.UtcNow)
            return null; // Expirada

        // Si la invitación está restringida a un email, verificar
        if (!string.IsNullOrEmpty(invitation.InvitedEmail))
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null || user.Email != invitation.InvitedEmail)
                return null; // Email no coincide
        }

        // Verificar que el usuario no sea ya miembro
        var alreadyMember = await _db.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == invitation.ProjectId && pm.UserId == userId);

        if (alreadyMember)
            return null; // Ya es miembro

        // Agregar como miembro
        var member = new ProjectMember
        {
            ProjectId = invitation.ProjectId,
            UserId = userId,
            Role = ProjectRole.Member
        };

        _db.ProjectMembers.Add(member);

        // Marcar invitación como aceptada
        invitation.IsAccepted = true;
        invitation.AcceptedByUserId = userId;
        invitation.AcceptedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var userInfo = await _db.Users.FindAsync(userId);
        var project = invitation.Project;
        var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:4200";

        // SignalR: notificar a los miembros del proyecto
        var memberInfo = new MemberInfo
        {
            Id = member.Id,
            UserId = userId,
            Email = userInfo!.Email,
            DisplayName = userInfo.DisplayName,
            AvatarUrl = userInfo.AvatarUrl,
            Role = "Member",
            JoinedAt = member.JoinedAt
        };

        await _hubContext.Clients.Group(invitation.ProjectId.ToString())
            .SendAsync("MemberAdded", memberInfo);

        return new InvitationResponse
        {
            Id = invitation.Id,
            ProjectId = invitation.ProjectId,
            ProjectName = project.Name,
            InvitedEmail = invitation.InvitedEmail,
            Token = invitation.Token,
            InvitationLink = $"{baseUrl.TrimEnd('/')}/invite/{invitation.Token}",
            CreatedById = invitation.CreatedById,
            CreatedByName = invitation.CreatedBy.DisplayName,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            IsAccepted = true,
            AcceptedByUserId = userId,
            AcceptedAt = invitation.AcceptedAt
        };
    }

    public async Task<bool> RevokeAsync(Guid invitationId, Guid projectId, Guid requesterId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null || project.OwnerId != requesterId)
            return false;

        var invitation = await _db.ProjectInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.ProjectId == projectId);

        if (invitation is null || invitation.IsAccepted)
            return false;

        _db.ProjectInvitations.Remove(invitation);
        await _db.SaveChangesAsync();
        return true;
    }
}
