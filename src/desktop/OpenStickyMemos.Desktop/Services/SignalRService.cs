using System;
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

    public SignalRService(ISettingsService settings, IAuthService auth)
    {
        _settings = settings;
        _auth = auth;
    }

    public async Task StartAsync()
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_settings.Current.SignalRUrl), options =>
            {
                options.AccessTokenProvider = () =>
                    Task.FromResult(_auth.AccessToken) !;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _connection.On<NoteResponse>("NoteCreated", note =>
            NoteCreated?.Invoke(note));

        _connection.On<NoteResponse>("NoteUpdated", note =>
            NoteUpdated?.Invoke(note));

        _connection.On<string, string>("NoteDeleted", (noteId, projectId) =>
            NoteDeleted?.Invoke(noteId, projectId));

        _connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            ConnectionStateChanged?.Invoke(true);
        }
        catch
        {
            ConnectionStateChanged?.Invoke(false);
        }
    }

    public async Task JoinProjectAsync(string projectId)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("JoinProject", projectId);
    }

    public async Task LeaveProjectAsync(string projectId)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("LeaveProject", projectId);
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
