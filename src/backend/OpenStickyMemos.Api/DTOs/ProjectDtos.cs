using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

// ── Request ──

public class CreateProjectRequest
{
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }
}

public class UpdateProjectRequest
{
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? Description { get; set; }
}

public class AddMemberRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

// ── Response ──

public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public int NoteCount { get; set; }
    public List<MemberInfo> Members { get; set; } = new();
}

public class MemberInfo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
