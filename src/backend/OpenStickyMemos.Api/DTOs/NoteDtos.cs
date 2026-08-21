using System.ComponentModel.DataAnnotations;

namespace OpenStickyMemos.Api.DTOs;

// ── Request ──

public class CreateNoteRequest
{
    [MaxLength(256)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    [MaxLength(16)]
    public string Color { get; set; } = "#FFE066";

    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 250;
    public double Height { get; set; } = 250;

    public bool IsPinned { get; set; }

    public int ZIndex { get; set; }

    public bool IsTaskList { get; set; }

    [MaxLength(256)]
    public string? Tags { get; set; }

    public DateTime? ReminderAt { get; set; }
}

public class UpdateNoteRequest
{
    [MaxLength(256)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    [MaxLength(16)]
    public string? Color { get; set; }

    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    public bool? IsPinned { get; set; }

    public int? ZIndex { get; set; }

    public bool? IsTaskList { get; set; }

    [MaxLength(256)]
    public string? Tags { get; set; }

    public DateTime? ReminderAt { get; set; }

    /// <summary>Si es true, limpia el recordatorio (ReminderAt se pone en null).</summary>
    public bool ClearReminder { get; set; }
}

public class UpdateNotePositionRequest
{
    public double PositionX { get; set; }
    public double PositionY { get; set; }
}

public class MoveNoteRequest
{
    [Required]
    public Guid TargetProjectId { get; set; }
}

public class BatchUpdateNotesRequest
{
    public List<Guid> NoteIds { get; set; } = new();
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public string? Color { get; set; }
    public bool? IsPinned { get; set; }
}

// ── Response ──

public class NoteResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string Color { get; set; } = "#FFE066";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsPinned { get; set; }
    public int ZIndex { get; set; }
    public bool IsArchived { get; set; }
    public bool PendingArchive { get; set; }
    public bool PendingDelete { get; set; }
    public bool IsOwner { get; set; }
    public bool IsTaskList { get; set; }
    public string? Tags { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Resultado de archivar/eliminar una nota (ejecutado o solicitado a aprobación).</summary>
public class NoteActionResponse
{
    public NoteResponse? Note { get; set; }
    /// <summary>La acción se ejecutó directamente (el owner la realizó).</summary>
    public bool Performed { get; set; }
    /// <summary>Se creó una solicitud pendiente que el owner debe aprobar.</summary>
    public bool RequestCreated { get; set; }
    /// <summary>La nota fue eliminada definitivamente.</summary>
    public bool Deleted { get; set; }
    public Guid? RequestId { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Solicitud de un miembro para archivar/eliminar una nota (pendiente de aprobación del owner).</summary>
public class NoteActionRequestResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid ProjectId { get; set; }
    public string Type { get; set; } = string.Empty; // "Archive" | "Delete"
    public string Status { get; set; } = string.Empty; // "Pending" | "Approved" | "Rejected" | "Cancelled"
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterAvatar { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteColor { get; set; } = "#FFE066";
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedById { get; set; }
}
