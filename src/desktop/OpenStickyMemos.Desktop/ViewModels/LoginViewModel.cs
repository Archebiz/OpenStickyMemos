using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenStickyMemos.Desktop.Services;
using System.Reflection;

namespace OpenStickyMemos.Desktop.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly ISignalRService _signalR;

    // ── Credenciales de administrador offline ──
    private const string ADMIN_USUARIO = "ADMINISTRADOR";
    private const string ADMIN_PASSWORD = "UrC12025@!";

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoginMode = true;

    [ObservableProperty]
    private bool _rememberPassword;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string _versionText = string.Empty;

    /// <summary>Evento para que la vista navegue el WebView2 a la URL de OAuth</summary>
    public event Action<string>? StartGoogleLogin;
    public event Action<string>? StartMicrosoftLogin;
    public event Action? CloseWebView;

    [ObservableProperty]
    private bool _showWebView;

    public LoginViewModel(IAuthService auth, INavigationService navigation,
        ISettingsService settings, ISignalRService signalR)
    {
        _auth = auth;
        _navigation = navigation;
        _settings = settings;
        _signalR = signalR;

        // Versión desde el ensamblado
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.0";
    }

    [RelayCommand]
    private void CloseApp()
    {
        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new Views.SettingsWindow(_settings);
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task LoginWithEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Completa todos los campos";
            return;
        }

        // ── Verificar credenciales de administrador offline ──
        if (Email.Trim().ToUpper() == ADMIN_USUARIO && Password == ADMIN_PASSWORD)
        {
            IsAdmin = true;
            ErrorMessage = null;
            _navigation.NavigateTo<Views.DashboardView>();
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        IsAdmin = false;

        try
        {
            bool success;
            if (IsLoginMode)
                success = await _auth.LoginWithEmailAsync(Email.Trim(), Password);
            else
                success = await _auth.RegisterWithEmailAsync(Email.Trim(), Password, null);

            if (success)
            {
                await _signalR.StartAsync();
                _navigation.NavigateTo<Views.DashboardView>();
            }
            else
            {
                ErrorMessage = IsLoginMode
                    ? "Email o contraseña incorrectos"
                    : "No se pudo registrar. ¿El email ya está en uso?";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsLoginMode = !IsLoginMode;
        ErrorMessage = null;
        IsAdmin = false;
    }

    [RelayCommand]
    private void LoginWithGoogle()
    {
        var clientId = _settings.Current.OAuth.Google.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            ErrorMessage = "Google Client ID no configurado en appsettings.json";
            return;
        }

        var redirectUri = _settings.Current.OAuth.Google.RedirectUri;
        ErrorMessage = null;

        // Construir URL de OAuth de Google con response_mode=fragment
        var url = $"https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=id_token" +
                  $"&scope=openid%20profile%20email" +
                  $"&nonce={Guid.NewGuid():N}";

        StartGoogleLogin?.Invoke(url);
    }

    [RelayCommand]
    private void LoginWithMicrosoft()
    {
        var clientId = _settings.Current.OAuth.Microsoft.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            ErrorMessage = "Microsoft Client ID no configurado en appsettings.json";
            return;
        }

        var redirectUri = _settings.Current.OAuth.Microsoft.RedirectUri;
        var tenantId = _settings.Current.OAuth.Microsoft.TenantId;
        ErrorMessage = null;

        // Construir URL de OAuth de Microsoft con response_mode=fragment
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=id_token" +
                  $"&scope=openid%20profile%20email" +
                  $"&nonce={Guid.NewGuid():N}";

        StartMicrosoftLogin?.Invoke(url);
    }

    /// <summary>
    /// Procesa el id_token recibido del WebView2.
    /// </summary>
    public async void HandleToken(string? idToken)
    {
        ShowWebView = false;

        if (string.IsNullOrEmpty(idToken))
        {
            ErrorMessage = "No se recibió el token de autenticación";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Primero intentar con Google
            var success = await _auth.LoginWithGoogleAsync(idToken);

            if (!success)
                success = await _auth.LoginWithMicrosoftAsync(idToken);

            if (success)
            {
                await _signalR.StartAsync();
                _navigation.NavigateTo<Views.DashboardView>();
            }
            else
            {
                ErrorMessage = "Error al iniciar sesión. Verifica la configuración.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
