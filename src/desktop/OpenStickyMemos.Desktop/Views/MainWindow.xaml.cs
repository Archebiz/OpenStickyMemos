using System.Windows;
using System.Windows.Controls;
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
        if (Activator.CreateInstance(viewType) is UserControl view)
            MainContentArea.Content = view;
    }
}
