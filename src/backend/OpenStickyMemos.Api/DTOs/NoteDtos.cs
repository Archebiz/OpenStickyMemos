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
}

public class UpdateNotePositionRequest
{
    public double PositionX { get; set; }
    public double PositionY { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
