using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? AvatarUrl { get; set; }

    [Required, MaxLength(32)]
    public string AuthProvider { get; set; } = string.Empty; // "Google" or "Microsoft"

    [Required, MaxLength(256)]
    public string ProviderId { get; set; } = string.Empty;   // External provider user ID

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Project> OwnedProjects { get; set; } = new List<Project>();
    public ICollection<ProjectMember> Memberships { get; set; } = new List<ProjectMember>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
