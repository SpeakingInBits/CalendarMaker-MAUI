using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using SkiaSharp;

namespace CalendarMaker_MAUI.ViewModels;

/// <summary>
/// ViewModel for the designer page, handling calendar project editing and rendering coordination.
/// Separates business logic from UI concerns to improve testability and maintainability.
/// </summary>
public sealed partial class DesignerViewModel : ObservableObject
{
    #region Services

    private readonly IProjectStorageService _storage;
    private readonly IAssetService _assets;
    private readonly IPdfExportService _pdf;
    private readonly ILayoutCalculator _layoutCalculator;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IFilePickerService _filePickerService;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private CalendarProject? _project;

    [ObservableProperty]
    private int _pageIndex = -1; // -1=Front Cover, 0-11=Months, 12=Back Cover

    [ObservableProperty]
    private int _activeSlotIndex = 0;

    [ObservableProperty]
    private string _pageLabel = "Front Cover";

    [ObservableProperty]
    private double _splitRatio = 0.5;

    [ObservableProperty]
    private double _zoomValue = 1.0;

    [ObservableProperty]
    private bool _zoomSliderEnabled = false;

    [ObservableProperty]
    private bool _splitControlVisible = false;

    [ObservableProperty]
    private bool _borderlessControlVisible = false;

    [ObservableProperty]
    private bool _borderlessChecked = false;

    [ObservableProperty]
    private int _selectedPhotoLayoutIndex = 0;

    [ObservableProperty]
    private int _selectedStartMonthIndex = 0;

    [ObservableProperty]
    private int _selectedFirstDayOfWeekIndex = 0;

    [ObservableProperty]
    private bool _doubleSidedEnabled = false;

    [ObservableProperty]
    private string _yearText = DateTime.Now.Year.ToString();

    #endregion

    #region Cached Rendering Data

    /// <summary>
    /// Cached photo slots for the current page (used for hit testing and rendering)
    /// </summary>
    public List<SKRect> LastPhotoSlots { get; private set; } = new();

    /// <summary>
    /// Cached photo area rect for the current page
    /// </summary>
    public SKRect LastPhotoRect { get; private set; }

    /// <summary>
    /// Cached content rect for the current page
    /// </summary>
    public SKRect LastContentRect { get; private set; }

    #endregion

    #region Gesture State (for touch handling)

    public bool IsDragging { get; set; }
    public bool IsPointerDown { get; set; }
    public bool PressedOnAsset { get; set; }
    public SKPoint DragStartPagePoint { get; set; }
    public double StartPanX { get; set; }
    public double StartPanY { get; set; }
    public double StartZoom { get; set; }
    public float DragExcessX { get; set; }
    public float DragExcessY { get; set; }
    public DateTime LastTapAt { get; set; } = DateTime.MinValue;
    public SKPoint LastTapPoint { get; set; }

    #endregion

    #region Commands

    public ICommand NavigatePageCommand { get; }
    public ICommand ImportPhotosCommand { get; }
    public ICommand ShowPhotoSelectorCommand { get; }
    public ICommand FlipLayoutCommand { get; }
    public ICommand ExportCurrentPageCommand { get; }
    public ICommand ExportCoverCommand { get; }
    public ICommand ExportYearCommand { get; }
    public ICommand ExportDoubleSidedCommand { get; }
    public ICommand ResetSplitCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand SaveProjectCommand { get; }

    #endregion

    #region Static Data

    public ObservableCollection<string> MonthNames { get; } = new(
        Enumerable.Range(1, 12).Select(i => new DateTime(2000, i, 1).ToString("MMMM"))
    );

    public ObservableCollection<string> DayOfWeekNames { get; } = new(
        Enum.GetNames(typeof(DayOfWeek))
    );

    public ObservableCollection<string> PhotoLayoutNames { get; } = new()
    {
        "Single",
 "Two Vertical",
        "Grid 2×2",
        "Two Horizontal",
     "Three Left Stack",
        "Three Right Stack"
    };

    #endregion

    public DesignerViewModel(
        IProjectStorageService storage,
 IAssetService assets,
      IPdfExportService pdf,
        ILayoutCalculator layoutCalculator,
        IDialogService dialogService,
        INavigationService navigationService,
        IFilePickerService filePickerService)
    {
        _storage = storage;
        _assets = assets;
        _pdf = pdf;
        _layoutCalculator = layoutCalculator;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _filePickerService = filePickerService;

        // Initialize commands
        NavigatePageCommand = new RelayCommand<int>(NavigatePage);
        ImportPhotosCommand = new AsyncRelayCommand(ImportPhotosAsync);
        ShowPhotoSelectorCommand = new AsyncRelayCommand(ShowPhotoSelectorAsync);
        FlipLayoutCommand = new RelayCommand(FlipLayout);
        ExportCurrentPageCommand = new AsyncRelayCommand(ExportCurrentPageAsync);
        ExportCoverCommand = new AsyncRelayCommand(ExportCoverAsync);
        ExportYearCommand = new AsyncRelayCommand(ExportYearAsync);
        ExportDoubleSidedCommand = new AsyncRelayCommand(ExportDoubleSidedAsync);
        ResetSplitCommand = new RelayCommand(ResetSplit);
        ResetZoomCommand = new RelayCommand(ResetZoom);
        GoBackCommand = new AsyncRelayCommand(_navigationService.GoBackAsync);
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
    }

    #region Public Methods

    /// <summary>
    /// Loads a project by ID.
    /// </summary>
    public async Task LoadProjectAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return;
        }

        var projects = await _storage.GetProjectsAsync();
        Project = projects.FirstOrDefault(p => p.Id == projectId);

        if (Project != null)
        {
            // Sync UI properties from project
            SplitRatio = Project.LayoutSpec.SplitRatio;
            YearText = Project.Year.ToString();
            SelectedStartMonthIndex = Math.Clamp(Project.StartMonth - 1, 0, 11);
            SelectedFirstDayOfWeekIndex = (int)Project.FirstDayOfWeek;
            DoubleSidedEnabled = Project.EnableDoubleSided;
            ActiveSlotIndex = 0;

            UpdatePageLabel();
            SyncPhotoLayoutPicker();
            SyncZoomUI();
        }
    }

    /// <summary>
    /// Gets the currently active image asset (if any).
    /// </summary>
    public ImageAsset? GetActiveAsset()
    {
        if (Project == null)
        {
            return null;
        }

        if (PageIndex == -1)
        {
            return Project.ImageAssets.FirstOrDefault(a =>
                a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == ActiveSlotIndex);
        }
        else if (PageIndex == 12)
        {
            return Project.ImageAssets.FirstOrDefault(a =>
    a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == ActiveSlotIndex);
        }
        else
        {
            return Project.ImageAssets
               .Where(a => a.Role == "monthPhoto" && a.MonthIndex == PageIndex && (a.SlotIndex ?? 0) == ActiveSlotIndex)
                    .OrderBy(a => a.Order)
               .FirstOrDefault();
        }
    }

    /// <summary>
    /// Updates cached rendering data for the current page.
    /// Called by the view after layout calculations.
    /// </summary>
    public void UpdateCachedRenderingData(List<SKRect> photoSlots, SKRect photoRect, SKRect contentRect)
    {
        LastPhotoSlots = photoSlots;
        LastPhotoRect = photoRect;
        LastContentRect = contentRect;
    }

    /// <summary>
    /// Gets the current photo layout for the active page.
    /// </summary>
    public PhotoLayout GetCurrentPhotoLayout()
    {
        if (Project == null)
        {
            return PhotoLayout.Single;
        }

        if (PageIndex == -1)
        {
            return Project.FrontCoverPhotoLayout;
        }
        else if (PageIndex == 12)
        {
            return Project.BackCoverPhotoLayout;
        }
        else if (PageIndex >= -2 && PageIndex <= 11)
        {
            return Project.MonthPhotoLayouts.TryGetValue(PageIndex, out var layout)
                        ? layout
                 : Project.LayoutSpec.PhotoLayout;
        }

        return PhotoLayout.Single;
    }

    #endregion

    #region Command Implementations

    private void NavigatePage(int direction)
    {
        PageIndex += direction;

        // Determine page range based on double-sided mode
        int minPage = Project?.EnableDoubleSided == true ? -2 : -1;

        if (PageIndex < minPage)
        {
            PageIndex = 12;
        }

        if (PageIndex > 12)
        {
            PageIndex = minPage;
        }

        ActiveSlotIndex = 0;
        UpdatePageLabel();
        SyncZoomUI();
        SyncPhotoLayoutPicker();
    }

    private async Task ImportPhotosAsync()
    {
        if (Project == null)
        {
            return;
        }

        var results = await _filePickerService.PickMultipleFilesAsync(new Microsoft.Maui.Storage.PickOptions
        {
            PickerTitle = "Select photos to add to project",
            FileTypes = Microsoft.Maui.Storage.FilePickerFileType.Images
        });

        if (results == null || !results.Any())
        {
            return;
        }

        foreach (var result in results)
        {
            await _assets.ImportProjectPhotoAsync(Project, result);
        }
    }

    private async Task ShowPhotoSelectorAsync()
    {
        if (Project == null)
        {
            return;
        }

        var allPhotos = await _assets.GetAllPhotosAsync(Project);
        string slotDescription = GetSlotDescription();

        var modal = new Views.PhotoSelectorModal(allPhotos, slotDescription);

        modal.PhotoSelected += async (_, args) =>
     {
         if (Project == null)
         {
             return;
         }

         var selected = args.SelectedAsset;
         string role;
         int? monthIndex = null;
         int? slotIndex = ActiveSlotIndex;

         if (PageIndex == -1)
         {
             role = "coverPhoto";
         }
         else if (PageIndex == 12)
         {
             role = "backCoverPhoto";
         }
         else
         {
             role = "monthPhoto";
             monthIndex = PageIndex;
         }

         await _assets.AssignPhotoToSlotAsync(Project, selected.Id, monthIndex ?? 0, slotIndex, role);
         SyncZoomUI();
         await _navigationService.PopModalAsync();
     };

        modal.RemoveRequested += async (_, __) =>
     {
         if (Project == null)
         {
             return;
         }

         await RemovePhotoFromActiveSlotAsync();
         await _navigationService.PopModalAsync();
     };

        modal.Cancelled += async (_, __) =>
        {
            await _navigationService.PopModalAsync();
        };

        await _navigationService.PushModalAsync(modal, true);
    }

    private void FlipLayout()
    {
        if (Project == null)
        {
            return;
        }

        var placement = Project.LayoutSpec.Placement;
        Project.LayoutSpec.Placement = placement switch
        {
            LayoutPlacement.PhotoLeftCalendarRight => LayoutPlacement.PhotoRightCalendarLeft,
            LayoutPlacement.PhotoRightCalendarLeft => LayoutPlacement.PhotoLeftCalendarRight,
            LayoutPlacement.PhotoTopCalendarBottom => LayoutPlacement.PhotoBottomCalendarTop,
            LayoutPlacement.PhotoBottomCalendarTop => LayoutPlacement.PhotoTopCalendarBottom,
            _ => placement
        };

        _ = _storage.UpdateProjectAsync(Project);
    }

    private async Task ExportCurrentPageAsync()
    {
        if (Project == null)
        {
            return;
        }

        byte[] bytes;
        string fileName;

        if (PageIndex == -1)
        {
            bytes = await _pdf.ExportCoverAsync(Project);
            fileName = $"Calendar_{Project.Year}_FrontCover.pdf";
        }
        else if (PageIndex == 12)
        {
            bytes = await _pdf.ExportBackCoverAsync(Project);
            fileName = $"Calendar_{Project.Year}_BackCover.pdf";
        }
        else
        {
            bytes = await _pdf.ExportMonthAsync(Project, PageIndex);
            var month = ((Project.StartMonth - 1 + PageIndex) % 12) + 1;
            fileName = $"Calendar_{Project.Year}_{month:00}.pdf";
        }

        await SaveBytesAsync(fileName, bytes);
    }

    private async Task ExportCoverAsync()
    {
        if (Project == null)
        {
            return;
        }

        var bytes = await _pdf.ExportCoverAsync(Project);
        var fileName = $"Calendar_{Project.Year}_Cover.pdf";
        await SaveBytesAsync(fileName, bytes);
    }

    private async Task ExportYearAsync()
    {
        if (Project == null)
        {
            return;
        }

        // TODO: Implement progress modal
        var bytes = await _pdf.ExportYearAsync(Project, includeCover: true);
        var fileName = $"Calendar_{Project.Year}_FullYear.pdf";
        await SaveBytesAsync(fileName, bytes);
    }

    private async Task ExportDoubleSidedAsync()
    {
        if (Project == null)
        {
            return;
        }

        // TODO: Implement progress modal
        var bytes = await _pdf.ExportDoubleSidedAsync(Project);
        var fileName = $"Calendar_{Project.Year}_DoubleSided.pdf";
        await SaveBytesAsync(fileName, bytes);
    }

    private void ResetSplit()
    {
        if (Project == null)
        {
            return;
        }

        Project.LayoutSpec.SplitRatio = 0.5;
        SplitRatio = 0.5;
        _ = _storage.UpdateProjectAsync(Project);
    }

    private void ResetZoom()
    {
        var asset = GetActiveAsset();
        if (asset == null || Project == null)
        {
            return;
        }

        asset.Zoom = 1;
        asset.PanX = 0;
        asset.PanY = 0;
        ZoomValue = 1;
        _ = _storage.UpdateProjectAsync(Project);
    }

    private async Task SaveProjectAsync()
    {
        if (Project != null)
        {
            await _storage.UpdateProjectAsync(Project);
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSplitRatioChanged(double value)
    {
        if (Project != null)
        {
            Project.LayoutSpec.SplitRatio = value;
            _ = _storage.UpdateProjectAsync(Project);
        }
    }

    partial void OnZoomValueChanged(double value)
    {
        var asset = GetActiveAsset();
        if (asset != null && Project != null)
        {
            asset.Zoom = Math.Clamp(value, 0.5, 3.0);
            _ = _storage.UpdateProjectAsync(Project);
        }
    }

    partial void OnSelectedPhotoLayoutIndexChanged(int value)
    {
        if (Project == null || value < 0)
        {
            return;
        }

        var layout = value switch
        {
            1 => PhotoLayout.TwoVerticalSplit,
            2 => PhotoLayout.Grid2x2,
            3 => PhotoLayout.TwoHorizontalStack,
            4 => PhotoLayout.ThreeLeftStack,
            5 => PhotoLayout.ThreeRightStack,
            _ => PhotoLayout.Single
        };

        if (PageIndex == -1)
        {
            Project.FrontCoverPhotoLayout = layout;
        }
        else if (PageIndex == 12)
        {
            Project.BackCoverPhotoLayout = layout;
        }
        else if (PageIndex >= -2 && PageIndex <= 11)
        {
            Project.MonthPhotoLayouts[PageIndex] = layout;
        }

        _ = _storage.UpdateProjectAsync(Project);
        ActiveSlotIndex = 0;
        SyncZoomUI();
    }

    partial void OnSelectedStartMonthIndexChanged(int value)
    {
        if (Project != null)
        {
            Project.StartMonth = value + 1;
            PageIndex = -1;
            ActiveSlotIndex = 0;
            SyncZoomUI();
            UpdatePageLabel();
            _ = _storage.UpdateProjectAsync(Project);
        }
    }

    partial void OnSelectedFirstDayOfWeekIndexChanged(int value)
    {
        if (Project != null)
        {
            Project.FirstDayOfWeek = (DayOfWeek)value;
            _ = _storage.UpdateProjectAsync(Project);
        }
    }

    partial void OnDoubleSidedEnabledChanged(bool value)
    {
        if (Project == null)
        {
            return;
        }

        // If enabling and start month is not January, need to confirm with user
        if (value && Project.StartMonth != 1)
        {
            // This will be handled in the View with a dialog
            return;
        }

        Project.EnableDoubleSided = value;
        _ = _storage.UpdateProjectAsync(Project);

        if (!value && PageIndex == -2)
        {
            PageIndex = -1;
        }

        UpdatePageLabel();
    }

    partial void OnYearTextChanged(string value)
    {
        if (Project != null && int.TryParse(value, out var year) && year >= 1900 && year <= 2100)
        {
            Project.Year = year;
            UpdatePageLabel();
            _ = _storage.UpdateProjectAsync(Project);
        }
    }

    partial void OnBorderlessCheckedChanged(bool value)
    {
        if (Project == null)
        {
            return;
        }

        if (PageIndex == -1)
        {
            Project.CoverSpec.BorderlessFrontCover = value;
        }
        else if (PageIndex == 12)
        {
            Project.CoverSpec.BorderlessBackCover = value;
        }

        _ = _storage.UpdateProjectAsync(Project);
    }

    #endregion

    #region Private Helper Methods

    private void UpdatePageLabel()
    {
        if (Project == null)
        {
            PageLabel = string.Empty;
            return;
        }

        if (PageIndex == -2)
        {
            var prevYear = Project.Year - 1;
            PageLabel = $"December {prevYear} (Prev Year)";
        }
        else if (PageIndex == -1)
        {
            PageLabel = "Front Cover";
        }
        else if (PageIndex == 12)
        {
            PageLabel = "Back Cover";
        }
        else
        {
            var month = ((Project.StartMonth - 1 + PageIndex) % 12) + 1;
            var year = Project.Year + (Project.StartMonth - 1 + PageIndex) / 12;
            PageLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        UpdateControlVisibility();
        SyncPhotoLayoutPicker();
    }

    private void UpdateControlVisibility()
    {
        bool isCoverOrPrevDec = PageIndex == -2 || PageIndex == -1 || PageIndex == 12;
        SplitControlVisible = !isCoverOrPrevDec;

        bool isCoverPage = PageIndex == -1 || PageIndex == 12;
        BorderlessControlVisible = isCoverPage;

        if (Project != null && isCoverPage)
        {
            BorderlessChecked = PageIndex == -1
                   ? Project.CoverSpec.BorderlessFrontCover
                  : Project.CoverSpec.BorderlessBackCover;
        }
    }

    private void SyncPhotoLayoutPicker()
    {
        var layout = GetCurrentPhotoLayout();

        SelectedPhotoLayoutIndex = layout switch
        {
            PhotoLayout.TwoVerticalSplit => 1,
            PhotoLayout.Grid2x2 => 2,
            PhotoLayout.TwoHorizontalStack => 3,
            PhotoLayout.ThreeLeftStack => 4,
            PhotoLayout.ThreeRightStack => 5,
            _ => 0
        };
    }

    private void SyncZoomUI()
    {
        var asset = GetActiveAsset();
        ZoomSliderEnabled = asset != null;

        if (asset != null)
        {
            ZoomValue = Math.Clamp(asset.Zoom, 0.5, 3.0);
        }
    }

    private string GetSlotDescription()
    {
        if (Project == null)
        {
            return string.Empty;
        }

        if (PageIndex == -2)
        {
            var prevYear = Project.Year - 1;
            return $"December {prevYear} (Prev Year) - Slot {ActiveSlotIndex + 1}";
        }
        else if (PageIndex == -1)
        {
            return $"Front Cover - Slot {ActiveSlotIndex + 1}";
        }
        else if (PageIndex == 12)
        {
            return $"Back Cover - Slot {ActiveSlotIndex + 1}";
        }
        else
        {
            var month = ((Project.StartMonth - 1 + PageIndex) % 12) + 1;
            var year = Project.Year + (Project.StartMonth - 1 + PageIndex) / 12;
            var monthName = new DateTime(year, month, 1).ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture);
            return $"{monthName} - Slot {ActiveSlotIndex + 1}";
        }
    }

    private async Task RemovePhotoFromActiveSlotAsync()
    {
        if (Project == null)
        {
            return;
        }

        if (PageIndex == -1)
        {
            var existingPhoto = Project.ImageAssets.FirstOrDefault(a =>
                 a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == ActiveSlotIndex);
            if (existingPhoto != null)
            {
                Project.ImageAssets.Remove(existingPhoto);
                await _storage.UpdateProjectAsync(Project);
            }
        }
        else if (PageIndex == 12)
        {
            var existingPhoto = Project.ImageAssets.FirstOrDefault(a =>
         a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == ActiveSlotIndex);
            if (existingPhoto != null)
            {
                Project.ImageAssets.Remove(existingPhoto);
                await _storage.UpdateProjectAsync(Project);
            }
        }
        else
        {
            await _assets.RemovePhotoFromSlotAsync(Project, PageIndex, ActiveSlotIndex, "monthPhoto");
        }
    }

    private async Task SaveBytesAsync(string suggestedFileName, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = await _filePickerService.SaveFileAsync(suggestedFileName, stream, default);

        if (!result.IsSuccessful)
        {
            await _dialogService.ShowAlertAsync("Save Failed", result.Exception?.Message ?? "Unknown error");
        }
    }

    #endregion
}
