using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStickyMemos.Desktop.Services;

public class AppSettings
{
    public string ApiUrl { get; set; } = "http://localhost:5000";
    public string SignalRUrl { get; set; } = "http://localhost:5000/api/hubs/notes";
    public OAuthSettings OAuth { get; set; } = new();
}

public class OAuthSettings
{
    public ProviderSettings Google { get; set; } = new();
    public ProviderSettings Microsoft { get; set; } = new();
}

public class ProviderSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost";
    public string TenantId { get; set; } = "common";
}

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save(AppSettings settings);
}

public class SettingsService : ISettingsService
{
    public AppSettings Current { get; }

    public SettingsService()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(basePath, "appsettings.json");

        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        else
        {
            Current = new AppSettings();
        }

        // Override with environment variables (for Railway)
        var envApiUrl = Environment.GetEnvironmentVariable("API_URL");
        if (!string.IsNullOrEmpty(envApiUrl))
            Current.ApiUrl = envApiUrl;

        var envSignalR = Environment.GetEnvironmentVariable("SIGNALR_URL");
        if (!string.IsNullOrEmpty(envSignalR))
            Current.SignalRUrl = envSignalR;
    }

    public void Save(AppSettings settings)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(basePath, "appsettings.json");
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        // Actualizar Current in-place (no reemplazar referencia)
        Current.ApiUrl = settings.ApiUrl;
        Current.SignalRUrl = settings.SignalRUrl;
        Current.OAuth = settings.OAuth;
    }
}
