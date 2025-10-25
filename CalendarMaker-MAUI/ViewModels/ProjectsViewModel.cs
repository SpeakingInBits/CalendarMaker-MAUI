namespace CalendarMaker_MAUI.ViewModels;

using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly IProjectStorageService _storage;
    private readonly ILayoutService _layoutService;

    [ObservableProperty]
    private List<CalendarProject> projects = new();

    public ProjectsViewModel(IProjectStorageService storage, ILayoutService layoutService)
    {
        _storage = storage;
        _layoutService = layoutService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Projects = (await _storage.GetProjectsAsync()).ToList();
    }

    [RelayCommand]
    public async Task CreateDefaultProjectAsync()
    {
        var project = new CalendarProject
        {
            Name = "My Calendar",
            PageSpec = new PageSpec { Size = PageSize.FiveBySeven, Orientation = PageOrientation.Landscape },
        };
        _layoutService.ApplyDefaultPreset(project);
        await _storage.CreateProjectAsync(project);
        await LoadAsync();
    }

    public async Task CreateSpecificProjectAsync(CalendarProject project)
    {
        _layoutService.ApplyDefaultPreset(project);
        await _storage.CreateProjectAsync(project);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteProjectAsync(CalendarProject project)
    {
        await _storage.DeleteProjectAsync(project.Id);
        await LoadAsync();
    }
}