namespace OpenStickyMemos.Api.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? NoteId { get; set; }
    public string? NoteTitle { get; set; }
    public string Type { get; set; } = "Mention";
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadNotificationsCountResponse
{
    public int Count { get; set; }
}
