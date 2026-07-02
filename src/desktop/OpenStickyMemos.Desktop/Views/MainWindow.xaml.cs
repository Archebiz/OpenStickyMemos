using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.ViewModels;

namespace OpenStickyMemos.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly INavigationService _navigation;

    public MainWindow(INavigationService navigation, MainViewModel viewModel)
    {
        InitializeComponent();
        _navigation = navigation;
        DataContext = viewModel;

        _navigation.NavigationChanged += OnNavigationChanged;

        // Navegar a LoginView inmediatamente (aquí ya hay suscriptor)
        NavigateSafe(typeof(LoginView));
    }

    private void OnNavigationChanged(Type viewType) => NavigateSafe(viewType);

    private void NavigateSafe(Type viewType)
    {
        var log = $"[{DateTime.Now:HH:mm:ss}] NavigateTo: {viewType.Name}\n";

        try
        {
            var view = App.Services.GetRequiredService(viewType) as UserControl;
            if (view is null)
            {
                File.AppendAllText(App.LogPath, log + "  -> ERROR: view is null\n");
                return;
            }
            log += $"  -> View creada: {view.GetType().Name}\n";

            var vmTypeName = viewType.FullName!
                .Replace(".Views.", ".ViewModels.");
            // Quitar solo el sufijo "View" y agregar "ViewModel"
            // (NO usar Replace("View","ViewModel") porque corrompe "ViewModels")
            if (vmTypeName.EndsWith("View"))
                vmTypeName = vmTypeName[..^4] + "ViewModel";
            else
                vmTypeName += "ViewModel";
            var vmType = viewType.Assembly.GetType(vmTypeName);
            if (vmType is not null)
            {
                view.DataContext = App.Services.GetRequiredService(vmType);
                log += $"  -> DataContext: {vmType.Name}\n";
            }
            else
            {
                log += $"  -> WARNING: ViewModel no encontrado\n";
            }

            MainContentArea.Content = view;
            log += "  -> Content asignado OK\n";

            // Ajustar ventana según la vista
            if (viewType == typeof(LoginView))
            {
                TitleBar.Visibility = Visibility.Collapsed;
                MinWidth = 0; MinHeight = 0;
                Width = 400; Height = 640;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                TitleBar.Visibility = Visibility.Visible;
                MinWidth = 800; MinHeight = 500;
                Width = 1100; Height = 700;
                ResizeMode = ResizeMode.CanResize;
            }
        }
        catch (Exception ex)
        {
            log += $"  -> EXCEPTION: {ex.GetType().Name}: {ex.Message}\n";
        }

        try { File.AppendAllText(App.LogPath, log); } catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
