using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using CalendarMaker_MAUI.ViewModels;
using FluentAssertions;
using Moq;

namespace CalendarMaker.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ProjectsViewModel"/> using a mocked storage service and the real
/// (pure) <see cref="LayoutService"/>.
/// </summary>
public class ProjectsViewModelTests
{
    private readonly Mock<IProjectStorageService> _storage = new();
    private readonly LayoutService _layoutService = new();
    private readonly ProjectsViewModel _viewModel;

    public ProjectsViewModelTests()
    {
        _viewModel = new ProjectsViewModel(_storage.Object, _layoutService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesProjectsFromStorage()
    {
        var stored = new List<CalendarProject>
        {
            new() { Name = "One" },
            new() { Name = "Two" }
        };
        _storage.Setup(s => s.GetProjectsAsync()).ReturnsAsync(stored);

        await _viewModel.LoadAsync();

        _viewModel.Projects.Should().HaveCount(2);
        _viewModel.Projects.Select(p => p.Name).Should().BeEquivalentTo(new[] { "One", "Two" });
    }

    [Fact]
    public async Task CreateDefaultProjectAsync_CreatesLandscapeFiveBySevenAndReloads()
    {
        CalendarProject? created = null;
        _storage.Setup(s => s.CreateProjectAsync(It.IsAny<CalendarProject>()))
            .Callback<CalendarProject>(p => created = p)
            .ReturnsAsync("new-id");
        _storage.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<CalendarProject>());

        await _viewModel.CreateDefaultProjectAsync();

        created.Should().NotBeNull();
        created!.Name.Should().Be("My Calendar");
        created.PageSpec.Size.Should().Be(PageSize.FiveBySeven);
        created.PageSpec.Orientation.Should().Be(PageOrientation.Landscape);
        // LayoutService should have applied the 5x7 landscape preset.
        created.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoLeftCalendarRight);
        _storage.Verify(s => s.GetProjectsAsync(), Times.Once); // reload after create
    }

    [Fact]
    public async Task CreateSpecificProjectAsync_AppliesPresetPersistsAndReloads()
    {
        var project = new CalendarProject
        {
            Name = "Custom",
            PageSpec = new PageSpec { Size = PageSize.Tabloid_11x17, Orientation = PageOrientation.Portrait }
        };
        _storage.Setup(s => s.CreateProjectAsync(project)).ReturnsAsync("id");
        _storage.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<CalendarProject> { project });

        await _viewModel.CreateSpecificProjectAsync(project);

        // Tabloid portrait preset => photo top, 60/40 split.
        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoTopCalendarBottom);
        project.LayoutSpec.SplitRatio.Should().Be(0.6);
        _storage.Verify(s => s.CreateProjectAsync(project), Times.Once);
        _viewModel.Projects.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteProjectAsync_DeletesByIdAndReloads()
    {
        var project = new CalendarProject { Id = "del-1", Name = "Doomed" };
        _storage.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<CalendarProject>());

        await _viewModel.DeleteProjectAsync(project);

        _storage.Verify(s => s.DeleteProjectAsync("del-1"), Times.Once);
        _storage.Verify(s => s.GetProjectsAsync(), Times.Once); // reload after delete
        _viewModel.Projects.Should().BeEmpty();
    }
}
