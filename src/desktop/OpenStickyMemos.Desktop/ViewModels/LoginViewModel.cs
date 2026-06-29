using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenStickyMemos.Desktop.Services;
using System.Collections.ObjectModel;

namespace OpenStickyMemos.Desktop.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;

    public LoginViewModel(IAuthService auth, INavigationService navigation)
    {
        _auth = auth;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        // TODO: Integrate WebView2 for Google OAuth
        // For now, this is a placeholder
        IsLoading = true;
        ErrorMessage = "Integración OAuth pendiente";
        IsLoading = false;
    }

    [RelayCommand]
    private async Task LoginWithMicrosoftAsync()
    {
        // TODO: Integrate WebView2 for Microsoft OAuth
        IsLoading = true;
        ErrorMessage = "Integración OAuth pendiente";
        IsLoading = false;
    }
}
