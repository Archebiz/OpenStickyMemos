using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenStickyMemos.Desktop.Services;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string AuthProvider { get; set; } = string.Empty;
}

public interface IAuthService
{
    string? AccessToken { get; }
    UserInfo? CurrentUser { get; }
    bool IsLoggedIn { get; }
    event Action? AuthChanged;
    Task<bool> LoginWithEmailAsync(string email, string password);
    Task<bool> RegisterWithEmailAsync(string email, string password, string? displayName);
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthService(ISettingsService settings, ICredentialService credentials)
    {
        _settings = settings;
        _credentials = credentials;
        _http = new HttpClient { BaseAddress = new Uri(settings.Current.ApiUrl) };
    }

    private static readonly string LogPath = Desktop.App.LogPath;
    private static void LogHttp(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [HTTP] {msg}\n"); } catch { }
    }

    public async Task<bool> LoginWithGoogleAsync(string idToken)
    {
        EnsureBaseAddress();
        return await ExchangeToken(idToken, "Google");
    }

    public async Task<bool> LoginWithMicrosoftAsync(string idToken)
    {
        EnsureBaseAddress();
        return await ExchangeToken(idToken, "Microsoft");
    }

    /// <summary>Asegura que el HttpClient use la URL actual de settings</summary>
    private void EnsureBaseAddress()
    {
        var currentUrl = _settings.Current.ApiUrl;
        if (_http.BaseAddress?.OriginalString != currentUrl)
            _http.BaseAddress = new Uri(currentUrl);
    }

    public async Task<bool> LoginWithEmailAsync(string email, string password)
    {
        EnsureBaseAddress();
        return await EmailAuthAsync("/api/Auth/login", email, password);
    }

    public async Task<bool> RegisterWithEmailAsync(string email, string password, string? displayName)
    {
        EnsureBaseAddress();
        return await EmailAuthAsync("/api/Auth/register", email, password, displayName);
    }

    public async Task<bool> RefreshTokenAsync()
    {
        EnsureBaseAddress();
        var refreshToken = _credentials.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        try
        {
            LogHttp($">> POST /api/Auth/refresh");
            var response = await _http.PostAsJsonAsync("/api/Auth/refresh",
                new { refreshToken });
            var respBody = await response.Content.ReadAsStringAsync();
            LogHttp($"<< {(int)response.StatusCode} {respBody}");

            if (!response.IsSuccessStatusCode) return false;

            var auth = JsonSerializer.Deserialize<AuthResponse>(respBody, JsonOptions);
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

    private async Task<bool> EmailAuthAsync(string endpoint, string email, string password, string? displayName = null)
    {
        try
        {
            var body = displayName is not null
                ? new { email, password, displayName }
                : (object)new { email, password };

            var json = JsonSerializer.Serialize(body);
            var fullUrl = $"{_http.BaseAddress?.OriginalString.TrimEnd('/')}{endpoint}";
            LogHttp($">> POST {fullUrl} {json}");

            var response = await _http.PostAsJsonAsync(endpoint, body);
            var respJson = await response.Content.ReadAsStringAsync();
            LogHttp($"<< {(int)response.StatusCode} {respJson}");

            if (!response.IsSuccessStatusCode)
            {
                System.Windows.MessageBox.Show(
                    $"Respuesta del servidor:\n\nHTTP {(int)response.StatusCode}\n\n{respJson}",
                    "DEBUG Auth",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return false;
            }

            var auth = JsonSerializer.Deserialize<AuthResponse>(respJson, JsonOptions);
            if (auth is null) return false;

            ApplyAuth(auth);
            return true;
        }
        catch (Exception ex)
        {
            LogHttp($"!! ERROR: {ex.GetType().Name}: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Error de conexión con el servidor:\n\n{ex.GetType().Name}: {ex.Message}",
                "Error de autenticación",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    private async Task<bool> ExchangeToken(string idToken, string provider)
    {
        try
        {
            var endpoint = provider == "Google" ? "google" : "microsoft";
            LogHttp($">> POST /api/Auth/{endpoint}");
            var response = await _http.PostAsJsonAsync($"/api/Auth/{endpoint}",
                new { idToken, provider });
            var respBody = await response.Content.ReadAsStringAsync();
            LogHttp($"<< {(int)response.StatusCode} {respBody}");

            if (!response.IsSuccessStatusCode) return false;

            var auth = JsonSerializer.Deserialize<AuthResponse>(respBody, JsonOptions);
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

        LogHttp($"ApplyAuth: AccessToken={(AccessToken is not null ? $"{AccessToken[..Math.Min(AccessToken.Length, 30)]}..." : "NULL")}, " +
            $"RefreshToken={(auth.RefreshToken is not null ? $"{auth.RefreshToken[..Math.Min(auth.RefreshToken.Length, 10)]}..." : "NULL")}, " +
            $"User={auth.User?.Email ?? "NULL"}");

        AuthChanged?.Invoke();
    }
}
