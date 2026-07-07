using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace OpenStickyMemos.Desktop.Services;

public interface ISignalRService
{
    bool IsConnected { get; }
    event Action<NoteResponse>? NoteCreated;
    event Action<NoteResponse>? NoteUpdated;
    event Action<string, string>? NoteDeleted; // noteId, projectId
    event Action<bool>? ConnectionStateChanged;
    Task StartAsync();
    Task JoinProjectAsync(string projectId);
    Task LeaveProjectAsync(string projectId);
    Task StopAsync();
}

public class SignalRService : ISignalRService
{
    private HubConnection? _connection;
    private readonly ISettingsService _settings;
    private readonly IAuthService _auth;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public event Action<NoteResponse>? NoteCreated;
    public event Action<NoteResponse>? NoteUpdated;
    public event Action<string, string>? NoteDeleted;
    public event Action<bool>? ConnectionStateChanged;

    private static readonly string LogPath = Desktop.App.LogPath;

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SignalR] {msg}\n"); } catch { }
    }

    public SignalRService(ISettingsService settings, IAuthService auth)
    {
        _settings = settings;
        _auth = auth;
        Log($"SignalRService creado. SignalRUrl configurada: {settings.Current.SignalRUrl}, ApiUrl: {settings.Current.ApiUrl}");
    }

    /// <summary>Deriva la URL de SignalR desde la URL base de la API</summary>
    private string GetSignalRUrl()
    {
        var configuredUrl = _settings.Current.SignalRUrl;
        var apiUrl = _settings.Current.ApiUrl?.TrimEnd('/') ?? "http://localhost:5000";

        // Si la URL configurada apunta a localhost pero la API apunta a otro lado,
        // auto-derivar SignalR desde la API URL
        if (configuredUrl?.Contains("localhost") == true && !apiUrl.Contains("localhost"))
        {
            return apiUrl + "/api/hubs/notes";
        }

        // Usar la URL configurada (por defecto o personalizada)
        return configuredUrl ?? apiUrl + "/api/hubs/notes";
    }

    public async Task StartAsync()
    {
        if (_connection is not null) return;

        var signalrUrl = GetSignalRUrl();
        Log($"Conectando SignalR a: {signalrUrl}");

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(signalrUrl), options =>
            {
                options.AccessTokenProvider = () =>
                    Task.FromResult(_auth.AccessToken) !;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _connection.On<NoteResponse>("NoteCreated", note =>
        {
            Log($"Evento recibido: NoteCreated (id={note.Id}, projectId={note.ProjectId})");
            NoteCreated?.Invoke(note);
        });

        _connection.On<NoteResponse>("NoteUpdated", note =>
        {
            Log($"Evento recibido: NoteUpdated (id={note.Id})");
            NoteUpdated?.Invoke(note);
        });

        _connection.On<string, string>("NoteDeleted", (noteId, projectId) =>
        {
            Log($"Evento recibido: NoteDeleted (id={noteId}, projectId={projectId})");
            NoteDeleted?.Invoke(noteId, projectId);
        });

        _connection.Reconnecting += _ =>
        {
            Log("SignalR: Reconnecting...");
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            Log("SignalR: Reconnected");
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            Log("SignalR: Connection closed");
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            Log("SignalR: Conectado exitosamente");
            ConnectionStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            Log($"SignalR: Error de conexión: {ex.GetType().Name}: {ex.Message}");
            ConnectionStateChanged?.Invoke(false);
        }
    }

    public async Task JoinProjectAsync(string projectId)
    {
        if (_connection is not null)
        {
            Log($"JoinProject: {projectId}");
            await _connection.InvokeAsync("JoinProject", projectId);
        }
    }

    public async Task LeaveProjectAsync(string projectId)
    {
        if (_connection is not null)
        {
            Log($"LeaveProject: {projectId}");
            await _connection.InvokeAsync("LeaveProject", projectId);
        }
    }

    public async Task StopAsync()
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
