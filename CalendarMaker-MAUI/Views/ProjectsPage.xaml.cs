using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.ViewModels;

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
        ProjectsList.SelectionChanged += ProjectsList_SelectionChanged;
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CalendarProject proj)
        {
            var confirm = await DisplayAlert("Delete Project", $"Delete '{proj.Name}'? This removes all its files.", "Delete", "Cancel");
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
            await Navigation.PushAsync(new DesignerPage());
        }
    }

    private async void ProjectsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is not null)
        {
            await Navigation.PushAsync(new DesignerPage());
            ProjectsList.SelectedItem = null;
        }
    }
}
