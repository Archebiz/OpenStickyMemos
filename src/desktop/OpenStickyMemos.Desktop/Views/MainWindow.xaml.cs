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
    }

    private void OnNavigationChanged(Type viewType)
    {
        var view = App.Services.GetRequiredService(viewType) as UserControl;
        if (view is null) return;

        // Asignar ViewModel como DataContext por convención (LoginView → LoginViewModel)
        var vmType = Type.GetType(viewType.FullName!.Replace("Views.", "ViewModels.").Replace("View", "ViewModel"));
        if (vmType is not null)
            view.DataContext = App.Services.GetRequiredService(vmType);

        MainContentArea.Content = view;
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
