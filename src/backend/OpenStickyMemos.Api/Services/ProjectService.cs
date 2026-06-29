using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface IProjectService
{
    Task<List<ProjectResponse>> GetUserProjectsAsync(Guid userId);
    Task<ProjectResponse?> GetByIdAsync(Guid projectId, Guid userId);
    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, Guid ownerId);
    Task<ProjectResponse?> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid userId);
    Task<bool> DeleteAsync(Guid projectId, Guid userId);
    Task<MemberInfo?> AddMemberAsync(Guid projectId, string email, Guid requesterId);
    Task<bool> RemoveMemberAsync(Guid projectId, Guid memberUserId, Guid requesterId);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId);
}

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;

    public ProjectService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProjectResponse>> GetUserProjectsAsync(Guid userId)
    {
        var projectIds = await _db.ProjectMembers
            .Where(pm => pm.UserId == userId)
            .Select(pm => pm.ProjectId)
            .ToListAsync();

        return await _db.Projects
            .Where(p => projectIds.Contains(p.Id))
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Notes)
            .Select(p => MapToResponse(p))
            .ToListAsync();
    }

    public async Task<ProjectResponse?> GetByIdAsync(Guid projectId, Guid userId)
    {
        if (!await IsMemberAsync(projectId, userId))
            return null;

        var project = await _db.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Notes)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        return project is null ? null : MapToResponse(project);
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, Guid ownerId)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = ownerId
        };

        _db.Projects.Add(project);

        // Owner is automatically a member
        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectRole.Owner
        });

        await _db.SaveChangesAsync();

        // Reload with includes
        return (await GetByIdAsync(project.Id, ownerId))!;
    }

    public async Task<ProjectResponse?> UpdateAsync(Guid projectId, UpdateProjectRequest request, Guid userId)
    {
        var project = await _db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project is null || project.OwnerId != userId)
            return null;

        project.Name = request.Name;
        project.Description = request.Description;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetByIdAsync(projectId, userId);
    }

    public async Task<bool> DeleteAsync(Guid projectId, Guid userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null || project.OwnerId != userId)
            return false;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<MemberInfo?> AddMemberAsync(Guid projectId, string email, Guid requesterId)
    {
        var project = await _db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project is null || project.OwnerId != requesterId)
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return null; // User not registered yet

        var alreadyMember = project.Members.Any(m => m.UserId == user.Id);
        if (alreadyMember)
            return null;

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            Role = ProjectRole.Member
        };

        _db.ProjectMembers.Add(member);
        await _db.SaveChangesAsync();

        return new MemberInfo
        {
            Id = member.Id,
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Role = "Member",
            JoinedAt = member.JoinedAt
        };
    }

    public async Task<bool> RemoveMemberAsync(Guid projectId, Guid memberUserId, Guid requesterId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null || project.OwnerId != requesterId)
            return false;

        if (memberUserId == project.OwnerId)
            return false; // Cannot remove the owner

        var member = await _db.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == memberUserId);

        if (member is null)
            return false;

        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsMemberAsync(Guid projectId, Guid userId)
    {
        return await _db.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
    }

    // ── Helpers ──

    private static ProjectResponse MapToResponse(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        OwnerId = p.OwnerId,
        OwnerName = p.Owner.DisplayName,
        OwnerAvatar = p.Owner.AvatarUrl,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        MemberCount = p.Members.Count,
        NoteCount = p.Notes.Count,
        Members = p.Members.Select(m => new MemberInfo
        {
            Id = m.Id,
            UserId = m.User.Id,
            Email = m.User.Email,
            DisplayName = m.User.DisplayName,
            AvatarUrl = m.User.AvatarUrl,
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt
        }).ToList()
    };
}
