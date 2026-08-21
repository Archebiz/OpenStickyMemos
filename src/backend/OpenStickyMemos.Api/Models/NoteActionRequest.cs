using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public enum NoteActionType
{
    Archive,
    Delete
}

public enum NoteActionStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

/// <summary>
/// Solicitud de un miembro (no owner) para archivar o eliminar una nota.
/// El owner debe aprobarla o rechazarla para que la acción se ejecute.
/// </summary>
public class NoteActionRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note Note { get; set; } = null!;

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required]
    public Guid RequesterId { get; set; }

    [ForeignKey(nameof(RequesterId))]
    public User Requester { get; set; } = null!;

    public NoteActionType Type { get; set; }

    public NoteActionStatus Status { get; set; } = NoteActionStatus.Pending;

    /// <summary>Snapshot del título de la nota al momento de la solicitud (para mantener contexto).</summary>
    [MaxLength(256)]
    public string? NoteTitle { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    public Guid? DecidedById { get; set; }

    [ForeignKey(nameof(DecidedById))]
    public User? DecidedBy { get; set; }
}
