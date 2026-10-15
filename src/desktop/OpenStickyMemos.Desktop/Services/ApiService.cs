using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenStickyMemos.Desktop.Services;

public class ProjectResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public int NoteCount { get; set; }
    public List<MemberInfo> Members { get; set; } = new();
}

public class MemberInfo
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class NoteResponse
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string Color { get; set; } = "#FFE066";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsPinned { get; set; }
    public int ZIndex { get; set; }
    public bool IsArchived { get; set; }
    public bool PendingArchive { get; set; }
    public bool PendingDelete { get; set; }
    public bool IsOwner { get; set; }
    public bool IsTaskList { get; set; }
    public string? Tags { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Resultado de archivar/eliminar una nota (ejecutado o solicitado a aprobación).</summary>
public class NoteActionResponse
{
    public NoteResponse? Note { get; set; }
    public bool Performed { get; set; }
    public bool RequestCreated { get; set; }
    public bool Deleted { get; set; }
    public string? RequestId { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Solicitud de un miembro para archivar/eliminar (pendiente de aprobación del owner).</summary>
public class NoteActionRequestResponse
{
    public string Id { get; set; } = string.Empty;
    public string NoteId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Archive" | "Delete"
    public string Status { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterAvatar { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteColor { get; set; } = "#FFE066";
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedById { get; set; }

    public string TypeDisplay => Type == "Delete" ? "eliminar" : "archivar";
}

// ── Invitaciones ──

public class InvitationResponse
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? InvitedEmail { get; set; }
    public string Token { get; set; } = string.Empty;
    public string InvitationLink { get; set; } = string.Empty;
    public string CreatedById { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public class InvitationPublicResponse
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectDescription { get; set; }
    public string? InvitedEmail { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public bool IsAccepted { get; set; }
}

public interface IApiService
{
    void SetToken(string token);
    Task<List<ProjectResponse>> GetProjectsAsync();
    Task<ProjectResponse?> GetProjectAsync(string id);
    Task<ProjectResponse> CreateProjectAsync(string name, string? description);
    Task<ProjectResponse?> UpdateProjectAsync(string id, string name, string? description);
    Task<bool> DeleteProjectAsync(string id);
    Task<MemberInfo?> AddMemberAsync(string projectId, string email);
    Task<bool> RemoveMemberAsync(string projectId, string memberUserId);
    Task<List<NoteResponse>> GetNotesAsync(string projectId, bool includeArchived = false);
    Task<NoteResponse?> CreateNoteAsync(string projectId, object request);
    Task<NoteResponse?> UpdateNoteAsync(string projectId, string noteId, object request);
    Task<NoteResponse?> UpdateNotePositionAsync(string projectId, string noteId, double x, double y);
    Task<NoteActionResponse?> DeleteNoteAsync(string projectId, string noteId);
    Task<NoteActionResponse?> ArchiveNoteAsync(string projectId, string noteId);
    Task<NoteActionResponse?> UnarchiveNoteAsync(string projectId, string noteId);
    Task<NoteResponse?> MoveNoteAsync(string sourceProjectId, string noteId, string targetProjectId);
    Task<List<NoteActionRequestResponse>> GetActionRequestsAsync(string projectId);
    Task<bool> ApproveActionRequestAsync(string projectId, string requestId);
    Task<bool> RejectActionRequestAsync(string projectId, string requestId);

    // ── Invitaciones ──
    Task<InvitationResponse?> CreateInvitationAsync(string projectId, object request);
    Task<List<InvitationResponse>> GetProjectInvitationsAsync(string projectId);
    Task<bool> RevokeInvitationAsync(string projectId, string invitationId);
    Task<InvitationPublicResponse?> GetInvitationPublicInfoAsync(string token);
}

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly IAuthService _auth;

    private static readonly string LogPath = Desktop.App.LogPath;

    public ApiService(ISettingsService settings, IAuthService auth)
    {
        _settings = settings;
        _auth = auth;
        _http = new HttpClient { BaseAddress = new Uri(settings.Current.ApiUrl) };

        // Auto-aplicar token si ya hay sesión activa
        ApplyToken();

        // Mantener token sincronizado cuando cambie (login, refresh, logout)
        _auth.AuthChanged += ApplyToken;

        Log($"ApiService iniciado. BaseAddress: {settings.Current.ApiUrl}, IsLoggedIn: {auth.IsLoggedIn}");
    }

    /// <summary>Configura el header Authorization desde IAuthService automáticamente</summary>
    private void ApplyToken()
    {
        var token = _auth.AccessToken;
        Log($"ApplyToken: _auth.AccessToken = {(token is not null ? $"{token[..Math.Min(token.Length, 30)]}... (len={token.Length})" : "NULL")}");
        Log($"ApplyToken: _auth.IsLoggedIn = {_auth.IsLoggedIn}");

        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            Log($"Token JWT configurado correctamente");
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
            Log("Token JWT removido (sin token disponible)");
        }
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        Log($"Token JWT configurado manualmente: {token[..Math.Min(token.Length, 20)]}...");
    }

    // ── Logging ──

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [ApiService] {msg}\n"); } catch { }
    }

    private static void LogError(string context, Exception ex)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [ApiService] [ERROR] {context}: {ex.GetType().Name}: {ex.Message}\n"); } catch { }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> sendRequest)
    {
        var response = await sendRequest();
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Log("401 Unauthorized recibido. Intentando RefreshTokenAsync()...");
            var refreshed = await _auth.RefreshTokenAsync();
            if (refreshed)
            {
                ApplyToken();
                Log("Token refrescado con éxito. Reintentando petición...");
                response = await sendRequest();
            }
        }
        return response;
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        Log($"GET {fullUrl}");
        try
        {
            var response = await SendWithRetryAsync(() => _http.GetAsync(url));
            var body = await response.Content.ReadAsStringAsync();
            Log($"GET {fullUrl} → {(int)response.StatusCode} {body[..Math.Min(body.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"GET {fullUrl}", ex);
            return null;
        }
    }

    private async Task<T?> PostAsync<T>(string url, object body) where T : class
    {
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        var json = JsonSerializer.Serialize(body);
        Log($"POST {fullUrl} {json}");
        try
        {
            var response = await SendWithRetryAsync(() => _http.PostAsJsonAsync(url, body));
            var respBody = await response.Content.ReadAsStringAsync();
            Log($"POST {fullUrl} → {(int)response.StatusCode} {respBody[..Math.Min(respBody.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<T>(respBody, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"POST {fullUrl}", ex);
            return null;
        }
    }

    private async Task<T?> PatchAsync<T>(string url, object body) where T : class
    {
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        var json = JsonSerializer.Serialize(body);
        Log($"PATCH {fullUrl} {json}");
        try
        {
            var response = await SendWithRetryAsync(() => _http.PatchAsJsonAsync(url, body));
            var respBody = await response.Content.ReadAsStringAsync();
            Log($"PATCH {fullUrl} → {(int)response.StatusCode} {respBody[..Math.Min(respBody.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<T>(respBody, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"PATCH {fullUrl}", ex);
            return null;
        }
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        Log($"DELETE {fullUrl}");
        try
        {
            var response = await SendWithRetryAsync(() => _http.DeleteAsync(url));
            Log($"DELETE {fullUrl} → {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogError($"DELETE {fullUrl}", ex);
            return false;
        }
    }

    // ── Projects ──

    public async Task<List<ProjectResponse>> GetProjectsAsync()
    {
        return await GetAsync<List<ProjectResponse>>("/api/Projects")
               ?? new List<ProjectResponse>();
    }

    public async Task<ProjectResponse?> GetProjectAsync(string id)
    {
        return await GetAsync<ProjectResponse>($"/api/Projects/{id}");
    }

    public async Task<ProjectResponse> CreateProjectAsync(string name, string? description)
    {
        var result = await PostAsync<ProjectResponse>("/api/Projects",
            new { name, description });
        return result!;
    }

    public async Task<ProjectResponse?> UpdateProjectAsync(string id, string name, string? description)
    {
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}/api/Projects/{id}";
        var json = JsonSerializer.Serialize(new { name, description });
        Log($"PUT {fullUrl} {json}");
        try
        {
            var response = await _http.PutAsJsonAsync($"/api/Projects/{id}",
                new { name, description });
            var respBody = await response.Content.ReadAsStringAsync();
            Log($"PUT {fullUrl} → {(int)response.StatusCode} {respBody[..Math.Min(respBody.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<ProjectResponse>(respBody, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"PUT {fullUrl}", ex);
            return null;
        }
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        return await DeleteAsync($"/api/Projects/{id}");
    }

    // ── Members ──

    public async Task<MemberInfo?> AddMemberAsync(string projectId, string email)
    {
        return await PostAsync<MemberInfo>(
            $"/api/Projects/{projectId}/members", new { email });
    }

    public async Task<bool> RemoveMemberAsync(string projectId, string memberUserId)
    {
        return await DeleteAsync($"/api/Projects/{projectId}/members/{memberUserId}");
    }

    // ── Notes ──

    public async Task<List<NoteResponse>> GetNotesAsync(string projectId, bool includeArchived = false)
    {
        var url = $"/api/Projects/{projectId}/notes";
        if (includeArchived)
            url += "?includeArchived=true";
        return await GetAsync<List<NoteResponse>>(url)
               ?? new List<NoteResponse>();
    }

    public async Task<NoteResponse?> CreateNoteAsync(string projectId, object request)
    {
        return await PostAsync<NoteResponse>(
            $"/api/Projects/{projectId}/notes", request);
    }

    public async Task<NoteResponse?> UpdateNoteAsync(string projectId, string noteId, object request)
    {
        var url = $"/api/Projects/{projectId}/notes/{noteId}";
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        var json = JsonSerializer.Serialize(request);
        Log($"PUT {fullUrl} {json}");
        try
        {
            var response = await SendWithRetryAsync(() => _http.PutAsJsonAsync(url, request));
            var respBody = await response.Content.ReadAsStringAsync();
            Log($"PUT {fullUrl} → {(int)response.StatusCode} {respBody[..Math.Min(respBody.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<NoteResponse>(respBody, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"PUT {fullUrl}", ex);
            return null;
        }
    }

    public async Task<NoteResponse?> UpdateNotePositionAsync(string projectId, string noteId, double x, double y)
    {
        return await PatchAsync<NoteResponse>(
            $"/api/Projects/{projectId}/notes/{noteId}/position",
            new { positionX = x, positionY = y });
    }

    public async Task<NoteActionResponse?> DeleteNoteAsync(string projectId, string noteId)
    {
        var url = $"/api/Projects/{projectId}/notes/{noteId}";
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        Log($"DELETE {fullUrl}");
        try
        {
            var response = await _http.DeleteAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Log($"DELETE {fullUrl} → {(int)response.StatusCode} {body[..Math.Min(body.Length, 500)]}");

            // 204: eliminada directamente (owner). 202: solicitud creada (miembro).
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return new NoteActionResponse { Deleted = true, Performed = true, Message = "Nota eliminada" };

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted && !string.IsNullOrEmpty(body))
                return JsonSerializer.Deserialize<NoteActionResponse>(body, JsonOptions);

            return null;
        }
        catch (Exception ex)
        {
            LogError($"DELETE {fullUrl}", ex);
            return null;
        }
    }

    public async Task<NoteActionResponse?> ArchiveNoteAsync(string projectId, string noteId)
    {
        return await PostAsync<NoteActionResponse>(
            $"/api/Projects/{projectId}/notes/{noteId}/archive", new { });
    }

    public async Task<NoteActionResponse?> UnarchiveNoteAsync(string projectId, string noteId)
    {
        return await PostAsync<NoteActionResponse>(
            $"/api/Projects/{projectId}/notes/{noteId}/unarchive", new { });
    }

    public async Task<NoteResponse?> MoveNoteAsync(string sourceProjectId, string noteId, string targetProjectId)
    {
        var url = $"/api/Projects/{sourceProjectId}/notes/{noteId}/move";
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        var json = JsonSerializer.Serialize(new { targetProjectId });
        Log($"PATCH {fullUrl} {json}");
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(new { targetProjectId })
            };
            var response = await _http.SendAsync(request);
            var respBody = await response.Content.ReadAsStringAsync();
            Log($"PATCH {fullUrl} → {(int)response.StatusCode} {respBody[..Math.Min(respBody.Length, 500)]}");
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<NoteResponse>(respBody, JsonOptions);
        }
        catch (Exception ex)
        {
            LogError($"PATCH {fullUrl}", ex);
            return null;
        }
    }

    public async Task<List<NoteActionRequestResponse>> GetActionRequestsAsync(string projectId)
    {
        return await GetAsync<List<NoteActionRequestResponse>>(
                   $"/api/Projects/{projectId}/notes/requests")
               ?? new List<NoteActionRequestResponse>();
    }

    public async Task<bool> ApproveActionRequestAsync(string projectId, string requestId)
    {
        var url = $"/api/Projects/{projectId}/notes/requests/{requestId}/approve";
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        Log($"POST {fullUrl}");
        try
        {
            var response = await _http.PostAsync(url, null);
            Log($"POST {fullUrl} → {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogError($"POST {fullUrl}", ex);
            return false;
        }
    }

    public async Task<bool> RejectActionRequestAsync(string projectId, string requestId)
    {
        var url = $"/api/Projects/{projectId}/notes/requests/{requestId}/reject";
        var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{url}";
        Log($"POST {fullUrl}");
        try
        {
            var response = await _http.PostAsync(url, null);
            Log($"POST {fullUrl} → {(int)response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogError($"POST {fullUrl}", ex);
            return false;
        }
    }

    // ── Invitaciones ──

    public async Task<InvitationResponse?> CreateInvitationAsync(string projectId, object request)
    {
        return await PostAsync<InvitationResponse>($"/api/Projects/{projectId}/invitations", request);
    }

    public async Task<List<InvitationResponse>> GetProjectInvitationsAsync(string projectId)
    {
        return await GetAsync<List<InvitationResponse>>($"/api/Projects/{projectId}/invitations")
               ?? new List<InvitationResponse>();
    }

    public async Task<bool> RevokeInvitationAsync(string projectId, string invitationId)
    {
        return await DeleteAsync($"/api/Projects/{projectId}/invitations/{invitationId}");
    }

    public async Task<InvitationPublicResponse?> GetInvitationPublicInfoAsync(string token)
    {
        return await GetAsync<InvitationPublicResponse>($"/api/Invitations/{token}");
    }
}
