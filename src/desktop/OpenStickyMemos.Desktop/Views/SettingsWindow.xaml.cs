using System.Windows;
using OpenStickyMemos.Desktop.Services;

namespace OpenStickyMemos.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;

    public SettingsWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        // Cargar valores actuales
        var current = _settingsService.Current;
        txtApiUrl.Text = current.ApiUrl;
        txtGoogleClientId.Text = current.OAuth.Google.ClientId;
        txtGoogleRedirectUri.Text = current.OAuth.Google.RedirectUri;
        txtMicrosoftClientId.Text = current.OAuth.Microsoft.ClientId;
        txtMicrosoftTenantId.Text = current.OAuth.Microsoft.TenantId;
        txtMicrosoftRedirectUri.Text = current.OAuth.Microsoft.RedirectUri;
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Current;

        // Validar URL
        var apiUrl = txtApiUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            lblMensaje.Text = "La URL del servidor es requerida";
            lblMensaje.Foreground = System.Windows.Media.Brushes.Red;
            lblMensaje.Visibility = Visibility.Visible;
            return;
        }

        settings.ApiUrl = apiUrl.TrimEnd('/');
        settings.SignalRUrl = apiUrl.TrimEnd('/') + "/api/hubs/notes";
        settings.OAuth.Google.ClientId = txtGoogleClientId.Text.Trim();
        settings.OAuth.Google.RedirectUri = txtGoogleRedirectUri.Text.Trim();
        settings.OAuth.Microsoft.ClientId = txtMicrosoftClientId.Text.Trim();
        settings.OAuth.Microsoft.TenantId = txtMicrosoftTenantId.Text.Trim();
        settings.OAuth.Microsoft.RedirectUri = txtMicrosoftRedirectUri.Text.Trim();

        _settingsService.Save(settings);

        lblMensaje.Text = "Configuración guardada correctamente";
        lblMensaje.Foreground = System.Windows.Media.Brushes.Green;
        lblMensaje.Visibility = Visibility.Visible;
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}