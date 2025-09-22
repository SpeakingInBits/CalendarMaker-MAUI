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
    }
}
