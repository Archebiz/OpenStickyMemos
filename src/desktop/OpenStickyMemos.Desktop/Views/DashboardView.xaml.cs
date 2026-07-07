using System.Windows;
using System.Windows.Controls;

namespace OpenStickyMemos.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.DashboardViewModel vm)
            await vm.LoadProjectsCommand.ExecuteAsync(null);
    }
}
