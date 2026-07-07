using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenStickyMemos.Desktop.Services;
using OpenStickyMemos.Desktop.Views;
using System.Collections.ObjectModel;

namespace OpenStickyMemos.Desktop.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly INavigationService _navigation;

    public ObservableCollection<ProjectResponse> Projects { get; } = new();

    [ObservableProperty]
    private bool _hasProjects;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _newProjectDescription = string.Empty;

    [ObservableProperty]
    private bool _showCreateDialog;

    public DashboardViewModel(IApiService api, INavigationService navigation)
    {
        _api = api;
        _navigation = navigation;
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

    [RelayCommand]
    private void OpenCreateDialog()
    {
        NewProjectName = string.Empty;
        NewProjectDescription = string.Empty;
        ShowCreateDialog = true;
    }

    [RelayCommand]
    private void CloseCreateDialog()
    {
        ShowCreateDialog = false;
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        IsLoading = true;
        try
        {
            var project = await _api.CreateProjectAsync(
                NewProjectName.Trim(), NewProjectDescription?.Trim());
            Projects.Insert(0, project);
            HasProjects = true;
            ShowCreateDialog = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al crear proyecto: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenProject(string projectId)
    {
        _navigation.NavigationParameter = projectId;
        _navigation.NavigateTo<StickyBoardView>();
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(string projectId)
    {
        var deleted = await _api.DeleteProjectAsync(projectId);
        if (deleted)
        {
            var project = Projects.FirstOrDefault(p => p.Id == projectId);
            if (project is not null)
                Projects.Remove(project);
            HasProjects = Projects.Count > 0;
        }
    }
}
