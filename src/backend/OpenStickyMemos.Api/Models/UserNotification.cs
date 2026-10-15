using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public class UserNotification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public Guid AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public Guid? NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note? Note { get; set; }

    [Required, MaxLength(64)]
    public string Type { get; set; } = "Mention";

    [Required, MaxLength(512)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? NoteTitle { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
