using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenStickyMemos.Desktop.Services;

public record ProjectResponse(
    string Id, string Name, string? Description,
    string OwnerId, string OwnerName, string? OwnerAvatar,
    DateTime CreatedAt, DateTime UpdatedAt,
    int MemberCount, int NoteCount,
    List<MemberInfo> Members
);

public record MemberInfo(
    string Id, string UserId, string Email,
    string DisplayName, string? AvatarUrl, string Role,
    DateTime JoinedAt
);

public record NoteResponse(
    string Id, string ProjectId,
    string AuthorId, string AuthorName, string? AuthorAvatar,
    string? Title, string? Content, string Color,
    double PositionX, double PositionY,
    double Width, double Height, bool IsPinned,
    DateTime CreatedAt, DateTime UpdatedAt
);

public interface IApiService
{
    void SetToken(string token);
    Task<List<ProjectResponse>> GetProjectsAsync();
    Task<ProjectResponse?> GetProjectAsync(string id);
    Task<ProjectResponse> CreateProjectAsync(string name, string? description);
    Task<ProjectResponse?> UpdateProjectAsync(string id, string name, string? description);
    Task<bool> DeleteProjectAsync(string id);
    Task<MemberInfo?> AddMemberAsync(string projectId, string email);
    Task<List<NoteResponse>> GetNotesAsync(string projectId);
    Task<NoteResponse?> CreateNoteAsync(string projectId, object request);
    Task<NoteResponse?> UpdateNoteAsync(string projectId, string noteId, object request);
    Task<bool> DeleteNoteAsync(string projectId, string noteId);
}

public class ApiService : IApiService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;

    public ApiService(ISettingsService settings)
    {
        _settings = settings;
        _http = new HttpClient { BaseAddress = new Uri(settings.Current.ApiUrl) };
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<ProjectResponse>> GetProjectsAsync()
    {
        return await _http.GetFromJsonAsync<List<ProjectResponse>>("/projects")
               ?? new List<ProjectResponse>();
    }

    public async Task<ProjectResponse?> GetProjectAsync(string id)
    {
        try { return await _http.GetFromJsonAsync<ProjectResponse>($"/projects/{id}"); }
        catch { return null; }
    }

    public async Task<ProjectResponse> CreateProjectAsync(string name, string? description)
    {
        var response = await _http.PostAsJsonAsync("/projects",
            new { name, description });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }

    public async Task<ProjectResponse?> UpdateProjectAsync(string id, string name, string? description)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"/projects/{id}",
                new { name, description });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectResponse>();
        }
        catch { return null; }
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        try
        {
            var response = await _http.DeleteAsync($"/projects/{id}");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<MemberInfo?> AddMemberAsync(string projectId, string email)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/projects/{projectId}/members", new { email });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MemberInfo>();
        }
        catch { return null; }
    }

    public async Task<List<NoteResponse>> GetNotesAsync(string projectId)
    {
        return await _http.GetFromJsonAsync<List<NoteResponse>>(
                   $"/projects/{projectId}/notes")
               ?? new List<NoteResponse>();
    }

    public async Task<NoteResponse?> CreateNoteAsync(string projectId, object request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"/projects/{projectId}/notes", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NoteResponse>();
        }
        catch { return null; }
    }

    public async Task<NoteResponse?> UpdateNoteAsync(string projectId, string noteId, object request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(
                $"/projects/{projectId}/notes/{noteId}", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NoteResponse>();
        }
        catch { return null; }
    }

    public async Task<bool> DeleteNoteAsync(string projectId, string noteId)
    {
        try
        {
            var response = await _http.DeleteAsync(
                $"/projects/{projectId}/notes/{noteId}");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
