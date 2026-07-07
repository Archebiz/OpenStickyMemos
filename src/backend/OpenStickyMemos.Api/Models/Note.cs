using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenStickyMemos.Api.Models;

public class Note
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required]
    public Guid AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

    [MaxLength(256)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    [MaxLength(16)]
    public string Color { get; set; } = "#FFE066"; // Default sticky yellow

    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 250;
    public double Height { get; set; } = 250;

    public bool IsPinned { get; set; }

    public int ZIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
