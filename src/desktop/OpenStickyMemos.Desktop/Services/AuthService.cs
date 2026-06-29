using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenStickyMemos.Desktop.Services;

public record AuthResponse(
    string AccessToken, string RefreshToken,
    DateTime ExpiresAt, UserInfo User
);

public record UserInfo(
    string Id, string Email, string DisplayName,
    string? AvatarUrl, string AuthProvider
);

public interface IAuthService
{
    string? AccessToken { get; }
    UserInfo? CurrentUser { get; }
    bool IsLoggedIn { get; }
    event Action? AuthChanged;
    Task<bool> LoginWithGoogleAsync(string idToken);
    Task<bool> LoginWithMicrosoftAsync(string idToken);
    Task<bool> RefreshTokenAsync();
    Task<bool> TryAutoLoginAsync();
    void Logout();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ICredentialService _credentials;

    public string? AccessToken { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);
    public event Action? AuthChanged;

    public AuthService(ISettingsService settings, ICredentialService credentials)
    {
        _settings = settings;
        _credentials = credentials;
        _http = new HttpClient { BaseAddress = new Uri(settings.Current.ApiUrl) };
    }

    public async Task<bool> LoginWithGoogleAsync(string idToken)
    {
        return await ExchangeToken(idToken, "Google");
    }

    public async Task<bool> LoginWithMicrosoftAsync(string idToken)
    {
        return await ExchangeToken(idToken, "Microsoft");
    }

    public async Task<bool> RefreshTokenAsync()
    {
        var refreshToken = _credentials.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        try
        {
            var response = await _http.PostAsJsonAsync("/auth/refresh",
                new { refreshToken });
            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return false;

            ApplyAuth(auth);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Logout()
    {
        AccessToken = null;
        CurrentUser = null;
        _credentials.Clear();
        AuthChanged?.Invoke();
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        var token = _credentials.GetAccessToken();
        if (string.IsNullOrEmpty(token)) return false;

        AccessToken = token;
        return await RefreshTokenAsync();
    }

    // ── Private ──

    private async Task<bool> ExchangeToken(string idToken, string provider)
    {
        try
        {
            var endpoint = provider == "Google" ? "google" : "microsoft";
            var response = await _http.PostAsJsonAsync($"/auth/{endpoint}",
                new { idToken, provider });

            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return false;

            ApplyAuth(auth);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyAuth(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        CurrentUser = auth.User;
        _credentials.Save(auth.AccessToken, auth.RefreshToken,
            JsonSerializer.Serialize(auth.User));
        AuthChanged?.Invoke();
    }
}
