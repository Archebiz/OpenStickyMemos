using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public class ProjectInvitation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Email opcional al que está restringida la invitación.
    /// Si es null, cualquier usuario logueado puede aceptarla.
    /// </summary>
    [MaxLength(256)]
    public string? InvitedEmail { get; set; }

    /// <summary>
    /// Token único que identifica la invitación en el link.
    /// </summary>
    [Required, MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public Guid CreatedById { get; set; }

    [ForeignKey(nameof(CreatedById))]
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public bool IsAccepted { get; set; }

    public Guid? AcceptedByUserId { get; set; }

    [ForeignKey(nameof(AcceptedByUserId))]
    public User? AcceptedByUser { get; set; }

    public DateTime? AcceptedAt { get; set; }
}
