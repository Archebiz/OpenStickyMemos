using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using OpenStickyMemos.Desktop.ViewModels;

namespace OpenStickyMemos.Desktop.Views;

public partial class LoginView : UserControl
{
    private LoginViewModel? _vm;
    private bool _passwordVisible;

    public LoginView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Placeholder management (patrón PACRI) ──
    private static void SetupPlaceholder(TextBox tb, TextBlock ph)
    {
        tb.TextChanged += (_, _) =>
            ph.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetupPlaceholder(PasswordBox pb, TextBlock ph)
    {
        pb.PasswordChanged += (_, _) =>
            ph.Visibility = string.IsNullOrEmpty(pb.Password) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Toggle visibilidad de contraseña (patrón PACRI) ──
    private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        if (_passwordVisible)
        {
            // Mostrar contraseña en TextBox
            txtClaveVisible.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            txtClaveVisible.Visibility = Visibility.Visible;
            PlaceholderPassword.Visibility = string.IsNullOrEmpty(txtClaveVisible.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            ToggleIcon.Text = "\U0001F648"; // 🙈
            txtClaveVisible.Focus();
            txtClaveVisible.SelectionStart = txtClaveVisible.Text.Length;
        }
        else
        {
            // Ocultar contraseña en PasswordBox
            PasswordBox.Password = txtClaveVisible.Text;
            txtClaveVisible.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            PlaceholderPassword.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible : Visibility.Collapsed;
            ToggleIcon.Text = "\U0001F441"; // 👁
            PasswordBox.Focus();
        }
    }

    private void TxtClave_PasswordChanged(object sender, RoutedEventArgs e)
    {
        PlaceholderPassword.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;

        if (_vm is not null)
            _vm.Password = PasswordBox.Password;
    }

    // ── Sincronizar _vm.Password y placeholder desde txtClaveVisible ──
    private void TxtClaveVisible_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderPassword.Visibility = string.IsNullOrEmpty(txtClaveVisible.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        if (_vm is not null)
            _vm.Password = txtClaveVisible.Text;
    }

    // ── Enter key → submit (patrón PACRI) ──
    private void TxtClave_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && BtnSubmit.Command?.CanExecute(null) == true)
            BtnSubmit.Command.Execute(null);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as LoginViewModel;
        if (_vm is null) return;

        // Inicializar placeholders inline (patrón PACRI)
        SetupPlaceholder(TxtEmail, PlaceholderEmail);

        // Forzar estado inicial del placeholder del email
        PlaceholderEmail.Visibility = string.IsNullOrEmpty(TxtEmail.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        // Sincronizar PasswordBox con ViewModel
        PasswordBox.PasswordChanged += TxtClave_PasswordChanged;

        // Sincronizar txtClaveVisible (modo ver contraseña) con ViewModel
        txtClaveVisible.TextChanged += TxtClaveVisible_TextChanged;

        // Focus inicial
        TxtEmail.Focus();

        // Initialize WebView2
        await AuthWebView.EnsureCoreWebView2Async();

        // Intercept navigation to capture the id_token from redirect
        AuthWebView.CoreWebView2.NavigationStarting += OnNavigationStarting;

        // Listen for ViewModel events to start WebView navigation
        _vm.StartGoogleLogin += (url) =>
        {
            ShowWebView(true);
            AuthWebView.CoreWebView2.Navigate(url);
        };

        _vm.StartMicrosoftLogin += (url) =>
        {
            ShowWebView(true);
            AuthWebView.CoreWebView2.Navigate(url);
        };

        _vm.CloseWebView += () => ShowWebView(false);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri;

        // Intercept redirect URI (http://localhost)
        if (uri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true; // Stop navigation

            // Extract id_token from URL fragment (#id_token=...)
            var fragment = new Uri(uri).Fragment?.TrimStart('#');
            if (!string.IsNullOrEmpty(fragment))
            {
                var idToken = ParseFragmentValue(fragment, "id_token");
                if (!string.IsNullOrEmpty(idToken))
                {
                    _vm?.HandleToken(idToken);
                    return;
                }
            }

            // También soportar query string (?id_token=...) como fallback
            var query = new Uri(uri).Query?.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                var idToken = ParseFragmentValue(query, "id_token");
                if (!string.IsNullOrEmpty(idToken))
                {
                    _vm?.HandleToken(idToken);
                    return;
                }
            }

            // Si no hay token, cerrar WebView
            _vm?.HandleToken(null);
        }
    }

    private static string? ParseFragmentValue(string fragment, string key)
    {
        foreach (var part in fragment.Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0] == key)
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private void ShowWebView(bool show)
    {
        if (_vm is not null)
            _vm.ShowWebView = show;
    }
}
