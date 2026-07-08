using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenStickyMemos.Desktop.Services;

public class AppSettings
{
    public string ApiUrl { get; set; } = "http://localhost:5000";
    public string SignalRUrl { get; set; } = "http://localhost:5000/api/hubs/notes";
    public string WebUrl { get; set; } = "http://localhost:4200";
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

    // Ruta del archivo bundled (se sobreescribe con cada build/publish)
    private static readonly string BundledPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    // Ruta del archivo de usuario (persiste entre builds, en AppData)
    private static readonly string UserSettingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenStickyMemos",
            "settings.json");

    public SettingsService()
    {
        // 1. Cargar defaults desde el archivo bundled (junto al .exe)
        AppSettings defaults;
        if (File.Exists(BundledPath))
        {
            var json = File.ReadAllText(BundledPath);
            defaults = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        else
        {
            defaults = new AppSettings();
        }

        // 2. Sobrescribir con configuración persistente del usuario (si existe)
        var userDir = Path.GetDirectoryName(UserSettingsPath)!;
        if (!Directory.Exists(userDir))
            Directory.CreateDirectory(userDir);

        if (File.Exists(UserSettingsPath))
        {
            try
            {
                var userJson = File.ReadAllText(UserSettingsPath);
                var userSettings = JsonSerializer.Deserialize<AppSettings>(userJson);
                if (userSettings is not null)
                {
                    if (!string.IsNullOrEmpty(userSettings.ApiUrl))
                        defaults.ApiUrl = userSettings.ApiUrl;
                    if (!string.IsNullOrEmpty(userSettings.SignalRUrl))
                        defaults.SignalRUrl = userSettings.SignalRUrl;
                    if (!string.IsNullOrEmpty(userSettings.WebUrl))
                        defaults.WebUrl = userSettings.WebUrl;
                    if (userSettings.OAuth is not null)
                    {
                        if (!string.IsNullOrEmpty(userSettings.OAuth.Google.ClientId))
                            defaults.OAuth.Google.ClientId = userSettings.OAuth.Google.ClientId;
                        if (!string.IsNullOrEmpty(userSettings.OAuth.Microsoft.ClientId))
                            defaults.OAuth.Microsoft.ClientId = userSettings.OAuth.Microsoft.ClientId;
                    }
                }
            }
            catch { /* ignorar errores de lectura */ }
        }

        // 3. Override con variables de entorno (para Railway)
        var envApiUrl = Environment.GetEnvironmentVariable("API_URL");
        if (!string.IsNullOrEmpty(envApiUrl))
            defaults.ApiUrl = envApiUrl;

        var envSignalR = Environment.GetEnvironmentVariable("SIGNALR_URL");
        if (!string.IsNullOrEmpty(envSignalR))
            defaults.SignalRUrl = envSignalR;

        var envWebUrl = Environment.GetEnvironmentVariable("WEB_URL");
        if (!string.IsNullOrEmpty(envWebUrl))
            defaults.WebUrl = envWebUrl;

        Current = defaults;
    }

    public void Save(AppSettings settings)
    {
        // Guardar SOLO en AppData, no tocamos el bundled
        var userDir = Path.GetDirectoryName(UserSettingsPath)!;
        if (!Directory.Exists(userDir))
            Directory.CreateDirectory(userDir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(UserSettingsPath, json);

        // Actualizar Current in-place
        Current.ApiUrl = settings.ApiUrl;
        Current.SignalRUrl = settings.SignalRUrl;
        Current.WebUrl = settings.WebUrl;
        Current.OAuth = settings.OAuth;
    }
}
