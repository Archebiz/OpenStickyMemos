using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public enum ProjectRole
{
    Owner,
    Member
}

public class ProjectMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public ProjectRole Role { get; set; } = ProjectRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
