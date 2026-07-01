using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;
using OpenStickyMemos.Desktop.Views;

namespace OpenStickyMemos.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static readonly string LogPath = Path.Combine(
        Path.GetDirectoryName(typeof(App).Assembly.Location) ?? ".",
        "crash.log");

    public App()
    {
        try
        {
            Services = ConfigureServices();
        }
        catch (Exception ex)
        {
            File.WriteAllText(LogPath, $"[FATAL] ConfigureServices: {ex}\n");
            throw;
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Global exception handlers ──
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", (Exception)args.ExceptionObject);

        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            args.Handled = true; // evitar cierre abrupto
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("TaskScheduler", args.Exception);
            args.SetObserved();
        };

        try
        {
            base.OnStartup(e);
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogCrash("OnStartup", ex);
            Shutdown(-1);
        }
    }

    private static void LogCrash(string source, Exception ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}\n");
        }
        catch
        {
            // No hay nada que hacer si falla el log
        }
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
        services.AddTransient<StickyBoardViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<StickyBoardView>();

        return services.BuildServiceProvider();
    }
}

