using CommunityToolkit.Mvvm.ComponentModel;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;
using System.Collections.ObjectModel;

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
    public int ZIndex { get; set; }
    public bool IsArchived { get; set; }
    public bool PendingArchive { get; set; }
    public bool PendingDelete { get; set; }
    public bool IsOwner { get; set; }
    public bool IsTaskList { get; set; }
    public string? Tags { get; set; }
    public DateTime? ReminderAt { get; set; }
    public bool ReminderTriggered { get; set; }
    public string? AuthorName { get; set; }
}

public class StickyBoardViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly INavigationService _navigation;
    private readonly ISignalRService _signalR;
    private readonly IAuthService _auth;
    private readonly ISettingsService _settings;

    private string _projectId = string.Empty;
    private string _projectName = string.Empty;
    private string _projectDescription = string.Empty;
    private string _currentUserId = string.Empty;
    private string _projectOwnerId = string.Empty;
    private bool _isOwner;
    private bool _showArchived;
    private readonly Dictionary<string, NoteItem> _notes = new();

    public string ProjectName => _projectName;
    public string ProjectId => _projectId;
    public bool IsOwner => _isOwner;
    public bool ShowArchived
    {
        get => _showArchived;
        set { if (_showArchived != value) { _showArchived = value; OnPropertyChanged(); } }
    }

    private string _tagFilter = string.Empty;
    public string TagFilter
    {
        get => _tagFilter;
        set
        {
            if (_tagFilter != value)
            {
                _tagFilter = value;
                OnPropertyChanged();
                FilterChanged?.Invoke();
            }
        }
    }

    public ObservableCollection<ProjectResponse> Projects { get; } = new();
    public ObservableCollection<NoteActionRequestResponse> ActionRequests { get; } = new();

    private readonly System.Timers.Timer _debounceTimer;
    private readonly System.Timers.Timer _reminderTimer;
    private readonly Dictionary<string, (string Title, string Content)> _pendingContentUpdates = new();

    /// <summary>Eventos para la vista</summary>
    public event Action<NoteItem>? NoteAdded;
    public event Action<NoteItem>? NoteUpdated;
    public event Action<string>? NoteRemoved;
    public event Action? BoardCleared;
    public event Action? ActionRequestsChanged;
    public event Action<NoteItem>? ReminderDue;
    public event Action? FilterChanged;

    public StickyBoardViewModel(
        IApiService api,
        INavigationService navigation,
        ISignalRService signalR,
        IAuthService auth,
        ISettingsService settings)
    {
        _api = api;
        _navigation = navigation;
        _signalR = signalR;
        _auth = auth;
        _settings = settings;
        _api.SetToken(auth.AccessToken ?? string.Empty);

        // Debounce timer (300ms)
        _debounceTimer = new System.Timers.Timer(300) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => FlushPendingContent();

        // Timer de recordatorios (cada 15s revisa notas vencidas)
        _reminderTimer = new System.Timers.Timer(15000) { AutoReset = true };
        _reminderTimer.Elapsed += (_, _) => CheckReminders();
    }

    private void CheckReminders()
    {
        var now = DateTime.UtcNow;
        List<NoteItem>? due = null;
        foreach (var note in _notes.Values)
        {
            if (!note.ReminderTriggered && note.ReminderAt.HasValue && note.ReminderAt.Value <= now)
            {
                note.ReminderTriggered = true;
                due ??= new List<NoteItem>();
                due.Add(note);
            }
        }

        if (due is null) return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        foreach (var note in due)
        {
            if (dispatcher is null || dispatcher.CheckAccess())
                ReminderDue?.Invoke(note);
            else
                dispatcher.Invoke(() => ReminderDue?.Invoke(note));
        }
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

            // Obtener nombre del proyecto y owner
            var project = await _api.GetProjectAsync(_projectId);
            _projectName = project?.Name ?? "Sin nombre";
            _projectDescription = project?.Description ?? string.Empty;
            _projectOwnerId = project?.OwnerId ?? string.Empty;
            OnPropertyChanged(nameof(ProjectName));

            _currentUserId = _auth.CurrentUser?.Id ?? string.Empty;
            _isOwner = !string.IsNullOrEmpty(_currentUserId) && _currentUserId == _projectOwnerId;
            OnPropertyChanged(nameof(IsOwner));

            // Cargar todos los proyectos del usuario (para mover notas y renombrar)
            await LoadProjectsAsync();

            // Cargar notas
            await LoadNotesAsync();

            // Cargar solicitudes pendientes (el owner ve todas; un miembro las propias)
            await LoadActionRequestsAsync();

            // Conectar SignalR y escuchar eventos
            await _signalR.StartAsync();
            await _signalR.JoinProjectAsync(_projectId);

            _signalR.NoteCreated += OnRemoteNoteCreated;
            _signalR.NoteUpdated += OnRemoteNoteUpdated;
            _signalR.NoteDeleted += OnRemoteNoteDeleted;
            _signalR.ActionRequestCreated += OnActionRequestChanged;
            _signalR.ActionRequestResolved += OnActionRequestChanged;

            // Iniciar chequeo de recordatorios
            _reminderTimer.Start();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProjectsAsync()
    {
        var projects = await _api.GetProjectsAsync();
        Projects.Clear();
        foreach (var p in projects)
            Projects.Add(p);
    }

    public async Task LoadNotesAsync()
    {
        var notes = await _api.GetNotesAsync(_projectId, ShowArchived);
        _notes.Clear();
        BoardCleared?.Invoke();
        foreach (var n in notes)
        {
            var item = MapToItem(n);
            _notes[item.Id] = item;
            NoteAdded?.Invoke(item);
        }
    }

    public async Task LoadActionRequestsAsync()
    {
        ActionRequests.Clear();
        var requests = await _api.GetActionRequestsAsync(_projectId);
        foreach (var r in requests)
            ActionRequests.Add(r);
        ActionRequestsChanged?.Invoke();
    }

    public async Task<NoteItem?> CreateNoteAsync(double positionX, double positionY, string color)
    {
        var request = new { positionX, positionY, color, width = 200, height = 180 };
        var note = await _api.CreateNoteAsync(_projectId, request);
        if (note is null) return null;

        var item = MapToItem(note);

        // Evitar duplicado por race condition: si SignalR NoteCreated llegó antes
        // que la respuesta HTTP, la nota ya fue agregada al canvas. En ese caso
        // solo actualizamos los datos sin disparar NoteAdded (evita ghost note).
        if (_notes.ContainsKey(item.Id))
        {
            var existing = _notes[item.Id];
            existing.Title = item.Title;
            existing.Content = item.Content;
            existing.Color = item.Color;
            existing.PositionX = item.PositionX;
            existing.PositionY = item.PositionY;
            existing.Width = item.Width;
            existing.Height = item.Height;
            existing.IsPinned = item.IsPinned;
            existing.ZIndex = item.ZIndex;
            existing.IsArchived = item.IsArchived;
            existing.PendingArchive = item.PendingArchive;
            existing.PendingDelete = item.PendingDelete;
            existing.IsOwner = item.IsOwner;
            existing.IsTaskList = item.IsTaskList;
            existing.Tags = item.Tags;
            existing.ReminderAt = item.ReminderAt;
            existing.AuthorName = item.AuthorName;
            NoteUpdated?.Invoke(existing);
        }
        else
        {
            _notes[item.Id] = item;
            NoteAdded?.Invoke(item);
        }

        return item;
    }

    public async Task<NoteActionResponse?> DeleteNoteAsync(string noteId)
    {
        var result = await _api.DeleteNoteAsync(_projectId, noteId);
        if (result is not null)
        {
            if (result.Deleted)
            {
                _notes.Remove(noteId);
                NoteRemoved?.Invoke(noteId);
            }
            else if (result.RequestCreated && result.Note is not null)
            {
                // Miembro: la nota queda marcada como pendiente de aprobación del owner
                OnRemoteNoteUpdated(result.Note);
                await LoadActionRequestsAsync();
            }
        }
        return result;
    }

    public async Task<NoteActionResponse?> ArchiveNoteAsync(string noteId)
    {
        var result = await _api.ArchiveNoteAsync(_projectId, noteId);
        if (result is not null)
        {
            if (result.Note is not null)
                OnRemoteNoteUpdated(result.Note);
            if (result.RequestCreated)
                await LoadActionRequestsAsync();
        }
        return result;
    }

    public async Task<NoteActionResponse?> UnarchiveNoteAsync(string noteId)
    {
        var result = await _api.UnarchiveNoteAsync(_projectId, noteId);
        if (result?.Note is not null)
            OnRemoteNoteUpdated(result.Note);
        return result;
    }

    public async Task<NoteResponse?> MoveNoteToProjectAsync(string noteId, string targetProjectId)
    {
        if (string.IsNullOrEmpty(targetProjectId) || targetProjectId == _projectId)
            return null;

        var result = await _api.MoveNoteAsync(_projectId, noteId, targetProjectId);
        if (result is not null)
        {
            _notes.Remove(noteId);
            NoteRemoved?.Invoke(noteId);
        }
        return result;
    }

    public async Task<bool> ApproveActionRequestAsync(string requestId)
    {
        var ok = await _api.ApproveActionRequestAsync(_projectId, requestId);
        if (ok) await LoadActionRequestsAsync();
        return ok;
    }

    public async Task<bool> RejectActionRequestAsync(string requestId)
    {
        var ok = await _api.RejectActionRequestAsync(_projectId, requestId);
        if (ok) await LoadActionRequestsAsync();
        return ok;
    }

    public async Task<bool> RenameProjectAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var result = await _api.UpdateProjectAsync(_projectId, newName.Trim(), _projectDescription);
        if (result is not null)
        {
            _projectName = result.Name;
            OnPropertyChanged(nameof(ProjectName));
            return true;
        }
        return false;
    }

    public void UpdateNotePosition(string noteId, double x, double y)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;

        note.PositionX = Math.Max(0, x);
        note.PositionY = Math.Max(0, y);

        // Optimizado: llamada PATCH al endpoint granular de posición
        _ = _api.UpdateNotePositionAsync(_projectId, noteId, note.PositionX, note.PositionY);
    }

    public void UpdateNoteZIndex(string noteId, int zIndex)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;
        note.ZIndex = zIndex;
        _ = _api.UpdateNoteAsync(_projectId, noteId, new { zIndex });
    }

    public void UpdateNoteSize(string noteId, double width, double height)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;
        note.Width = width;
        note.Height = height;
        _ = _api.UpdateNoteAsync(_projectId, noteId, new { width, height });
    }

    public void UpdateNotePin(string noteId, bool isPinned)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;
        note.IsPinned = isPinned;
        _ = _api.UpdateNoteAsync(_projectId, noteId, new { isPinned });
        NoteUpdated?.Invoke(note);
    }

    public void UpdateNoteColor(string noteId, string color)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;
        note.Color = color;
        _ = _api.UpdateNoteAsync(_projectId, noteId, new { color });
        NoteUpdated?.Invoke(note);
    }

    public void UpdateNoteContent(string noteId, string title, string content)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;

        note.Title = title;
        note.Content = content;

        // Debounce auto-save por nota para evitar que se pisen entre sí
        lock (_pendingContentUpdates)
        {
            _pendingContentUpdates[noteId] = (title, content);
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>
    /// Actualiza atributos de la nota (modo tareas, etiquetas y recordatorio) y guarda.
    /// Si reminderAt es null, se limpia el recordatorio (clearReminder=true).
    /// </summary>
    public void UpdateNoteMetaAsync(string noteId, string? tags, DateTime? reminderAt, bool? isTaskList)
    {
        if (!_notes.TryGetValue(noteId, out var note)) return;

        note.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags;
        note.ReminderAt = reminderAt;
        note.ReminderTriggered = false;
        if (isTaskList.HasValue) note.IsTaskList = isTaskList.Value;

        var request = new Dictionary<string, object?>
        {
            ["tags"] = note.Tags,
            ["reminderAt"] = reminderAt,
            ["isTaskList"] = note.IsTaskList,
            ["clearReminder"] = reminderAt is null
        };
        _ = _api.UpdateNoteAsync(_projectId, noteId, request);
        NoteUpdated?.Invoke(note);
    }

    private void FlushPendingContent()
    {
        Dictionary<string, (string Title, string Content)> toFlush;
        lock (_pendingContentUpdates)
        {
            if (_pendingContentUpdates.Count == 0) return;
            toFlush = new Dictionary<string, (string Title, string Content)>(_pendingContentUpdates);
            _pendingContentUpdates.Clear();
        }

        foreach (var (noteId, data) in toFlush)
        {
            _ = _api.UpdateNoteAsync(_projectId, noteId, new
            {
                title = data.Title,
                content = data.Content,
            });
        }
    }

    public void GoBack()
    {
        _signalR.LeaveProjectAsync(_projectId);
        _signalR.NoteCreated -= OnRemoteNoteCreated;
        _signalR.NoteUpdated -= OnRemoteNoteUpdated;
        _signalR.NoteDeleted -= OnRemoteNoteDeleted;
        _signalR.ActionRequestCreated -= OnActionRequestChanged;
        _signalR.ActionRequestResolved -= OnActionRequestChanged;
        _reminderTimer.Stop();
        _reminderTimer.Dispose();
        _debounceTimer.Dispose();
        _navigation.GoBack();
    }

    // ── Miembros e Invitaciones ──

    public async Task<ProjectResponse?> GetProjectAsync()
    {
        return await _api.GetProjectAsync(_projectId);
    }

    public async Task<MemberInfo?> AddMemberAsync(string email)
    {
        return await _api.AddMemberAsync(_projectId, email);
    }

    public async Task<bool> RemoveMemberAsync(string memberUserId)
    {
        return await _api.RemoveMemberAsync(_projectId, memberUserId);
    }

    public async Task<InvitationResponse?> CreateInvitationAsync(object request)
    {
        var result = await _api.CreateInvitationAsync(_projectId, request);
        if (result is not null)
            FixInvitationLink(result);
        return result;
    }

    public async Task<List<InvitationResponse>> GetProjectInvitationsAsync()
    {
        var list = await _api.GetProjectInvitationsAsync(_projectId);
        foreach (var inv in list)
            FixInvitationLink(inv);
        return list;
    }

    public async Task<bool> RevokeInvitationAsync(string invitationId)
    {
        return await _api.RevokeInvitationAsync(_projectId, invitationId);
    }

    /// <summary>
    /// Corrige el InvitationLink usando la WebUrl configurada en el desktop,
    /// en lugar de la que devuelve el backend (que puede ser localhost).
    /// </summary>
    private void FixInvitationLink(InvitationResponse inv)
    {
        var webUrl = _settings.Current.WebUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(webUrl) && !string.IsNullOrEmpty(inv.Token))
        {
            inv.InvitationLink = $"{webUrl}/invite/{inv.Token}";
        }
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

        // Si la nota fue archivada y no estamos viendo el archivo, quitarla del board
        if (note.IsArchived && !ShowArchived)
        {
            if (_notes.Remove(note.Id))
                NoteRemoved?.Invoke(note.Id);
            return;
        }

        if (!_notes.TryGetValue(note.Id, out var item)) return;

        item.Title = note.Title;
        item.Content = note.Content;
        item.Color = note.Color;
        item.PositionX = note.PositionX;
        item.PositionY = note.PositionY;
        item.Width = note.Width;
        item.Height = note.Height;
        item.IsPinned = note.IsPinned;
        item.ZIndex = note.ZIndex;
        item.IsArchived = note.IsArchived;
        item.PendingArchive = note.PendingArchive;
        item.PendingDelete = note.PendingDelete;
        item.IsOwner = note.IsOwner;
        item.IsTaskList = note.IsTaskList;
        item.Tags = note.Tags;
        item.ReminderAt = note.ReminderAt;
        item.ReminderTriggered = false;

        NoteUpdated?.Invoke(item);
    }

    private void OnRemoteNoteDeleted(string noteId, string projectId)
    {
        if (projectId != _projectId) return;
        _notes.Remove(noteId);
        NoteRemoved?.Invoke(noteId);
    }

    private void OnActionRequestChanged(NoteActionRequestResponse request)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = LoadActionRequestsAsync();
        }
        else
        {
            dispatcher.InvokeAsync(async () => await LoadActionRequestsAsync());
        }
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
        ZIndex = n.ZIndex,
        IsArchived = n.IsArchived,
        PendingArchive = n.PendingArchive,
        PendingDelete = n.PendingDelete,
        IsOwner = n.IsOwner,
        IsTaskList = n.IsTaskList,
        Tags = n.Tags,
        ReminderAt = n.ReminderAt,
        AuthorName = n.AuthorName,
    };
}
