using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// File-backed tests for <see cref="ProjectStorageService"/> using an isolated temp directory
/// (via the root-directory constructor seam) so no MAUI app-data context is required.
/// </summary>
public class ProjectStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStorageService _storage;

    public ProjectStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CalendarMakerTests", Guid.NewGuid().ToString("N"));
        _storage = new ProjectStorageService(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort cleanup */ }
    }

    private static CalendarProject NewProject(string name = "Test") => new()
    {
        Name = name,
        Year = 2025,
        PageSpec = new PageSpec { Size = PageSize.Letter }
    };

    [Fact]
    public async Task CreateProjectAsync_AssignsIdAndPersistsToDisk()
    {
        var project = NewProject();

        string id = await _storage.CreateProjectAsync(project);

        id.Should().NotBeNullOrWhiteSpace();
        project.Id.Should().Be(id);
        File.Exists(Path.Combine(_root, id, "project.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsAllPersistedProjects()
    {
        await _storage.CreateProjectAsync(NewProject("A"));
        await _storage.CreateProjectAsync(NewProject("B"));

        var projects = await _storage.GetProjectsAsync();

        projects.Should().HaveCount(2);
        projects.Select(p => p.Name).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public async Task GetProjectsAsync_EmptyRoot_ReturnsEmpty()
    {
        var projects = await _storage.GetProjectsAsync();

        projects.Should().BeEmpty();
    }

    [Fact]
    public async Task RoundTrip_PreservesProjectData()
    {
        var project = NewProject("Round Trip");
        project.StartMonth = 6;
        project.LayoutSpec.SplitRatio = 0.65;
        await _storage.CreateProjectAsync(project);

        var loaded = (await _storage.GetProjectsAsync()).Single();

        loaded.Name.Should().Be("Round Trip");
        loaded.Year.Should().Be(2025);
        loaded.StartMonth.Should().Be(6);
        loaded.LayoutSpec.SplitRatio.Should().Be(0.65);
        loaded.PageSpec.Size.Should().Be(PageSize.Letter);
    }

    [Fact]
    public async Task UpdateProjectAsync_PersistsChangesAndStampsUpdatedUtc()
    {
        var project = NewProject("Original");
        await _storage.CreateProjectAsync(project);
        DateTime beforeUpdate = DateTime.UtcNow;

        project.Name = "Renamed";
        await _storage.UpdateProjectAsync(project);

        var loaded = (await _storage.GetProjectsAsync()).Single();
        loaded.Name.Should().Be("Renamed");
        loaded.UpdatedUtc.Should().BeOnOrAfter(beforeUpdate.AddSeconds(-1));
    }

    [Fact]
    public async Task DeleteProjectAsync_RemovesTheProjectDirectory()
    {
        string id = await _storage.CreateProjectAsync(NewProject());

        await _storage.DeleteProjectAsync(id);

        Directory.Exists(Path.Combine(_root, id)).Should().BeFalse();
        (await _storage.GetProjectsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteProjectAsync_UnknownId_DoesNotThrow()
    {
        var act = async () => await _storage.DeleteProjectAsync("does-not-exist");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetProjectDirectory_ReturnsPathUnderRoot()
    {
        string dir = _storage.GetProjectDirectory("abc123");

        dir.Should().Be(Path.Combine(_root, "abc123"));
    }
}
