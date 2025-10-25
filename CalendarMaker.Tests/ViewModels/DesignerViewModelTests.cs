using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using CalendarMaker_MAUI.ViewModels;
using FluentAssertions;
using Moq;
using SkiaSharp;

namespace CalendarMaker.Tests.ViewModels;

/// <summary>
/// Comprehensive tests for DesignerViewModel covering all commands, properties, and business logic.
/// </summary>
public class DesignerViewModelTests
{
    private readonly Mock<IProjectStorageService> _mockStorage;
    private readonly Mock<IAssetService> _mockAssets;
    private readonly Mock<IPdfExportService> _mockPdf;
    private readonly Mock<ILayoutCalculator> _mockLayoutCalculator;
    private readonly Mock<IDialogService> _mockDialog;
    private readonly Mock<INavigationService> _mockNavigation;
    private readonly Mock<IFilePickerService> _mockFilePicker;
    private readonly DesignerViewModel _viewModel;

    public DesignerViewModelTests()
    {
        // Setup mocks
        _mockStorage = new Mock<IProjectStorageService>();
        _mockAssets = new Mock<IAssetService>();
        _mockPdf = new Mock<IPdfExportService>();
        _mockLayoutCalculator = new Mock<ILayoutCalculator>();
        _mockDialog = new Mock<IDialogService>();
        _mockNavigation = new Mock<INavigationService>();
        _mockFilePicker = new Mock<IFilePickerService>();

        // Create ViewModel with mocked dependencies
        _viewModel = new DesignerViewModel(
            _mockStorage.Object,
            _mockAssets.Object,
            _mockPdf.Object,
            _mockLayoutCalculator.Object,
            _mockDialog.Object,
            _mockNavigation.Object,
            _mockFilePicker.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Assert
        _viewModel.PageIndex.Should().Be(-1); // Front cover
        _viewModel.ActiveSlotIndex.Should().Be(0);
        _viewModel.PageLabel.Should().Be("Front Cover");
        _viewModel.SplitRatio.Should().Be(0.5);
        _viewModel.ZoomValue.Should().Be(1.0);
        _viewModel.ZoomSliderEnabled.Should().BeFalse();
        _viewModel.DoubleSidedEnabled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldInitializeCommands()
    {
        // Assert - All commands should be non-null
        _viewModel.NavigatePageCommand.Should().NotBeNull();
        _viewModel.ImportPhotosCommand.Should().NotBeNull();
        _viewModel.ShowPhotoSelectorCommand.Should().NotBeNull();
        _viewModel.FlipLayoutCommand.Should().NotBeNull();
        _viewModel.ExportCurrentPageCommand.Should().NotBeNull();
        _viewModel.ExportCoverCommand.Should().NotBeNull();
        _viewModel.ExportYearCommand.Should().NotBeNull();
        _viewModel.ExportDoubleSidedCommand.Should().NotBeNull();
        _viewModel.ResetSplitCommand.Should().NotBeNull();
        _viewModel.ResetZoomCommand.Should().NotBeNull();
        _viewModel.GoBackCommand.Should().NotBeNull();
        _viewModel.SaveProjectCommand.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Assert
        _viewModel.MonthNames.Should().HaveCount(12);
        _viewModel.DayOfWeekNames.Should().HaveCount(7);
        _viewModel.PhotoLayoutNames.Should().HaveCount(6);
    }

    #endregion

    #region LoadProjectAsync Tests

    [Fact]
    public async Task LoadProjectAsync_WithValidProjectId_ShouldLoadProject()
    {
        // Arrange
        var projectId = "test-project-123";
        var testProject = CreateTestProject(projectId);
        var projects = new List<CalendarProject> { testProject };

        _mockStorage.Setup(x => x.GetProjectsAsync())
 .ReturnsAsync(projects);

        // Act
        await _viewModel.LoadProjectAsync(projectId);

        // Assert
        _viewModel.Project.Should().NotBeNull();
        _viewModel.Project.Id.Should().Be(projectId);
        _viewModel.YearText.Should().Be(testProject.Year.ToString());
        _viewModel.SelectedStartMonthIndex.Should().Be(testProject.StartMonth - 1);
    }

    [Fact]
    public async Task LoadProjectAsync_WithEmptyProjectId_ShouldNotLoadProject()
    {
        // Act
        await _viewModel.LoadProjectAsync(string.Empty);

        // Assert
        _viewModel.Project.Should().BeNull();
        _mockStorage.Verify(x => x.GetProjectsAsync(), Times.Never);
    }

    [Fact]
    public async Task LoadProjectAsync_WithNonExistentProject_ShouldSetProjectToNull()
    {
        // Arrange
        _mockStorage.Setup(x => x.GetProjectsAsync())
    .ReturnsAsync(new List<CalendarProject>());

        // Act
        await _viewModel.LoadProjectAsync("non-existent");

        // Assert
        _viewModel.Project.Should().BeNull();
    }

    #endregion

    #region NavigatePage Tests

    [Fact]
    public void NavigatePage_Forward_ShouldIncrementPageIndex()
    {
        // Arrange
        _viewModel.PageIndex = 0; // January

        // Act
        _viewModel.NavigatePageCommand.Execute(1);

        // Assert
        _viewModel.PageIndex.Should().Be(1); // February
    }

    [Fact]
    public void NavigatePage_Backward_ShouldDecrementPageIndex()
    {
        // Arrange
        _viewModel.PageIndex = 1; // February

        // Act
        _viewModel.NavigatePageCommand.Execute(-1);

        // Assert
        _viewModel.PageIndex.Should().Be(0); // January
    }

    [Fact]
    public void NavigatePage_ForwardFromBackCover_ShouldWrapToFrontCover()
    {
        // Arrange
        _viewModel.PageIndex = 12; // Back cover

        // Act
        _viewModel.NavigatePageCommand.Execute(1);

        // Assert
        _viewModel.PageIndex.Should().Be(-1); // Front cover
    }

    [Fact]
    public void NavigatePage_BackwardFromFrontCover_ShouldWrapToBackCover()
    {
        // Arrange
        _viewModel.PageIndex = -1; // Front cover

        // Act
        _viewModel.NavigatePageCommand.Execute(-1);

        // Assert
        _viewModel.PageIndex.Should().Be(12); // Back cover
    }

    [Fact]
    public void NavigatePage_WithDoubleSidedEnabled_ShouldAllowPreviousDecember()
    {
        // Arrange
        var project = CreateTestProject();
        project.EnableDoubleSided = true;
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = -1; // Front cover

        // Act
        _viewModel.NavigatePageCommand.Execute(-1);

        // Assert
        _viewModel.PageIndex.Should().Be(-2); // Should go to previous December when double-sided is enabled
    }

    [Fact]
    public void NavigatePage_ShouldResetActiveSlotIndex()
    {
        // Arrange
        _viewModel.ActiveSlotIndex = 3;
        _viewModel.PageIndex = 0;

        // Act
        _viewModel.NavigatePageCommand.Execute(1);

        // Assert
        _viewModel.ActiveSlotIndex.Should().Be(0);
    }

    #endregion

    #region FlipLayout Tests

    [Fact]
    public void FlipLayout_PhotoLeftCalendarRight_ShouldFlipToPhotoRightCalendarLeft()
    {
        // Arrange
        var project = CreateTestProject();
        project.LayoutSpec.Placement = LayoutPlacement.PhotoLeftCalendarRight;
        SetupProjectInViewModel(project);

        // Act
        _viewModel.FlipLayoutCommand.Execute(null);

        // Assert
        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoRightCalendarLeft);
    }

    [Fact]
    public void FlipLayout_PhotoTopCalendarBottom_ShouldFlipToPhotoBottomCalendarTop()
    {
        // Arrange
        var project = CreateTestProject();
        project.LayoutSpec.Placement = LayoutPlacement.PhotoTopCalendarBottom;
        SetupProjectInViewModel(project);

        // Act
        _viewModel.FlipLayoutCommand.Execute(null);

        // Assert
        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoBottomCalendarTop);
    }

    [Fact]
    public void FlipLayout_ShouldSaveProject()
    {
        // Arrange
        var project = CreateTestProject();
        SetupProjectInViewModel(project);

        // Act
        _viewModel.FlipLayoutCommand.Execute(null);

        // Assert
        _mockStorage.Verify(x => x.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    #endregion

    #region ResetSplit Tests

    [Fact]
    public void ResetSplit_ShouldSetSplitRatioToHalf()
    {
        // Arrange
        var project = CreateTestProject();
        SetupProjectInViewModel(project);
        _viewModel.SplitRatio = 0.7;

        // Act
        _viewModel.ResetSplitCommand.Execute(null);

        // Assert
        _viewModel.SplitRatio.Should().Be(0.5);
        project.LayoutSpec.SplitRatio.Should().Be(0.5);
    }

    [Fact]
    public void ResetSplit_ShouldSaveProject()
    {
        // Arrange
        var project = CreateTestProject();
        SetupProjectInViewModel(project);

        // Act
        _viewModel.ResetSplitCommand.Execute(null);

        // Assert
        _mockStorage.Verify(x => x.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    #endregion

    #region ResetZoom Tests

    [Fact]
    public void ResetZoom_WithActiveAsset_ShouldResetZoomToOne()
    {
        // Arrange
        var project = CreateTestProject();
        var asset = new ImageAsset
        {
            Id = "asset-1",
            Role = "coverPhoto",
            SlotIndex = 0,
            Zoom = 2.5,
            PanX = 0.5,
            PanY = -0.3
        };
        project.ImageAssets.Add(asset);
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = -1; // Front cover
        _viewModel.ActiveSlotIndex = 0;

        // Act
        _viewModel.ResetZoomCommand.Execute(null);

        // Assert
        asset.Zoom.Should().Be(1.0);
        asset.PanX.Should().Be(0.0);
        asset.PanY.Should().Be(0.0);
        _viewModel.ZoomValue.Should().Be(1.0);
    }

    #endregion

    #region GetActiveAsset Tests

    [Fact]
    public void GetActiveAsset_OnFrontCover_ShouldReturnCoverPhoto()
    {
        // Arrange
        var project = CreateTestProject();
        var coverAsset = new ImageAsset
        {
            Id = "cover-1",
            Role = "coverPhoto",
            SlotIndex = 0,
            Path = "test.jpg"
        };
        project.ImageAssets.Add(coverAsset);
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = -1; // Front cover
        _viewModel.ActiveSlotIndex = 0;

        // Act
        var result = _viewModel.GetActiveAsset();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("cover-1");
        result.Role.Should().Be("coverPhoto");
    }

    [Fact]
    public void GetActiveAsset_OnBackCover_ShouldReturnBackCoverPhoto()
    {
        // Arrange
        var project = CreateTestProject();
        var backCoverAsset = new ImageAsset
        {
            Id = "back-1",
            Role = "backCoverPhoto",
            SlotIndex = 0,
            Path = "test.jpg"
        };
        project.ImageAssets.Add(backCoverAsset);
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 12; // Back cover
        _viewModel.ActiveSlotIndex = 0;

        // Act
        var result = _viewModel.GetActiveAsset();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("back-1");
        result.Role.Should().Be("backCoverPhoto");
    }

    [Fact]
    public void GetActiveAsset_OnMonthPage_ShouldReturnMonthPhoto()
    {
        // Arrange
        var project = CreateTestProject();
        var monthAsset = new ImageAsset
        {
            Id = "month-1",
            Role = "monthPhoto",
            MonthIndex = 0,
            SlotIndex = 0,
            Path = "test.jpg"
        };
        project.ImageAssets.Add(monthAsset);
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 0; // January
        _viewModel.ActiveSlotIndex = 0;

        // Act
        var result = _viewModel.GetActiveAsset();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("month-1");
        result.Role.Should().Be("monthPhoto");
    }

    [Fact]
    public void GetActiveAsset_WithNoProject_ShouldReturnNull()
    {
        // Arrange
        _viewModel.Project = null;

        // Act
        var result = _viewModel.GetActiveAsset();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentPhotoLayout Tests

    [Fact]
    public void GetCurrentPhotoLayout_OnFrontCover_ShouldReturnFrontCoverLayout()
    {
        // Arrange
        var project = CreateTestProject();
        project.FrontCoverPhotoLayout = PhotoLayout.Grid2x2;
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = -1;

        // Act
        var result = _viewModel.GetCurrentPhotoLayout();

        // Assert
        result.Should().Be(PhotoLayout.Grid2x2);
    }

    [Fact]
    public void GetCurrentPhotoLayout_OnBackCover_ShouldReturnBackCoverLayout()
    {
        // Arrange
        var project = CreateTestProject();
        project.BackCoverPhotoLayout = PhotoLayout.TwoVerticalSplit;
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 12;

        // Act
        var result = _viewModel.GetCurrentPhotoLayout();

        // Assert
        result.Should().Be(PhotoLayout.TwoVerticalSplit);
    }

    [Fact]
    public void GetCurrentPhotoLayout_OnMonthWithOverride_ShouldReturnOverrideLayout()
    {
        // Arrange
        var project = CreateTestProject();
        project.LayoutSpec.PhotoLayout = PhotoLayout.Single;
        project.MonthPhotoLayouts[0] = PhotoLayout.ThreeLeftStack;
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 0;

        // Act
        var result = _viewModel.GetCurrentPhotoLayout();

        // Assert
        result.Should().Be(PhotoLayout.ThreeLeftStack);
    }

    [Fact]
    public void GetCurrentPhotoLayout_OnMonthWithoutOverride_ShouldReturnDefaultLayout()
    {
        // Arrange
        var project = CreateTestProject();
        project.LayoutSpec.PhotoLayout = PhotoLayout.TwoHorizontalStack;
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 5;

        // Act
        var result = _viewModel.GetCurrentPhotoLayout();

        // Assert
        result.Should().Be(PhotoLayout.TwoHorizontalStack);
    }

    #endregion

    #region UpdateCachedRenderingData Tests

    [Fact]
    public void UpdateCachedRenderingData_ShouldUpdateAllCachedData()
    {
        // Arrange
        var photoSlots = new List<SKRect>
  {
   new SKRect(0, 0, 100, 100),
  new SKRect(100, 0, 200, 100)
        };
        var photoRect = new SKRect(0, 0, 200, 100);
        var contentRect = new SKRect(0, 0, 400, 600);

        // Act
        _viewModel.UpdateCachedRenderingData(photoSlots, photoRect, contentRect);

        // Assert
        _viewModel.LastPhotoSlots.Should().BeEquivalentTo(photoSlots);
        _viewModel.LastPhotoRect.Should().Be(photoRect);
        _viewModel.LastContentRect.Should().Be(contentRect);
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void SplitRatio_WhenChanged_ShouldUpdateProjectAndSave()
    {
        // Arrange
        var project = CreateTestProject();
        SetupProjectInViewModel(project);
        var newRatio = 0.6;

        // Act
        _viewModel.SplitRatio = newRatio;

        // Assert
        project.LayoutSpec.SplitRatio.Should().Be(newRatio);
        _mockStorage.Verify(x => x.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    [Fact]
    public void ZoomValue_WhenChanged_ShouldUpdateActiveAssetAndSave()
    {
        // Arrange
        var project = CreateTestProject();
        var asset = new ImageAsset
        {
            Id = "asset-1",
            Role = "coverPhoto",
            SlotIndex = 0,
            Zoom = 1.0
        };
        project.ImageAssets.Add(asset);
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = -1;
        _viewModel.ActiveSlotIndex = 0;

        // Act
        _viewModel.ZoomValue = 1.5;

        // Assert
        asset.Zoom.Should().Be(1.5);
        _mockStorage.Verify(x => x.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    [Fact]
    public void SelectedPhotoLayoutIndex_WhenChanged_ShouldApplyCorrectLayout()
    {
        // Arrange
        var project = CreateTestProject();
        SetupProjectInViewModel(project);
        _viewModel.PageIndex = 0; // Month page

        // Act
        _viewModel.SelectedPhotoLayoutIndex = 2; // Grid2x2

        // Assert
        project.MonthPhotoLayouts[0].Should().Be(PhotoLayout.Grid2x2);
        _mockStorage.Verify(x => x.UpdateProjectAsync(project), Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    private CalendarProject CreateTestProject(string? id = null)
    {
        return new CalendarProject
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = "Test Calendar",
            Year = 2025,
            StartMonth = 1,
            FirstDayOfWeek = DayOfWeek.Sunday,
            PageSpec = new PageSpec { Size = PageSize.Letter, Orientation = PageOrientation.Portrait },
            Margins = new Margins { LeftPt = 36, RightPt = 36, TopPt = 36, BottomPt = 36 },
            Theme = new ThemeSpec(),
            LayoutSpec = new LayoutSpec
            {
                Placement = LayoutPlacement.PhotoTopCalendarBottom,
                SplitRatio = 0.5,
                PhotoLayout = PhotoLayout.Single
            },
            CoverSpec = new CoverSpec(),
            ImageAssets = new List<ImageAsset>(),
            MonthPhotoLayouts = new Dictionary<int, PhotoLayout>(),
            FrontCoverPhotoLayout = PhotoLayout.Single,
            BackCoverPhotoLayout = PhotoLayout.Single,
            EnableDoubleSided = false
        };
    }

    private void SetupProjectInViewModel(CalendarProject project)
    {
        var projects = new List<CalendarProject> { project };
        _mockStorage.Setup(x => x.GetProjectsAsync()).ReturnsAsync(projects);
        _viewModel.LoadProjectAsync(project.Id).Wait();
    }

    #endregion
}
