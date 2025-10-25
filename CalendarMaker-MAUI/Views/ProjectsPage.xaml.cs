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
        var modal = new NewProjectModal();

        TaskCompletionSource<(string preset, string name, int year)?> tcs = new();

        modal.Cancelled += async (_, __) =>
        {
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
            tcs.TrySetResult(null);
        };
        modal.Created += async (_, __) =>
        {
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
            if (modal.SelectedPreset is string p && modal.ProjectName is string n && modal.Year is int y)
            {
                tcs.TrySetResult((p, n, y));
            }
            else
            {
                tcs.TrySetResult(null);
            }
        };

        await Shell.Current.Navigation.PushModalAsync(modal);
        var result = await tcs.Task;
        if (result is null)
        {
            return;
        }

        var (preset, name, year) = result.Value;

        var project = new CalendarProject
        {
            Name = string.IsNullOrWhiteSpace(name) ? "My Calendar" : name,
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
        else if (preset.StartsWith("11x17"))
        {
            project.PageSpec.Size = PageSize.Tabloid_11x17;
            project.PageSpec.Orientation = PageOrientation.Portrait;
            project.LayoutSpec.Placement = LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.6;
        }
        else if (preset.StartsWith("13x19"))
        {
            project.PageSpec.Size = PageSize.SuperB_13x19;
            project.PageSpec.Orientation = PageOrientation.Portrait;
            project.LayoutSpec.Placement = LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.65;
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
            bool confirm = await this.DisplayAlertAsync("Delete Project", $"Delete '{proj.Name}'? This removes all its files.", "Delete", "Cancel");
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
                string route = $"designer?projectId={Uri.EscapeDataString(proj.Id)}";
                await Shell.Current.GoToAsync(route);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}