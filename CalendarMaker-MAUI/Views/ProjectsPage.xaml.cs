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
        CreateBtn.Text = "New Project";
        CreateBtn.Clicked += async (_, __) => await OnNewProjectClicked();
        Loaded += async (_, __) => await _vm.LoadAsync();
        ProjectsList.SetBinding(ItemsView.ItemsSourceProperty, nameof(ProjectsViewModel.Projects));
    }

    private async Task OnNewProjectClicked()
    {
        var preset = await this.DisplayActionSheetAsync("Create Project", "Cancel", null,
            "5x7 Landscape 50/50 (Photo Left)",
            "Letter Portrait 50/50 (Photo Top)");
        if (preset is null || preset == "Cancel") return;

        var name = await this.DisplayPromptAsync("Project Name", "Enter a name:", initialValue: "My Calendar");
        if (string.IsNullOrWhiteSpace(name)) name = "My Calendar";
        var yearText = await this.DisplayPromptAsync("Year", "Enter calendar year:", initialValue: DateTime.Now.Year.ToString());
        var year = DateTime.Now.Year;
        _ = int.TryParse(yearText, out year);

        var project = new CalendarProject
        {
            Name = name,
            Year = year,
            FirstDayOfWeek = DayOfWeek.Sunday,
            PageSpec = new PageSpec(),
            Margins = new Margins(),
            Theme = new ThemeSpec(),
            LayoutSpec = new LayoutSpec { SplitRatio = 0.5 }
        };

        if (preset.StartsWith("5x7"))
        {
            project.PageSpec.Size = PageSize.FiveBySeven;
            project.PageSpec.Orientation = PageOrientation.Landscape;
            project.LayoutSpec.Placement = LayoutPlacement.PhotoLeftCalendarRight;
        }
        else
        {
            project.PageSpec.Size = PageSize.Letter;
            project.PageSpec.Orientation = PageOrientation.Portrait;
            project.LayoutSpec.Placement = LayoutPlacement.PhotoTopCalendarBottom;
        }

        await _vm.CreateSpecificProjectAsync(project);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CalendarProject proj)
        {
            var confirm = await this.DisplayAlertAsync("Delete Project", $"Delete '{proj.Name}'? This removes all its files.", "Delete", "Cancel");
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
            try
            {
                var route = $"designer?projectId={Uri.EscapeDataString(proj.Id)}";
                await Shell.Current.GoToAsync(route);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}
