using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;
using OpenStickyMemos.Desktop.Views;

namespace OpenStickyMemos.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        Services = ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginView>();
        services.AddTransient<DashboardView>();

        return services.BuildServiceProvider();
    }
}

