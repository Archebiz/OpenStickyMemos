using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface INotificationService
{
    Task<List<NotificationResponse>> GetUserNotificationsAsync(Guid userId, int limit = 30);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
    Task ProcessMentionsAsync(Note note, string? previousContent, Guid authorId);
}
