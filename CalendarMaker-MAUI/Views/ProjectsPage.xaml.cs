using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace CalendarMaker_MAUI.Views;

public partial class ProjectsPage : ContentPage
{
    private readonly ProjectsViewModel _vm;

    public ProjectsPage(ProjectsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
        CreateBtn.Clicked += async (_, __) => await _vm.CreateDefaultProjectAsync();
        Loaded += async (_, __) => await _vm.LoadAsync();
        ProjectsList.SetBinding(ItemsView.ItemsSourceProperty, nameof(ProjectsViewModel.Projects));
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CalendarProject proj)
        {
            var confirm = await DisplayAlertAsync("Delete Project", $"Delete '{proj.Name}'? This removes all its files.", "Delete", "Cancel");
            if (confirm)
            {
                await _vm.DeleteProjectAsync(proj);
            }
        }
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CalendarProject proj)
        {
            btn.IsEnabled = false;

            // Pass calendar project id as query parameter
            var route = $"designer?projectId={Uri.EscapeDataString(proj.Id)}";
            await Shell.Current.GoToAsync(route);
            btn.IsEnabled = true;
        }
    }
}
