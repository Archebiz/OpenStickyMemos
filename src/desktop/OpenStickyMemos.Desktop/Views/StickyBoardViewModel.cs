using CommunityToolkit.Mvvm.ComponentModel;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;

namespace OpenStickyMemos.Desktop.ViewModels;

/// <summary>
/// Representa una nota en el board (modelo ligero para la UI)
/// </summary>
public class NoteItem
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string Color { get; set; } = "#FFE066";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 180;
    public bool IsPinned { get; set; }
}

public class StickyBoardViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly INavigationService _navigation;
    private readonly ISignalRService _signalR;
    private readonly IAuthService _auth;

    private string _projectId = string.Empty;
    private readonly Dictionary<string, NoteItem> _notes = new();
    private readonly System.Timers.Timer _debounceTimer;
    private string? _pendingNoteId;
    private string? _pendingTitle;
    private string? _pendingContent;

    /// <summary>Eventos para la vista</summary>
    public event Action<NoteItem>? NoteAdded;
    public event Action<NoteItem>? NoteUpdated;
    public event Action<string>? NoteRemoved;

    public StickyBoardViewModel(
        IApiService api,
        INavigationService navigation,
        ISignalRService signalR,
        IAuthService auth)
    {
        _api = api;
        _navigation = navigation;
        _signalR = signalR;
        _auth = auth;
        _api.SetToken(auth.AccessToken ?? string.Empty);

        // Debounce timer (300ms)
        _debounceTimer = new System.Timers.Timer(300) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => FlushPendingContent();
    }

    public async Task LoadBoardAsync()
    {
        IsLoading = true;

        try
        {
            // Obtener projectId del parámetro de navegación
            var projectIdParam = _navigation.NavigationParameter as string;
            if (!string.IsNullOrEmpty(projectIdParam))
            {
                _projectId = projectIdParam;
            }
            else
            {
                // Fallback: primer proyecto disponible
                var projects = await _api.GetProjectsAsync();
                if (projects.Count == 0) return;
                _projectId = projects[0].Id;
            }

            // Cargar notas
            var notes = await _api.GetNotesAsync(_projectId);
            _notes.Clear();
            foreach (var n in notes)
            {
                var item = MapToItem(n);
                _notes[item.Id] = item;
                NoteAdded?.Invoke(item);
            }

            // Conectar SignalR y escuchar eventos
            await _signalR.StartAsync();
            await _signalR.JoinProjectAsync(_projectId);

            _signalR.NoteCreated += OnRemoteNoteCreated;
            _signalR.NoteUpdated += OnRemoteNoteUpdated;
            _signalR.NoteDeleted += OnRemoteNoteDeleted;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<NoteItem?> CreateNoteAsync(double positionX, double positionY, string color)
    {
        var request = new { positionX, positionY, color, width = 200, height = 180 };
        var note = await _api.CreateNoteAsync(_projectId, request);
        if (note is null) return null;

        var item = MapToItem(note);
        _notes[item.Id] = item;
        NoteAdded?.Invoke(item);
        return item;
    }

    public async Task DeleteNoteAsync(string noteId)
    {
        var deleted = await _api.DeleteNoteAsync(_projectId, noteId);
        if (deleted)
        {
            _notes.Remove(noteId);
            NoteRemoved?.Invoke(noteId);
        }
    }

    public void UpdateNotePosition(string noteId, double x, double y)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;

        note.PositionX = Math.Max(0, x);
        note.PositionY = Math.Max(0, y);

        // Auto-save with debounce
        _ = _api.UpdateNoteAsync(_projectId, noteId, new { positionX = note.PositionX, positionY = note.PositionY });
    }

    public void UpdateNoteContent(string noteId, string title, string content)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;

        note.Title = title;
        note.Content = content;

        // Debounce auto-save
        _pendingNoteId = noteId;
        _pendingTitle = title;
        _pendingContent = content;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void FlushPendingContent()
    {
        if (_pendingNoteId is null) return;
        _ = _api.UpdateNoteAsync(_projectId, _pendingNoteId, new
        {
            title = _pendingTitle,
            content = _pendingContent,
        });
        _pendingNoteId = null;
    }

    public void GoBack()
    {
        _signalR.LeaveProjectAsync(_projectId);
        _debounceTimer.Dispose();
        _navigation.GoBack();
    }

    // ── SignalR handlers ──

    private void OnRemoteNoteCreated(NoteResponse note)
    {
        if (note.ProjectId != _projectId) return;
        if (_notes.ContainsKey(note.Id)) return;

        var item = MapToItem(note);
        _notes[item.Id] = item;
        NoteAdded?.Invoke(item);
    }

    private void OnRemoteNoteUpdated(NoteResponse note)
    {
        if (note.ProjectId != _projectId) return;
        if (!_notes.TryGetValue(note.Id, out var item)) return;

        item.Title = note.Title;
        item.Content = note.Content;
        item.Color = note.Color;
        item.PositionX = note.PositionX;
        item.PositionY = note.PositionY;
        item.Width = note.Width;
        item.Height = note.Height;
        item.IsPinned = note.IsPinned;

        NoteUpdated?.Invoke(item);
    }

    private void OnRemoteNoteDeleted(string noteId, string projectId)
    {
        if (projectId != _projectId) return;
        _notes.Remove(noteId);
        NoteRemoved?.Invoke(noteId);
    }

    // ── Helpers ──

    private static NoteItem MapToItem(NoteResponse n) => new()
    {
        Id = n.Id,
        ProjectId = n.ProjectId,
        Title = n.Title,
        Content = n.Content,
        Color = n.Color,
        PositionX = n.PositionX,
        PositionY = n.PositionY,
        Width = n.Width,
        Height = n.Height,
        IsPinned = n.IsPinned,
    };
}
