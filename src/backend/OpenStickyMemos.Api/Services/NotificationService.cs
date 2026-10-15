using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Hubs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotesHub> _hubContext;

    public NotificationService(AppDbContext db, IHubContext<NotesHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task<List<NotificationResponse>> GetUserNotificationsAsync(Guid userId, int limit = 30)
    {
        var notifications = await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Author)
            .Include(n => n.Project)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            UserId = n.UserId,
            AuthorId = n.AuthorId,
            AuthorName = n.Author?.DisplayName ?? "Usuario",
            AuthorAvatar = n.Author?.AvatarUrl,
            ProjectId = n.ProjectId,
            ProjectName = n.Project?.Name ?? "Proyecto",
            NoteId = n.NoteId,
            NoteTitle = n.NoteTitle ?? (n.Note != null ? n.Note.Title : null),
            Type = n.Type,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null) return false;

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task ProcessMentionsAsync(Note note, string? previousContent, Guid authorId)
    {
        var currentText = $"{note.Title ?? ""} {note.Content ?? ""}".Trim();
        if (string.IsNullOrEmpty(currentText) || !currentText.Contains('@'))
            return;

        // Obtener proyecto, autor y miembros
        var project = await _db.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == note.ProjectId);

        if (project is null) return;

        var author = await _db.Users.FindAsync(authorId);
        var authorName = author?.DisplayName ?? "Alguien";

        // Miembros disponibles para mencionar (excluyendo al propio autor)
        var candidates = new List<(Guid UserId, string DisplayName, string Email)>();

        if (project.OwnerId != authorId && project.Owner is not null)
        {
            candidates.Add((project.OwnerId, project.Owner.DisplayName, project.Owner.Email));
        }

        foreach (var m in project.Members)
        {
            if (m.UserId != authorId && m.User is not null)
            {
                if (!candidates.Any(c => c.UserId == m.UserId))
                {
                    candidates.Add((m.UserId, m.User.DisplayName, m.User.Email));
                }
            }
        }

        var prev = previousContent ?? "";

        foreach (var member in candidates)
        {
            var isMentioned = false;

            // 1. Chequeo por email (ej: @usuario@dominio.com)
            if (!string.IsNullOrEmpty(member.Email) &&
                currentText.IndexOf($"@{member.Email}", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isMentioned = true;
            }

            // 2. Chequeo por DisplayName completo (ej: @Carlos Mendoza)
            if (!isMentioned && !string.IsNullOrWhiteSpace(member.DisplayName) &&
                currentText.IndexOf($"@{member.DisplayName}", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isMentioned = true;
            }

            // 3. Chequeo por primer nombre si tiene al menos 3 caracteres (ej: @Carlos)
            if (!isMentioned && !string.IsNullOrWhiteSpace(member.DisplayName))
            {
                var firstName = member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstName) && firstName.Length >= 3)
                {
                    var pattern = $@"(?i)@{Regex.Escape(firstName)}\b";
                    if (Regex.IsMatch(currentText, pattern))
                    {
                        isMentioned = true;
                    }
                }
            }

            if (!isMentioned) continue;

            // Verificar si ya estaba mencionado previamente para no duplicar en cada pulsación/edición menor
            var wasPreviouslyMentioned = false;
            if (!string.IsNullOrEmpty(prev))
            {
                if (!string.IsNullOrEmpty(member.Email) && prev.IndexOf($"@{member.Email}", StringComparison.OrdinalIgnoreCase) >= 0)
                    wasPreviouslyMentioned = true;
                else if (!string.IsNullOrWhiteSpace(member.DisplayName) && prev.IndexOf($"@{member.DisplayName}", StringComparison.OrdinalIgnoreCase) >= 0)
                    wasPreviouslyMentioned = true;
                else
                {
                    var firstName = member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstName) && firstName.Length >= 3 && Regex.IsMatch(prev, $@"(?i)@{Regex.Escape(firstName)}\b"))
                        wasPreviouslyMentioned = true;
                }
            }

            if (wasPreviouslyMentioned)
            {
                // Si ya estaba mencionado en la versión anterior del contenido, no volvemos a generar notificación
                continue;
            }

            // Prevenir notificaciones duplicadas en un intervalo de 5 minutos para la misma nota
            var recentNotification = await _db.Notifications
                .AnyAsync(n => n.UserId == member.UserId &&
                               n.NoteId == note.Id &&
                               n.CreatedAt > DateTime.UtcNow.AddMinutes(-5));

            if (recentNotification) continue;

            var titleStr = string.IsNullOrWhiteSpace(note.Title) ? "una nota" : $"'{note.Title}'";
            var notification = new UserNotification
            {
                UserId = member.UserId,
                AuthorId = authorId,
                ProjectId = note.ProjectId,
                NoteId = note.Id,
                NoteTitle = note.Title,
                Type = "Mention",
                Message = $"{authorName} te mencionó en {titleStr} ({project.Name})",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            var dto = new NotificationResponse
            {
                Id = notification.Id,
                UserId = notification.UserId,
                AuthorId = notification.AuthorId,
                AuthorName = authorName,
                AuthorAvatar = author?.AvatarUrl,
                ProjectId = notification.ProjectId,
                ProjectName = project.Name,
                NoteId = notification.NoteId,
                NoteTitle = notification.NoteTitle,
                Type = notification.Type,
                Message = notification.Message,
                IsRead = false,
                CreatedAt = notification.CreatedAt
            };

            // Enviar notificación instantánea a través de SignalR
            await _hubContext.Clients.Group($"user_{member.UserId}")
                .SendAsync("NotificationReceived", dto);
        }
    }
}
