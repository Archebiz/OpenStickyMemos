using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenStickyMemos.Desktop.Services;
using System.Collections.ObjectModel;

namespace OpenStickyMemos.Desktop.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IApiService _api;

    public ObservableCollection<ProjectResponse> Projects { get; } = new();

    [ObservableProperty]
    private bool _hasProjects;

    public DashboardViewModel(IApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var projects = await _api.GetProjectsAsync();
            Projects.Clear();
            foreach (var p in projects)
                Projects.Add(p);
            HasProjects = Projects.Count > 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar proyectos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
