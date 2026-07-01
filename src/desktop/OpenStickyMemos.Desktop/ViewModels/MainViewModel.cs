using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenStickyMemos.Desktop.Views;
using OpenStickyMemos.Desktop.Services;
using System.Threading.Tasks;
using System.Windows;

namespace OpenStickyMemos.Desktop.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;
    private readonly ISignalRService _signalR;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _userDisplayName;

    [ObservableProperty]
    private string? _userAvatarUrl;

    private static readonly string LogPath = App.LogPath;

    public MainViewModel(
        IAuthService auth,
        INavigationService navigation,
        ISignalRService signalR)
    {
        _auth = auth;
        _navigation = navigation;
        _signalR = signalR;

        _auth.AuthChanged += OnAuthChanged;

        // Mostrar LoginView inmediatamente, auto-login en segundo plano
        _navigation.NavigateTo<Views.LoginView>();

        _ = TryAutoLoginAsync();
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            var success = await _auth.TryAutoLoginAsync();
            if (success)
            {
                ApplyUserState();
                await _signalR.StartAsync();
                _navigation.NavigateTo<Views.DashboardView>();
            }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [ERROR] TryAutoLoginAsync: {ex}\n"); } catch { }
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _signalR.StopAsync();
        _auth.Logout();
        ApplyUserState();
        _navigation.NavigateTo<Views.LoginView>();
    }

    private void OnAuthChanged()
    {
        ApplyUserState();
    }

    private void ApplyUserState()
    {
        IsLoggedIn = _auth.IsLoggedIn;
        UserDisplayName = _auth.CurrentUser?.DisplayName;
        UserAvatarUrl = _auth.CurrentUser?.AvatarUrl;
    }
}
