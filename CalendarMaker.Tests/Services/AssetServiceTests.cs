using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;
using Moq;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for the in-memory asset assignment logic in <see cref="AssetService"/>.
/// File-copying paths that require the MAUI filesystem are out of scope here; these
/// tests exercise the pure project-manipulation behavior with a mocked storage service.
/// </summary>
public class AssetServiceTests
{
    private readonly Mock<IProjectStorageService> _storage = new();
    private readonly AssetService _service;

    public AssetServiceTests()
    {
        _service = new AssetService(_storage.Object);
    }

    private static CalendarProject ProjectWith(params ImageAsset[] assets)
    {
        var project = new CalendarProject { Id = "proj-1" };
        project.ImageAssets.AddRange(assets);
        return project;
    }

    [Fact]
    public async Task AssignPhotoToSlotAsync_MonthPhoto_AddsAssetAndSavesProject()
    {
        var source = new ImageAsset { Id = "src", Role = "unassigned", Path = "a.jpg" };
        var project = ProjectWith(source);

        await _service.AssignPhotoToSlotAsync(project, "src", monthIndex: 3, slotIndex: 0, role: "monthPhoto");

        var assigned = project.ImageAssets.SingleOrDefault(a => a.Role == "monthPhoto");
        assigned.Should().NotBeNull();
        assigned!.MonthIndex.Should().Be(3);
        assigned.SlotIndex.Should().Be(0);
        assigned.Path.Should().Be("a.jpg"); // reuses the source file
        _storage.Verify(s => s.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AssignPhotoToSlotAsync_CoverPhoto_LeavesMonthIndexNull()
    {
        var source = new ImageAsset { Id = "src", Role = "unassigned", Path = "cover.jpg" };
        var project = ProjectWith(source);

        await _service.AssignPhotoToSlotAsync(project, "src", monthIndex: 0, slotIndex: 1, role: "coverPhoto");

        var cover = project.ImageAssets.Single(a => a.Role == "coverPhoto");
        cover.MonthIndex.Should().BeNull();
        cover.SlotIndex.Should().Be(1);
    }

    [Fact]
    public async Task AssignPhotoToSlotAsync_UnknownAssetId_DoesNothing()
    {
        var project = ProjectWith(new ImageAsset { Id = "src", Role = "unassigned" });

        await _service.AssignPhotoToSlotAsync(project, "missing", monthIndex: 0, slotIndex: 0);

        project.ImageAssets.Should().HaveCount(1);
        _storage.Verify(s => s.UpdateProjectAsync(It.IsAny<CalendarProject>()), Times.Never);
    }

    [Fact]
    public async Task AssignPhotoToSlotAsync_ReplacesExistingPhotoInSameMonthSlot()
    {
        var source = new ImageAsset { Id = "src", Role = "unassigned", Path = "new.jpg" };
        var existing = new ImageAsset { Id = "old", Role = "monthPhoto", MonthIndex = 2, SlotIndex = 0, Path = "old.jpg" };
        var project = ProjectWith(source, existing);

        await _service.AssignPhotoToSlotAsync(project, "src", monthIndex: 2, slotIndex: 0, role: "monthPhoto");

        project.ImageAssets.Should().NotContain(a => a.Id == "old");
        project.ImageAssets.Count(a => a.Role == "monthPhoto" && a.MonthIndex == 2 && (a.SlotIndex ?? 0) == 0)
            .Should().Be(1);
    }

    [Fact]
    public async Task RemovePhotoFromSlotAsync_RemovesMatchingAssetAndSaves()
    {
        var asset = new ImageAsset { Id = "m", Role = "monthPhoto", MonthIndex = 5, SlotIndex = 0 };
        var project = ProjectWith(asset);

        await _service.RemovePhotoFromSlotAsync(project, monthIndex: 5, slotIndex: 0, role: "monthPhoto");

        project.ImageAssets.Should().BeEmpty();
        _storage.Verify(s => s.UpdateProjectAsync(project), Times.Once);
    }

    [Fact]
    public async Task RemovePhotoFromSlotAsync_NoMatch_DoesNotSave()
    {
        var asset = new ImageAsset { Id = "m", Role = "monthPhoto", MonthIndex = 5, SlotIndex = 0 };
        var project = ProjectWith(asset);

        await _service.RemovePhotoFromSlotAsync(project, monthIndex: 6, slotIndex: 0, role: "monthPhoto");

        project.ImageAssets.Should().HaveCount(1);
        _storage.Verify(s => s.UpdateProjectAsync(It.IsAny<CalendarProject>()), Times.Never);
    }

    [Fact]
    public async Task GetUnassignedPhotosAsync_ReturnsOnlyUnassigned()
    {
        var project = ProjectWith(
            new ImageAsset { Id = "u1", Role = "unassigned" },
            new ImageAsset { Id = "m1", Role = "monthPhoto", MonthIndex = 0 },
            new ImageAsset { Id = "u2", Role = "unassigned" });

        var result = await _service.GetUnassignedPhotosAsync(project);

        result.Should().OnlyContain(a => a.Role == "unassigned");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllPhotosAsync_ReturnsEveryAsset()
    {
        var project = ProjectWith(
            new ImageAsset { Id = "u1", Role = "unassigned" },
            new ImageAsset { Id = "m1", Role = "monthPhoto" });

        var result = await _service.GetAllPhotosAsync(project);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeletePhotoFromProjectAsync_RemovesAllAssetsSharingPathAndSaves()
    {
        var project = ProjectWith(
            new ImageAsset { Id = "a", Role = "unassigned", Path = "shared.jpg" },
            new ImageAsset { Id = "b", Role = "monthPhoto", MonthIndex = 1, Path = "shared.jpg" },
            new ImageAsset { Id = "c", Role = "monthPhoto", MonthIndex = 2, Path = "other.jpg" });

        await _service.DeletePhotoFromProjectAsync(project, "shared.jpg");

        project.ImageAssets.Select(a => a.Id).Should().BeEquivalentTo(new[] { "c" });
        _storage.Verify(s => s.UpdateProjectAsync(project), Times.Once);
    }

    [Fact]
    public async Task DeletePhotoFromProjectAsync_BlankPath_DoesNothing()
    {
        var project = ProjectWith(new ImageAsset { Id = "a", Path = "x.jpg" });

        await _service.DeletePhotoFromProjectAsync(project, "  ");

        project.ImageAssets.Should().HaveCount(1);
        _storage.Verify(s => s.UpdateProjectAsync(It.IsAny<CalendarProject>()), Times.Never);
    }
}
