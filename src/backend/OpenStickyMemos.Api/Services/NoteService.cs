using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Data;
using OpenStickyMemos.Api.DTOs;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Services;

public interface INoteService
{
    Task<List<NoteResponse>> GetProjectNotesAsync(Guid projectId, Guid userId);
    Task<NoteResponse?> GetByIdAsync(Guid noteId, Guid userId);
    Task<NoteResponse?> CreateAsync(Guid projectId, CreateNoteRequest request, Guid authorId);
    Task<NoteResponse?> UpdateAsync(Guid noteId, UpdateNoteRequest request, Guid userId);
    Task<bool> DeleteAsync(Guid noteId, Guid userId);
    Task<NoteResponse?> UpdatePositionAsync(Guid noteId, UpdateNotePositionRequest request, Guid userId);
}

public class NoteService : INoteService
{
    private readonly AppDbContext _db;
    private readonly IProjectService _projectService;

    public NoteService(AppDbContext db, IProjectService projectService)
    {
        _db = db;
        _projectService = projectService;
    }

    public async Task<List<NoteResponse>> GetProjectNotesAsync(Guid projectId, Guid userId)
    {
        if (!await _projectService.IsMemberAsync(projectId, userId))
            return new List<NoteResponse>();

        return await _db.Notes
            .Where(n => n.ProjectId == projectId)
            .Include(n => n.Author)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .Select(n => MapToResponse(n))
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

        return MapToResponse(note);
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
            IsPinned = request.IsPinned
        };

        _db.Notes.Add(note);

        // Update project timestamp
        var project = await _db.Projects.FindAsync(projectId);
        if (project is not null)
            project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Reload with author
        await _db.Entry(note).Reference(n => n.Author).LoadAsync();

        return MapToResponse(note);
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

        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToResponse(note);
    }

    public async Task<bool> DeleteAsync(Guid noteId, Guid userId)
    {
        var note = await _db.Notes.FindAsync(noteId);
        if (note is null)
            return false;

        if (!await _projectService.IsMemberAsync(note.ProjectId, userId))
            return false;

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
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

    private static NoteResponse MapToResponse(Note n) => new()
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
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
