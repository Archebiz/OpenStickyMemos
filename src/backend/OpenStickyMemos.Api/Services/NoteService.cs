using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Hubs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface INoteService
{
    Task<List<NoteResponse>> GetProjectNotesAsync(Guid projectId, Guid userId, bool includeArchived = false);
    Task<NoteResponse?> GetByIdAsync(Guid noteId, Guid userId);
    Task<NoteResponse?> CreateAsync(Guid projectId, CreateNoteRequest request, Guid authorId);
    Task<NoteResponse?> UpdateAsync(Guid noteId, UpdateNoteRequest request, Guid userId);
    Task<NoteActionResponse> DeleteAsync(Guid noteId, Guid userId);
    Task<NoteResponse?> UpdatePositionAsync(Guid noteId, UpdateNotePositionRequest request, Guid userId);
    Task<NoteActionResponse> ArchiveAsync(Guid noteId, Guid userId);
    Task<NoteActionResponse> UnarchiveAsync(Guid noteId, Guid userId);
    Task<NoteResponse?> MoveAsync(Guid noteId, Guid targetProjectId, Guid userId);
    Task<List<NoteActionRequestResponse>> GetActionRequestsAsync(Guid projectId, Guid userId);
    Task<bool> ApproveActionRequestAsync(Guid requestId, Guid userId);
    Task<bool> RejectActionRequestAsync(Guid requestId, Guid userId);
    Task<bool> CancelActionRequestAsync(Guid requestId, Guid userId);
}

public class NoteService : INoteService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;
    private readonly IHubContext<NotesHub> _hubContext;

    public NoteService(AppDbContext db, IProjectService projectService, IHubContext<NotesHub> hubContext)
    {
        _db = db;
        _projectService = projectService;
        _hubContext = hubContext;
    }

    public async Task<List<NoteResponse>> GetProjectNotesAsync(Guid projectId, Guid userId, bool includeArchived = false)
    {
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project is null || !await _projectService.IsMemberAsync(projectId, userId))
            return new List<NoteResponse>();

        var isOwner = project.OwnerId == userId;

        var query = _db.Notes.Where(n => n.ProjectId == projectId);
        if (!includeArchived)
            query = query.Where(n => !n.IsArchived);

        return await query
            .Include(n => n.Author)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.IsArchived)
            .ThenByDescending(n => n.UpdatedAt)
            .Select(n => MapToResponse(n, isOwner))
            .ToListAsync();
    }

    public async Task<NoteResponse?> GetByIdAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note is null)
            return null;

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return null;

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == note.ProjectId);

        return MapToResponse(note, project?.OwnerId == userId);
    }

    public async Task<NoteResponse?> CreateAsync(Guid projectId, CreateNoteRequest request, Guid authorId)
    {
        if (!await _projectService.IsMemberAsync(projectId, authorId))
            return null;

        var note = new Note
        {
            ProjectId = projectId,
            AuthorId = authorId,
            Title = request.Title,
            Content = request.Content,
            Color = request.Color,
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            Width = request.Width,
            Height = request.Height,
            IsPinned = request.IsPinned,
            ZIndex = request.ZIndex,
            IsTaskList = request.IsTaskList,
            Tags = request.Tags,
            ReminderAt = request.ReminderAt
        };

        _db.Notes.Add(note);

        // Update project timestamp
        var project = await _db.Projects.FindAsync(projectId);
        if (project is not null)
            project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Reload with author
        await _db.Entry(note).Reference(n => n.Author).LoadAsync();

        var response = MapToResponse(note, project?.OwnerId == authorId);

        // SignalR: notificar a todos los miembros del proyecto
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("NoteCreated", response);

        return response;
    }

    public async Task<NoteResponse?> UpdateAsync(Guid noteId, UpdateNoteRequest request, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note is null)
            return null;

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return null;

        if (request.Title is not null) note.Title = request.Title;
        if (request.Content is not null) note.Content = request.Content;
        if (request.Color is not null) note.Color = request.Color;
        if (request.PositionX.HasValue) note.PositionX = request.PositionX.Value;
        if (request.PositionY.HasValue) note.PositionY = request.PositionY.Value;
        if (request.Width.HasValue) note.Width = request.Width.Value;
        if (request.Height.HasValue) note.Height = request.Height.Value;
        if (request.IsPinned.HasValue) note.IsPinned = request.IsPinned.Value;
        if (request.ZIndex.HasValue) note.ZIndex = request.ZIndex.Value;
        if (request.IsTaskList.HasValue) note.IsTaskList = request.IsTaskList.Value;
        if (request.Tags is not null) note.Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim();
        if (request.ReminderAt.HasValue) note.ReminderAt = request.ReminderAt.Value;
        else if (request.ClearReminder) note.ReminderAt = null;

        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == note.ProjectId);
        var response = MapToResponse(note, project?.OwnerId == userId);

        // SignalR: notificar actualización a todos los miembros del proyecto
        await _hubContext.Clients.Group(note.ProjectId.ToString())
            .SendAsync("NoteUpdated", response);

        return response;
    }

    public async Task<NoteActionResponse> DeleteAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes.FindAsync(noteId);
        if (note is null)
            return new NoteActionResponse { Message = "Nota no encontrada" };

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return new NoteActionResponse { Message = "No tienes acceso a esta nota" };

        var projectId = note.ProjectId;
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);
        var isOwner = project?.OwnerId == userId;

        // Owner: elimina directamente
        if (isOwner)
        {
            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();

            await _hubContext.Clients.Group(projectId.ToString())
                .SendAsync("NoteDeleted", new { noteId, projectId });

            return new NoteActionResponse
            {
                Deleted = true,
                Performed = true,
                Message = "Nota eliminada"
            };
        }

        // Miembro: solicita aprobación del owner
        var existingPending = await _db.NoteActionRequests.AnyAsync(r =>
            r.NoteId == noteId && r.Type == NoteActionType.Delete && r.Status == NoteActionStatus.Pending);

        if (existingPending)
            return new NoteActionResponse { RequestCreated = true, Message = "Ya hay una solicitud de eliminación pendiente" };

        note.PendingDelete = true;
        note.UpdatedAt = DateTime.UtcNow;

        var request = new NoteActionRequest
        {
            NoteId = note.Id,
            ProjectId = projectId,
            RequesterId = userId,
            Type = NoteActionType.Delete,
            NoteTitle = note.Title
        };
        _db.NoteActionRequests.Add(request);
        await _db.SaveChangesAsync();

        await _db.Entry(note).Reference(n => n.Author).LoadAsync();
        var response = MapToResponse(note, false);
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("NoteUpdated", response);
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("ActionRequestCreated", MapRequestToResponse(await LoadRequestAsync(request.Id)));

        return new NoteActionResponse
        {
            Note = response,
            RequestCreated = true,
            RequestId = request.Id,
            Message = "Solicitud de eliminación enviada al owner"
        };
    }

    // ── Archivar / restaurar ──

    public async Task<NoteActionResponse> ArchiveAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes.FindAsync(noteId);
        if (note is null)
            return new NoteActionResponse { Message = "Nota no encontrada" };

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return new NoteActionResponse { Message = "No tienes acceso a esta nota" };

        var projectId = note.ProjectId;
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);
        var isOwner = project?.OwnerId == userId;

        // Owner: archiva directamente
        if (isOwner)
        {
            note.IsArchived = true;
            note.PendingArchive = false;
            note.PendingDelete = false;
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _db.Entry(note).Reference(n => n.Author).LoadAsync();
            var archivedResponse = MapToResponse(note, true);
            await _hubContext.Clients.Group(projectId.ToString())
                .SendAsync("NoteUpdated", archivedResponse);

            return new NoteActionResponse { Note = archivedResponse, Performed = true, Message = "Nota archivada" };
        }

        // Miembro: solicita aprobación
        var existingPending = await _db.NoteActionRequests.AnyAsync(r =>
            r.NoteId == noteId && r.Type == NoteActionType.Archive && r.Status == NoteActionStatus.Pending);

        if (existingPending)
            return new NoteActionResponse { RequestCreated = true, Message = "Ya hay una solicitud de archivo pendiente" };

        note.PendingArchive = true;
        note.UpdatedAt = DateTime.UtcNow;

        var request = new NoteActionRequest
        {
            NoteId = note.Id,
            ProjectId = projectId,
            RequesterId = userId,
            Type = NoteActionType.Archive,
            NoteTitle = note.Title
        };
        _db.NoteActionRequests.Add(request);
        await _db.SaveChangesAsync();

        await _db.Entry(note).Reference(n => n.Author).LoadAsync();
        var response = MapToResponse(note, false);
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("NoteUpdated", response);
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("ActionRequestCreated", MapRequestToResponse(await LoadRequestAsync(request.Id)));

        return new NoteActionResponse
        {
            Note = response,
            RequestCreated = true,
            RequestId = request.Id,
            Message = "Solicitud de archivo enviada al owner"
        };
    }

    public async Task<NoteActionResponse> UnarchiveAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes.FindAsync(noteId);
        if (note is null)
            return new NoteActionResponse { Message = "Nota no encontrada" };

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return new NoteActionResponse { Message = "No tienes acceso a esta nota" };

        var projectId = note.ProjectId;
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        note.IsArchived = false;
        note.PendingArchive = false;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _db.Entry(note).Reference(n => n.Author).LoadAsync();
        var response = MapToResponse(note, project?.OwnerId == userId);
        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("NoteUpdated", response);

        return new NoteActionResponse { Note = response, Performed = true, Message = "Nota restaurada" };
    }

    // ── Mover entre proyectos ──

    public async Task<NoteResponse?> MoveAsync(Guid noteId, Guid targetProjectId, Guid userId)
    {
        var note = await _db.Notes
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => n.Id == noteId);

        if (note is null)
            return null;

        var sourceProjectId = note.ProjectId;
        if (sourceProjectId == targetProjectId)
            return MapToResponse(note, await IsOwnerAsync(sourceProjectId, userId));

        // Debe ser miembro del proyecto origen y del destino
        if (!await _projectService.IsMemberAsync(sourceProjectId, userId))
            return null;
        if (!await _projectService.IsMemberAsync(targetProjectId, userId))
            return null;

        note.ProjectId = targetProjectId;
        note.PendingArchive = false;
        note.PendingDelete = false;
        note.UpdatedAt = DateTime.UtcNow;

        var sourceProject = await _db.Projects.FindAsync(sourceProjectId);
        var targetProject = await _db.Projects.FindAsync(targetProjectId);
        if (sourceProject is not null) sourceProject.UpdatedAt = DateTime.UtcNow;
        if (targetProject is not null) targetProject.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var isOwner = await IsOwnerAsync(targetProjectId, userId);
        var response = MapToResponse(note, isOwner);

        // SignalR: quitar del grupo origen y agregar al grupo destino
        await _hubContext.Clients.Group(sourceProjectId.ToString())
            .SendAsync("NoteDeleted", new { noteId, projectId = sourceProjectId });
        await _hubContext.Clients.Group(targetProjectId.ToString())
            .SendAsync("NoteCreated", response);

        return response;
    }

    // ── Solicitudes de aprobación ──

    public async Task<List<NoteActionRequestResponse>> GetActionRequestsAsync(Guid projectId, Guid userId)
    {
        if (!await _projectService.IsMemberAsync(projectId, userId))
            return new List<NoteActionRequestResponse>();

        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);
        var isOwner = project?.OwnerId == userId;

        var query = _db.NoteActionRequests
            .Include(r => r.Requester)
            .Include(r => r.Note)
            .Where(r => r.ProjectId == projectId);

        // El owner ve todas; los miembros solo las propias
        if (!isOwner)
            query = query.Where(r => r.RequesterId == userId);

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return list.Select(MapRequestToResponse).ToList();
    }

    public async Task<bool> ApproveActionRequestAsync(Guid requestId, Guid userId)
    {
        var request = await _db.NoteActionRequests
            .Include(r => r.Note)
            .Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null || request.Status != NoteActionStatus.Pending)
            return false;

        var project = await _db.Projects.FindAsync(request.ProjectId);
        if (project is null || project.OwnerId != userId)
            return false; // Solo el owner puede aprobar

        request.Status = NoteActionStatus.Approved;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedById = userId;

        var projectId = request.ProjectId;
        var note = request.Note;

        if (request.Type == NoteActionType.Delete && note is not null)
        {
            var noteId = note.Id;
            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();

            await _hubContext.Clients.Group(projectId.ToString())
                .SendAsync("NoteDeleted", new { noteId, projectId });
            await _hubContext.Clients.Group(projectId.ToString())
                .SendAsync("ActionRequestResolved", MapRequestToResponse(request));
            return true;
        }

        // Archive: marcar como archivada y limpiar pendientes
        if (note is not null)
        {
            note.IsArchived = true;
            note.PendingArchive = false;
            note.PendingDelete = false;
            note.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        if (note is not null)
        {
            await _db.Entry(note).Reference(n => n.Author).LoadAsync();
            await _hubContext.Clients.Group(projectId.ToString())
                .SendAsync("NoteUpdated", MapToResponse(note, true));
        }

        await _hubContext.Clients.Group(projectId.ToString())
            .SendAsync("ActionRequestResolved", MapRequestToResponse(request));
        return true;
    }

    public async Task<bool> RejectActionRequestAsync(Guid requestId, Guid userId)
    {
        var request = await _db.NoteActionRequests
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null || request.Status != NoteActionStatus.Pending)
            return false;

        var project = await _db.Projects.FindAsync(request.ProjectId);
        if (project is null || project.OwnerId != userId)
            return false; // Solo el owner puede rechazar

        request.Status = NoteActionStatus.Rejected;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedById = userId;

        if (request.Note is not null)
        {
            var note = request.Note;
            note.PendingArchive = false;
            note.PendingDelete = false;
            note.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        if (request.Note is not null)
        {
            await _db.Entry(request.Note).Reference(n => n.Author).LoadAsync();
            await _hubContext.Clients.Group(request.ProjectId.ToString())
                .SendAsync("NoteUpdated", MapToResponse(request.Note, true));
        }

        await _hubContext.Clients.Group(request.ProjectId.ToString())
            .SendAsync("ActionRequestResolved", MapRequestToResponse(request));
        return true;
    }

    public async Task<bool> CancelActionRequestAsync(Guid requestId, Guid userId)
    {
        var request = await _db.NoteActionRequests
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null || request.Status != NoteActionStatus.Pending)
            return false;
        if (request.RequesterId != userId)
            return false; // Solo el solicitante puede cancelar

        request.Status = NoteActionStatus.Cancelled;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedById = userId;

        if (request.Note is not null)
        {
            var note = request.Note;
            note.PendingArchive = false;
            note.PendingDelete = false;
            note.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        if (request.Note is not null)
        {
            await _db.Entry(request.Note).Reference(n => n.Author).LoadAsync();
            await _hubContext.Clients.Group(request.ProjectId.ToString())
                .SendAsync("NoteUpdated", MapToResponse(request.Note, false));
        }

        await _hubContext.Clients.Group(request.ProjectId.ToString())
            .SendAsync("ActionRequestResolved", MapRequestToResponse(request));
        return true;
    }

    public async Task<NoteResponse?> UpdatePositionAsync(Guid noteId, UpdateNotePositionRequest request, Guid userId)
    {
        return await UpdateAsync(noteId, new UpdateNoteRequest
        {
            PositionX = request.PositionX,
            PositionY = request.PositionY
        }, userId);
    }

    // ── Helpers ──

    private async Task<bool> IsOwnerAsync(Guid projectId, Guid userId)
    {
        return await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.OwnerId == userId)
            .FirstOrDefaultAsync();
    }

    private async Task<NoteActionRequest> LoadRequestAsync(Guid requestId)
    {
        return (await _db.NoteActionRequests
            .Include(r => r.Requester)
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == requestId))!;
    }

    private static NoteActionRequestResponse MapRequestToResponse(NoteActionRequest r) => new()
    {
        Id = r.Id,
        NoteId = r.NoteId,
        ProjectId = r.ProjectId,
        Type = r.Type.ToString(),
        Status = r.Status.ToString(),
        RequesterId = r.RequesterId,
        RequesterName = r.Requester?.DisplayName ?? string.Empty,
        RequesterAvatar = r.Requester?.AvatarUrl,
        NoteTitle = r.NoteTitle ?? string.Empty,
        NoteColor = r.Note?.Color ?? "#FFE066",
        CreatedAt = r.CreatedAt,
        DecidedAt = r.DecidedAt,
        DecidedById = r.DecidedById
    };

    private static NoteResponse MapToResponse(Note n, bool isOwner) => new()
    {
        Id = n.Id,
        ProjectId = n.ProjectId,
        AuthorId = n.AuthorId,
        AuthorName = n.Author.DisplayName,
        AuthorAvatar = n.Author.AvatarUrl,
        Title = n.Title,
        Content = n.Content,
        Color = n.Color,
        PositionX = n.PositionX,
        PositionY = n.PositionY,
        Width = n.Width,
        Height = n.Height,
        IsPinned = n.IsPinned,
        ZIndex = n.ZIndex,
        IsArchived = n.IsArchived,
        PendingArchive = n.PendingArchive,
        PendingDelete = n.PendingDelete,
        IsOwner = isOwner,
        IsTaskList = n.IsTaskList,
        Tags = n.Tags,
        ReminderAt = n.ReminderAt,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
