using System;
using System.Windows.Controls;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using OpenStickyMemos.Desktop.ViewModels;

namespace OpenStickyMemos.Desktop.Views;

public partial class LoginView : UserControl
{
    private LoginViewModel? _vm;

    public LoginView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as LoginViewModel;
        if (_vm is null) return;

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
