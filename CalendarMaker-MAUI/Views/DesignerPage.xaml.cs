using System.Globalization;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace CalendarMaker_MAUI.Views;

[QueryProperty(nameof(ProjectId), "projectId")]
public partial class DesignerPage : ContentPage
{
    private CalendarProject? _project;
    private readonly ICalendarEngine _engine;
    private readonly IProjectStorageService _storage;
    private readonly IAssetService _assets;
    private readonly IPdfExportService _pdf;
    private readonly ILayoutCalculator _layoutCalculator;
    private readonly ICalendarRenderer _calendarRenderer;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IFilePickerService _filePickerService;
    private readonly SKCanvasView _canvas;

    private int _pageIndex; // -1=Front Cover, 0-11=Months, 12=Back Cover

    // gesture helpers, used to manage pan/zoom of active image
    private float _pageScale = 1f;
    private float _pageOffsetX, _pageOffsetY;
    private SKRect _lastPhotoRect;
    private SKRect _lastContentRect;
    private List<SKRect> _lastPhotoSlots = new();
    private bool _isDragging;
    private bool _isPointerDown;
    private bool _pressedOnAsset;
    private const float DragStartThreshold = 3f; // page-space pixels
    private SKPoint _dragStartPagePt;
    private double _startPanX, _startPanY, _startZoom;
    private float _dragExcessX, _dragExcessY;
    private bool _dragIsCover;
    private DateTime _lastTapAt = DateTime.MinValue;
    private SKPoint _lastTapPt;
    private int _activeSlotIndex = 0;

    public string? ProjectId { get; set; }

    public DesignerPage(
        ICalendarEngine engine,
        IProjectStorageService storage,
        IAssetService assets,
        IPdfExportService pdf,
        ILayoutCalculator layoutCalculator,
        ICalendarRenderer calendarRenderer,
        IDialogService dialogService,
     INavigationService navigationService,
        IFilePickerService filePickerService)
    {
        InitializeComponent();
        _engine = engine;
        _storage = storage;
        _assets = assets;
        _pdf = pdf;
        _layoutCalculator = layoutCalculator;
        _calendarRenderer = calendarRenderer;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _filePickerService = filePickerService;

        // Ensure CanvasHost exists before assigning
        _canvas = new SKCanvasView { IgnorePixelScaling = false };
        _canvas.PaintSurface += Canvas_PaintSurface;
        _canvas.EnableTouchEvents = true;
        _canvas.Touch += OnCanvasTouch;

        if (CanvasHost != null)
            CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await _navigationService.GoBackAsync();
        PrevBtn.Clicked += (_, __) => NavigatePage(-1);
        NextBtn.Clicked += (_, __) => NavigatePage(1);
        AddPhotoBtn.Clicked += async (_, __) => await ImportPhotosToProjectAsync();
        ExportBtn.Clicked += OnExportClicked;
        ExportCoverBtn.Clicked += OnExportCoverClicked;
        ExportYearBtn.Clicked += OnExportYearClicked;
        ExportDoubleSidedBtn.Clicked += OnExportDoubleSidedClicked;

        FlipBtn.Clicked += (_, __) => FlipLayout();
        BorderlessCheckBox.CheckedChanged += OnBorderlessChanged;
        SplitSlider.ValueChanged += (_, e) => { SplitValueLabel.Text = e.NewValue.ToString("P0"); if (_project != null) { _project.LayoutSpec.SplitRatio = e.NewValue; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        ZoomSlider.ValueChanged += (_, e) => { ZoomValueLabel.Text = $"{e.NewValue:F2}x"; UpdateAssetZoom(e.NewValue); };
        YearEntry.TextChanged += OnYearChanged;
        StartMonthPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.StartMonth = StartMonthPicker.SelectedIndex + 1; _pageIndex = -1; _activeSlotIndex = 0; SyncZoomUI(); UpdatePageLabel(); _ = _storage.UpdateProjectAsync(_project); _canvas.InvalidateSurface(); } };
        FirstDowPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.FirstDayOfWeek = (DayOfWeek)FirstDowPicker.SelectedIndex; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        DoubleSidedCheckBox.CheckedChanged += OnDoubleSidedChanged;

        _pageIndex = -1; // start with front cover
        PopulateStaticPickers();
    }

    private void NavigatePage(int direction)
    {
        _pageIndex += direction;
        // Determine page range based on double-sided mode
        // Double-sided: -2 (prev Dec) to 12 (back cover) = 15 pages
        // Normal: -1 (front cover) to 12 (back cover) = 14 pages
        int minPage = _project?.EnableDoubleSided == true ? -2 : -1;

        if (_pageIndex < minPage)
        {
            _pageIndex = 12;
        }

        if (_pageIndex > 12)
        {
            _pageIndex = minPage;
        }

        _activeSlotIndex = 0;
        SyncZoomUI();
        UpdatePageLabel();
        _canvas.InvalidateSurface();
    }

    private async Task ImportPhotosToProjectAsync()
    {
        if (_project == null) return;

        var results = await _filePickerService.PickMultipleFilesAsync(new PickOptions
        {
            PickerTitle = "Select photos to add to project",
            FileTypes = FilePickerFileType.Images
        });

        if (results == null || !results.Any()) return;

        foreach (var result in results)
        {
            await _assets.ImportProjectPhotoAsync(_project, result);
        }

        // Refresh the display
        _canvas.InvalidateSurface();
    }

    private async Task ShowPhotoSelectorAsync()
    {
        if (_project == null) return;

        // Get all photos to show both unassigned and assigned
        var allPhotos = await _assets.GetAllPhotosAsync(_project);

        string slotDescription;
        if (_pageIndex == -2)
        {
            // Previous year's December
            var prevYear = _project.Year - 1;
            slotDescription = $"December {prevYear} (Prev Year) - Slot {_activeSlotIndex + 1}";
        }
        else if (_pageIndex == -1)
        {
            slotDescription = $"Front Cover - Slot {_activeSlotIndex + 1}";
        }
        else if (_pageIndex == 12)
        {
            slotDescription = $"Back Cover - Slot {_activeSlotIndex + 1}";
        }
        else
        {
            var month = ((_project.StartMonth - 1 + _pageIndex) % 12) + 1;
            var year = _project.Year + (_project.StartMonth - 1 + _pageIndex) / 12;
            var monthName = new DateTime(year, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);
            slotDescription = $"{monthName} - Slot {_activeSlotIndex + 1}";
        }

        var modal = new PhotoSelectorModal(allPhotos, slotDescription);

        // Assign selected photo to the active target
        modal.PhotoSelected += async (_, args) =>
        {
            if (_project == null) return;
            var selected = args.SelectedAsset;

            string role;
            int? monthIndex = null;
            int? slotIndex = _activeSlotIndex;

            if (_pageIndex == -1)
            {
                role = "coverPhoto";
            }
            else if (_pageIndex == 12)
            {
                role = "backCoverPhoto";
            }
            else
            {
                role = "monthPhoto";
                monthIndex = _pageIndex; // This handles -2 for previous December
            }

            await _assets.AssignPhotoToSlotAsync(_project, selected.Id, monthIndex ?? 0, slotIndex, role);
            SyncZoomUI();
            _canvas.InvalidateSurface();
            try { await _navigationService.PopModalAsync(); } catch { }
        };

        // Remove any existing photo from the active target
        modal.RemoveRequested += async (_, __) =>
        {
            if (_project == null) return;

            if (_pageIndex == -1) // Front cover
            {
                var existingPhoto = _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == _activeSlotIndex);
                if (existingPhoto != null)
                {
                    _project.ImageAssets.Remove(existingPhoto);
                    await _storage.UpdateProjectAsync(_project);
                }
            }
            else if (_pageIndex == 12) // Back cover
            {
                var existingPhoto = _project.ImageAssets.FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == _activeSlotIndex);
                if (existingPhoto != null)
                {
                    _project.ImageAssets.Remove(existingPhoto);
                    await _storage.UpdateProjectAsync(_project);
                }
            }
            else // Month page (including -2 for previous December)
            {
                await _assets.RemovePhotoFromSlotAsync(_project, _pageIndex, _activeSlotIndex, "monthPhoto");
            }

            _canvas.InvalidateSurface();
            try { await _navigationService.PopModalAsync(); } catch { }
        };

        // Close without changes
        modal.Cancelled += async (_, __) =>
           {
               try { await _navigationService.PopModalAsync(); } catch { }
           };

        try
        {
            await _navigationService.PushModalAsync(modal, true);
        }
        catch
        {
            // Fallback if navigation service fails
            await Navigation.PushModalAsync(modal, true);
        }

    }

    private void PopulateStaticPickers()
    {
        StartMonthPicker.ItemsSource = Enumerable.Range(1, 12).Select(i => new DateTime(2000, i, 1).ToString("MMMM", CultureInfo.InvariantCulture)).ToList();
        FirstDowPicker.ItemsSource = Enum.GetNames(typeof(DayOfWeek));
        PhotoLayoutPicker.SelectedIndexChanged += (_, __) => ApplyPhotoLayoutSelection();
        if (PhotoLayoutPicker.SelectedIndex < 0)
            PhotoLayoutPicker.SelectedIndex = 0; // default Single
    }

    private void ApplyPhotoLayoutSelection()
    {
        if (_project == null) return;
        var selected = PhotoLayoutPicker.SelectedIndex;
        if (selected < 0) selected = 0;
        var layout = selected switch
        {
            1 => PhotoLayout.TwoVerticalSplit,
            2 => PhotoLayout.Grid2x2,
            3 => PhotoLayout.TwoHorizontalStack,
            4 => PhotoLayout.ThreeLeftStack,
            5 => PhotoLayout.ThreeRightStack,
            _ => PhotoLayout.Single
        };

        // Apply to current page
        if (_pageIndex == -1) // Front cover
        {
            _project.FrontCoverPhotoLayout = layout;
        }
        else if (_pageIndex == 12) // Back cover
        {
            _project.BackCoverPhotoLayout = layout;
        }
        else if (_pageIndex >= -2 && _pageIndex <= 11) // Month pages (including -2 for previous December)
        {
            _project.MonthPhotoLayouts[_pageIndex] = layout;
        }

        _ = _storage.UpdateProjectAsync(_project);
        _activeSlotIndex = 0;
        SyncZoomUI();
        _canvas.InvalidateSurface();
    }

    private void UpdatePageLabel()
    {
        if (_project == null) { MonthLabel.Text = string.Empty; return; }

        if (_pageIndex == -2)
        {
            // Previous year's December (only in double-sided mode)
            var prevYear = _project.Year - 1;
            MonthLabel.Text = $"December {prevYear} (Prev Year)";
            SyncPhotoLayoutPicker();
        }
        else if (_pageIndex == -1)
        {
            MonthLabel.Text = "Front Cover";
            SyncPhotoLayoutPicker();
        }
        else if (_pageIndex == 12)
        {
            MonthLabel.Text = "Back Cover";
            SyncPhotoLayoutPicker();
        }
        else
        {
            var month = ((_project.StartMonth - 1 + _pageIndex) % 12) + 1;
            var year = _project.Year + (_project.StartMonth - 1 + _pageIndex) / 12;
            MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            SyncPhotoLayoutPicker();
        }

        UpdateSplitControlVisibility();
    }

    private void UpdateSplitControlVisibility()
    {
        // Hide split control for cover pages and previous December
        bool isCoverOrPrevDec = _pageIndex == -2 || _pageIndex == -1 || _pageIndex == 12;
        SplitControlGrid.IsVisible = !isCoverOrPrevDec;

        // Show borderless option only on cover pages
        bool isCoverPage = _pageIndex == -1 || _pageIndex == 12;
        BorderlessControl.IsVisible = isCoverPage;

        // Sync borderless checkbox state
        if (_project != null && isCoverPage)
        {
            BorderlessCheckBox.CheckedChanged -= OnBorderlessChanged; // Temporarily remove handler
            BorderlessCheckBox.IsChecked = _pageIndex == -1
                   ? _project.CoverSpec.BorderlessFrontCover
          : _project.CoverSpec.BorderlessBackCover;
            BorderlessCheckBox.CheckedChanged += OnBorderlessChanged; // Re-add handler
        }
    }

    private void OnBorderlessChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_project == null) return;

        if (_pageIndex == -1)
        {
            _project.CoverSpec.BorderlessFrontCover = e.Value;
        }
        else if (_pageIndex == 12)
        {
            _project.CoverSpec.BorderlessBackCover = e.Value;
        }

        _ = _storage.UpdateProjectAsync(_project);
        _canvas.InvalidateSurface();
    }

    private void OnDoubleSidedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_project == null) return;

        // If enabling double-sided mode and start month is not January, warn and change it
        if (e.Value && _project.StartMonth != 1)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
         {
             bool proceed = await _dialogService.ShowConfirmAsync(
         "Change Start Month?",
            "Double-sided calendars require a January start month. Would you like to change the start month to January?",
        "Yes, Change to January",
           "Cancel");

             if (proceed)
             {
                 _project.StartMonth = 1;
                 StartMonthPicker.SelectedIndex = 0; // January is index 0
                 _project.EnableDoubleSided = true;
                 await _storage.UpdateProjectAsync(_project);

                 // Reset to front cover to show the change
                 _pageIndex = -1;
                 _activeSlotIndex = 0;
                 SyncZoomUI();
                 UpdatePageLabel();
                 _canvas.InvalidateSurface();
             }
             else
             {
                 // User cancelled, revert the checkbox
                 DoubleSidedCheckBox.CheckedChanged -= OnDoubleSidedChanged;
                 DoubleSidedCheckBox.IsChecked = false;
                 DoubleSidedCheckBox.CheckedChanged += OnDoubleSidedChanged;
             }
         });
            return;
        }

        _project.EnableDoubleSided = e.Value;
        _ = _storage.UpdateProjectAsync(_project);

        // Reset to front cover if we were on the previous December page and toggled off
        if (!e.Value && _pageIndex == -2)
        {
            _pageIndex = -1;
        }

        UpdatePageLabel();
        _canvas.InvalidateSurface();
    }

    private void SyncPhotoLayoutPicker()
    {
        if (_project == null) return;

        PhotoLayout layout;

        if (_pageIndex == -1) // Front cover
        {
            layout = _project.FrontCoverPhotoLayout;
        }
        else if (_pageIndex == 12) // Back cover
        {
            layout = _project.BackCoverPhotoLayout;
        }
        else if (_pageIndex >= -2 && _pageIndex <= 11) // Month pages (including -2 for previous December)
        {
            layout = _project.MonthPhotoLayouts.TryGetValue(_pageIndex, out var l)
               ? l
           : _project.LayoutSpec.PhotoLayout;
        }
        else
        {
            return;
        }

        var idx = layout switch
        {
            PhotoLayout.TwoVerticalSplit => 1,
            PhotoLayout.Grid2x2 => 2,
            PhotoLayout.TwoHorizontalStack => 3,
            PhotoLayout.ThreeLeftStack => 4,
            PhotoLayout.ThreeRightStack => 5,
            _ => 0
        };
        PhotoLayoutPicker.SelectedIndex = idx;
    }

#if WINDOWS
    private void OnCanvasKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_project == null) return;
        var asset = GetActiveAsset();
        if (asset == null) return;
        double step = 0.05; // pan step
        bool handled = false;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: asset.PanX = Math.Clamp(asset.PanX - step, -1, 1); handled = true; break;
            case Windows.System.VirtualKey.Right: asset.PanX = Math.Clamp(asset.PanX + step, -1, 1); handled = true; break;
            case Windows.System.VirtualKey.Up: asset.PanY = Math.Clamp(asset.PanY - step, -1, 1); handled = true; break;
            case Windows.System.VirtualKey.Down: asset.PanY = Math.Clamp(asset.PanY + step, -1, 1); handled = true; break;
        }
        if (handled)
        {
            SyncZoomUI();
            _storage.UpdateProjectAsync(_project);
            _canvas.InvalidateSurface();
            e.Handled = true;
        }
    }
#endif

    private void FlipLayout()
    {
        if (_project == null) return;
        var p = _project.LayoutSpec.Placement;
        _project.LayoutSpec.Placement = p switch
        {
            LayoutPlacement.PhotoLeftCalendarRight => LayoutPlacement.PhotoRightCalendarLeft,
            LayoutPlacement.PhotoRightCalendarLeft => LayoutPlacement.PhotoLeftCalendarRight,
            LayoutPlacement.PhotoTopCalendarBottom => LayoutPlacement.PhotoBottomCalendarTop,
            LayoutPlacement.PhotoBottomCalendarTop => LayoutPlacement.PhotoTopCalendarBottom,
            _ => p
        };
        _ = _storage.UpdateProjectAsync(_project);
        _canvas.InvalidateSurface();
    }

    private void OnYearChanged(object? sender, TextChangedEventArgs e)
    {
        if (_project == null) return;
        if (int.TryParse(e.NewTextValue, out var year) && year >= 1900 && year <= 2100)
        {
            _project.Year = year;
            UpdatePageLabel();
            _ = _storage.UpdateProjectAsync(_project);
            _canvas.InvalidateSurface();
        }
    }

    private async Task WithBusyButtonAsync(Button? btn, Func<Task> action, string busyText = "Exporting…")
    {
        if (btn == null)
        {
            await action();
            return;
        }

        var originalText = btn.Text;
        var originalEnabled = btn.IsEnabled;
        try
        {
            btn.IsEnabled = false;
            btn.Text = busyText;
            await Task.Yield();
            await action();
        }
        finally
        {
            btn.Text = originalText;
            btn.IsEnabled = originalEnabled;
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        await WithBusyButtonAsync(sender as Button, async () =>
        {
            if (_project == null) return;

            byte[] bytes;
            string fileName;

            if (_pageIndex == -1)
            {
                bytes = await _pdf.ExportCoverAsync(_project);
                fileName = $"Calendar_{_project.Year}_FrontCover.pdf";
            }
            else if (_pageIndex == 12)
            {
                bytes = await _pdf.ExportBackCoverAsync(_project);
                fileName = $"Calendar_{_project.Year}_BackCover.pdf";
            }
            else
            {
                bytes = await _pdf.ExportMonthAsync(_project, _pageIndex);
                var month = ((_project.StartMonth - 1 + _pageIndex) % 12) + 1;
                fileName = $"Calendar_{_project.Year}_{month:00}.pdf";
            }

            await SaveBytesAsync(fileName, bytes);
        });
    }

    private async void OnExportCoverClicked(object? sender, EventArgs e)
    {
        await WithBusyButtonAsync(sender as Button, async () =>
        {
            if (_project == null) return;
            var bytes = await _pdf.ExportCoverAsync(_project);
            var fileName = $"Calendar_{_project.Year}_Cover.pdf";
            await SaveBytesAsync(fileName, bytes);
        });
    }

    private async void OnExportYearClicked(object? sender, EventArgs e)
    {
        if (_project == null) return;

        var cts = new CancellationTokenSource();
        var progressModal = new ExportProgressModal();
        progressModal.SetCancellationTokenSource(cts);

        var progress = new Progress<Services.ExportProgress>(p => progressModal.UpdateProgress(p));

        bool exportCompleted = false;
        byte[]? exportedBytes = null;
        Exception? exportException = null;

        progressModal.Cancelled += async (_, __) =>
        {
            try { await _navigationService.PopModalAsync(); } catch { }
        };

        await _navigationService.PushModalAsync(progressModal, true);

        _ = Task.Run(async () =>
              {
                  try
                  {
                      exportedBytes = await _pdf.ExportYearAsync(_project, includeCover: true, progress, cts.Token);
                      exportCompleted = true;
                  }
                  catch (OperationCanceledException)
                  {
                      await MainThread.InvokeOnMainThreadAsync(async () =>
       {
           try { await _navigationService.PopModalAsync(); } catch { }
           await _dialogService.ShowAlertAsync("Export Cancelled", "The export was cancelled.");
       });
                      return;
                  }
                  catch (Exception ex)
                  {
                      exportException = ex;
                  }

                  await MainThread.InvokeOnMainThreadAsync(async () =>
         {
             try { await _navigationService.PopModalAsync(); } catch { }

             if (exportException != null)
             {
                 await _dialogService.ShowAlertAsync("Export Failed", exportException.Message);
             }
             else if (exportCompleted && exportedBytes != null)
             {
                 var fileName = $"Calendar_{_project.Year}_FullYear.pdf";
                 await SaveBytesAsync(fileName, exportedBytes);
             }
         });
              });
    }

    private async void OnExportDoubleSidedClicked(object? sender, EventArgs e)
    {
        if (_project == null) return;

        var cts = new CancellationTokenSource();
        var progressModal = new ExportProgressModal();
        progressModal.SetCancellationTokenSource(cts);

        var progress = new Progress<Services.ExportProgress>(p => progressModal.UpdateProgress(p));

        bool exportCompleted = false;
        byte[]? exportedBytes = null;
        Exception? exportException = null;

        progressModal.Cancelled += async (_, __) =>
  {
      try { await _navigationService.PopModalAsync(); } catch { }
  };

        await _navigationService.PushModalAsync(progressModal, true);

        _ = Task.Run(async () =>
   {
       try
       {
           exportedBytes = await _pdf.ExportDoubleSidedAsync(_project, progress, cts.Token);
           exportCompleted = true;
       }
       catch (OperationCanceledException)
       {
           await MainThread.InvokeOnMainThreadAsync(async () =>
       {
           try { await _navigationService.PopModalAsync(); } catch { }
           await _dialogService.ShowAlertAsync("Export Cancelled", "The export was cancelled.");
       });
           return;
       }
       catch (Exception ex)
       {
           exportException = ex;
       }

       await MainThread.InvokeOnMainThreadAsync(async () =>
  {
      try { await _navigationService.PopModalAsync(); } catch { }

      if (exportException != null)
      {
          await _dialogService.ShowAlertAsync("Export Failed", exportException.Message);
      }
      else if (exportCompleted && exportedBytes != null)
      {
          var fileName = $"Calendar_{_project.Year}_DoubleSided.pdf";
          await SaveBytesAsync(fileName, exportedBytes);
      }
  });
   });
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

    private async Task EnsureProjectLoadedAsync()
    {
        if (_project != null) return;
        if (string.IsNullOrEmpty(ProjectId)) return;
        var projects = await _storage.GetProjectsAsync();
        _project = projects.FirstOrDefault(p => p.Id == ProjectId);
        if (_project != null)
        {
            SplitSlider.Value = _project.LayoutSpec.SplitRatio;
            SplitValueLabel.Text = _project.LayoutSpec.SplitRatio.ToString("P0");
            YearEntry.Text = _project.Year.ToString();
            StartMonthPicker.SelectedIndex = Math.Clamp(_project.StartMonth - 1, 0, 11);
            FirstDowPicker.SelectedIndex = (int)_project.FirstDayOfWeek;
            DoubleSidedCheckBox.IsChecked = _project.EnableDoubleSided;
            _activeSlotIndex = 0;
            SyncPhotoLayoutPicker();
            SyncZoomUI();
            UpdatePageLabel();
            _canvas.InvalidateSurface();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = EnsureProjectLoadedAsync();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await EnsureProjectLoadedAsync();
    }

    private void SyncZoomUI()
    {
        var asset = GetActiveAsset();
        ZoomSlider.IsEnabled = asset != null;
        if (asset != null)
        {
            ZoomSlider.Value = Math.Clamp(asset.Zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
            ZoomValueLabel.Text = $"{ZoomSlider.Value:F2}x";
        }
    }

    private ImageAsset? GetActiveAsset()
    {
        if (_project == null) return null;

        if (_pageIndex == -1)
            return _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == _activeSlotIndex);
        else if (_pageIndex == 12)
            return _project.ImageAssets.FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == _activeSlotIndex);
        else
            return _project.ImageAssets
                .Where(a => a.Role == "monthPhoto" && a.MonthIndex == _pageIndex && (a.SlotIndex ?? 0) == _activeSlotIndex)
                .OrderBy(a => a.Order)
                .FirstOrDefault();
    }

    private void UpdateAssetZoom(double newValue)
    {
        if (_project == null) return;
        var asset = GetActiveAsset();
        if (asset != null)
        {
            asset.Zoom = Math.Clamp(newValue, ZoomSlider.Minimum, ZoomSlider.Maximum);
            _ = _storage.UpdateProjectAsync(_project);
            _canvas.InvalidateSurface();
        }
    }

    private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        if (_project is null)
            return;

        var (pageWpt, pageHpt) = PageSizes.GetPoints(_project.PageSpec);
        if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }
        var scale = Math.Min(e.Info.Width / (float)pageWpt, e.Info.Height / (float)pageHpt);
        var offsetX = (e.Info.Width - (float)pageWpt * scale) / 2f;
        var offsetY = (e.Info.Height - (float)pageHpt * scale) / 2f;

        _pageScale = scale; _pageOffsetX = offsetX; _pageOffsetY = offsetY;

        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale((float)scale);

        var pageRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        using var pageBorder = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
        canvas.DrawRect(pageRect, pageBorder);

        // Determine content rect based on page type and borderless settings
        var m = _project.Margins;
        SKRect contentRect;

        if (_pageIndex == -1 && _project.CoverSpec.BorderlessFrontCover)
        {
            // Front cover with borderless - use full page
            contentRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        }
        else if (_pageIndex == 12 && _project.CoverSpec.BorderlessBackCover)
        {
            // Back cover with borderless - use full page
            contentRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        }
        else
        {
            // Normal margins
            contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, (float)pageWpt - (float)m.RightPt, (float)pageHpt - (float)m.BottomPt);
        }

        // If double-sided mode is enabled, show covers at half-height
        // This matches how they'll appear in the final PDF (Page 14)
        if (_project.EnableDoubleSided && (_pageIndex == -1 || _pageIndex == 12))
        {
            // Use only the top half or bottom half for covers in double-sided mode
            float halfHeight = contentRect.Height / 2f;
            if (_pageIndex == -1)
            {
                // Front cover - bottom half (to match Page 14 layout)
                contentRect = new SKRect(contentRect.Left, contentRect.MidY + 2f, contentRect.Right, contentRect.Bottom);
            }
            else // _pageIndex == 12
            {
                // Back cover - top half (to match Page 14 layout)
                contentRect = new SKRect(contentRect.Left, contentRect.Top, contentRect.Right, contentRect.MidY - 2f);
            }
        }

        _lastContentRect = contentRect;

        // Only draw content border if not borderless
        bool isBorderless = (_pageIndex == -1 && _project.CoverSpec.BorderlessFrontCover) ||
                           (_pageIndex == 12 && _project.CoverSpec.BorderlessBackCover);
        if (!isBorderless)
        {
            using var contentBorder = new SKPaint { Color = SKColors.Silver, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
            canvas.DrawRect(contentRect, contentBorder);
        }

        if (_pageIndex == -1) // Front cover
        {
            var layout = _project.FrontCoverPhotoLayout;
            _lastPhotoSlots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);

            _lastPhotoRect = contentRect;
            System.Diagnostics.Debug.WriteLine($"Front Cover: Layout={layout}, Slots={_lastPhotoSlots.Count}, ActiveSlot={_activeSlotIndex}");
            DrawCover(canvas, contentRect, _project, true);
        }
        else if (_pageIndex == -2) // Previous December (only in double-sided mode)
        {
            (SKRect photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, _project.LayoutSpec);

            // Month index 6 represents December when StartMonth is January (0-based from StartMonth)
            // For previous year's December, we use index 6 but with previous year
            int decemberIndex = (_project.StartMonth == 1) ? 11 : (12 - _project.StartMonth);

            var layout = _project.MonthPhotoLayouts.TryGetValue(decemberIndex, out var perMonth)
              ? perMonth
                : _project.LayoutSpec.PhotoLayout;

            _lastPhotoRect = photoRect;
            _lastPhotoSlots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);
            DrawPhotos(canvas, _lastPhotoSlots);

            // Draw calendar for previous year's December
            DrawPreviousDecemberCalendar(canvas, calRect, _project);
        }
        else if (_pageIndex == 12) // Back cover
        {
            var layout = _project.BackCoverPhotoLayout;
            _lastPhotoSlots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);
            _lastPhotoRect = contentRect;
            System.Diagnostics.Debug.WriteLine($"Back Cover: Layout={layout}, Slots={_lastPhotoSlots.Count}, ActiveSlot={_activeSlotIndex}");
            DrawCover(canvas, contentRect, _project, false);
        }
        else // Month page
        {
            (SKRect photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, _project.LayoutSpec);
     
            // Get the per-month layout override or default layout
       var layout = _project.MonthPhotoLayouts.TryGetValue(_pageIndex, out var perMonth)
         ? perMonth
         : _project.LayoutSpec.PhotoLayout;
            
 _lastPhotoRect = photoRect;
       _lastPhotoSlots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);
            DrawPhotos(canvas, _lastPhotoSlots);
          DrawCalendarGrid(canvas, calRect, _project);
        }

        canvas.Restore();
    }

    private void DrawPhotos(SKCanvas canvas, List<SKRect> slots)
    {
        if (_project == null) return;

        _calendarRenderer.RenderPhotoSlots(
          canvas,
         slots,
   _project.ImageAssets.ToList(),
     "monthPhoto",
         _pageIndex,
    _activeSlotIndex);
    }

    private void DrawCover(SKCanvas canvas, SKRect bounds, CalendarProject project, bool isFrontCover)
    {
        var role = isFrontCover ? "coverPhoto" : "backCoverPhoto";

        _calendarRenderer.RenderPhotoSlots(
                  canvas,
             _lastPhotoSlots,
                  project.ImageAssets.ToList(),
               role,
          null,
               _activeSlotIndex);
    }

    private void DrawBitmapWithPanZoom(SKCanvas canvas, SKBitmap bmp, SKRect rect, ImageAsset asset)
    {
        var imgW = (float)bmp.Width;
        var imgH = (float)bmp.Height;
        var rectW = rect.Width;
        var rectH = rect.Height;
        var imgAspect = imgW / imgH;
        var rectAspect = rectW / rectH;

        float scale = (imgAspect > rectAspect ? rectH / imgH : rectW / imgW) * (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);

        var targetW = imgW * scale;
        var targetH = imgH * scale;
        var excessX = Math.Max(0, (targetW - rectW) / 2f);
        var excessY = Math.Max(0, (targetH - rectH) / 2f);

        var px = (float)Math.Clamp(asset.PanX, -1, 1);
        var py = (float)Math.Clamp(asset.PanY, -1, 1);

        var left = rect.Left - excessX + px * excessX;
        var top = rect.Top - excessY + py * excessY;
        var dest = new SKRect(left, top, left + targetW, top + targetH);

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.Save();
        canvas.ClipRect(rect, antialias: true);
        canvas.DrawBitmap(bmp, dest, paint);
        canvas.Restore();
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (_project == null)
            return;

        var loc = e.Location;
        float density = 1f;
        try
        {
            var canvasSize = _canvas.CanvasSize;
            if (_canvas.Width > 0)
                density = (float)(canvasSize.Width / (float)_canvas.Width);
        }
        catch { }
        var touchPx = new SKPoint((float)loc.X * density, (float)loc.Y * density);

        var pagePt = new SKPoint((float)((touchPx.X - _pageOffsetX) / _pageScale), (float)((touchPx.Y - _pageOffsetY) / _pageScale));
        var isCover = (_pageIndex == -1 || _pageIndex == 12);
        var hitRect = isCover ? _lastContentRect : _lastPhotoRect;

        int HitTestSlot(SKPoint pt)
        {
            if (_lastPhotoSlots.Count == 0) return -1;
            return _lastPhotoSlots.FindIndex(r => r.Contains(pt));
        }

        SKRect CurrentTargetRect()
        {
            return _lastPhotoSlots.Count > _activeSlotIndex ? _lastPhotoSlots[_activeSlotIndex] : hitRect;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _isPointerDown = true;

                var hitIdx = HitTestSlot(pagePt);
                System.Diagnostics.Debug.WriteLine($"Touch Pressed: HitIdx={hitIdx}, ActiveSlot={_activeSlotIndex}, PageIndex={_pageIndex}, IsCover={isCover}");
                if (hitIdx >= 0 && hitIdx != _activeSlotIndex)
                {
                    _activeSlotIndex = hitIdx;
                    SyncZoomUI();
                    _canvas.InvalidateSurface();
                }

                var targetRectPressed = CurrentTargetRect();
                var assetPressed = isCover
                    ? _project.ImageAssets.FirstOrDefault(a => a.Role == (_pageIndex == -1 ? "coverPhoto" : "backCoverPhoto") && (a.SlotIndex ?? 0) == _activeSlotIndex)
                    : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _pageIndex && (a.SlotIndex ?? 0) == _activeSlotIndex);

                _pressedOnAsset = targetRectPressed.Contains(pagePt) && assetPressed != null && File.Exists(assetPressed.Path);
                _dragStartPagePt = pagePt;
                if (_pressedOnAsset)
                {
                    using var bmp = SKBitmap.Decode(assetPressed!.Path);
                    if (bmp != null)
                    {
                        var imgW = (float)bmp.Width; var imgH = (float)bmp.Height;
                        var rectW = targetRectPressed.Width; var rectH = targetRectPressed.Height;
                        var imgAspect = imgW / imgH; var rectAspect = rectW / rectH;
                        float baseScale = imgAspect > rectAspect ? rectH / imgH : rectW / imgW;
                        float scale = baseScale * (float)Math.Clamp(assetPressed.Zoom <= 0 ? 1 : assetPressed.Zoom, 0.5, 3.0);
                        var targetW = imgW * scale; var targetH = imgH * scale;
                        _dragExcessX = Math.Max(0, (targetW - rectW) / 2f);
                        _dragExcessY = Math.Max(0, (targetH - rectH) / 2f);

                        _startPanX = assetPressed.PanX;
                        _startPanY = assetPressed.PanY;
                        _startZoom = assetPressed.Zoom;
                        _dragIsCover = isCover;
                    }
                }
                break;

            case SKTouchAction.Moved:
                if (_isPointerDown && _pressedOnAsset)
                {
                    var assetMove = isCover
                        ? _project.ImageAssets.FirstOrDefault(a => a.Role == (_pageIndex == -1 ? "coverPhoto" : "backCoverPhoto") && (a.SlotIndex ?? 0) == _activeSlotIndex)
                        : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _pageIndex && (a.SlotIndex ?? 0) == _activeSlotIndex);
                    var dx0 = pagePt.X - _dragStartPagePt.X;
                    var dy0 = pagePt.Y - _dragStartPagePt.Y;
                    if (!_isDragging)
                    {
                        if (Math.Abs(dx0) > DragStartThreshold || Math.Abs(dy0) > DragStartThreshold)
                        {
                            _isDragging = true;
                        }
                    }

                    if (_isDragging && assetMove != null)
                    {
                        double newPanX = _startPanX + (_dragExcessX > 0 ? dx0 / _dragExcessX : 0);
                        double newPanY = _startPanY + (_dragExcessY > 0 ? dy0 / _dragExcessY : 0);
                        assetMove.PanX = Math.Clamp(newPanX, -1, 1);
                        assetMove.PanY = Math.Clamp(newPanY, -1, 1);
                        _canvas.InvalidateSurface();
                        e.Handled = true;
                        return;
                    }
                }
                break;

            case SKTouchAction.Released:
                _isPointerDown = false;
                if (_isDragging)
                {
                    _isDragging = false;
                    _ = _storage.UpdateProjectAsync(_project);
                    e.Handled = true;
                    return;
                }

                var targetRectReleased = CurrentTargetRect();
                if (targetRectReleased.Contains(pagePt))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastTapAt).TotalMilliseconds < 300 && SKPoint.Distance(pagePt, _lastTapPt) < 10)
                    {
                        _ = ShowPhotoSelectorAsync();
                    }
                    _lastTapAt = now;
                    _lastTapPt = pagePt;
                }
                break;

            case SKTouchAction.Cancelled:
                _isPointerDown = false;
                _isDragging = false;
                break;
        }

        e.Handled = false;
    }

    private void DrawCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
        var month = ((project.StartMonth - 1 + _pageIndex) % 12) + 1;
        var year = project.Year + (project.StartMonth - 1 + _pageIndex) / 12;

        _calendarRenderer.RenderCalendarGrid(canvas, bounds, project, year, month);
    }

    private void DrawPreviousDecemberCalendar(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
        // Previous year's December
        int year = project.Year - 1;
        int month = 12;

        _calendarRenderer.RenderCalendarGrid(canvas, bounds, project, year, month);
    }

    private void OnSplitResetTapped(object? sender, TappedEventArgs e)
    {
        if (_project != null)
        {
            _project.LayoutSpec.SplitRatio = 0.5;
            SplitSlider.Value = 0.5;
            SplitValueLabel.Text = "50%";
            _ = _storage.UpdateProjectAsync(_project);
            _canvas.InvalidateSurface();
        }
    }

    private void OnZoomResetTapped(object? sender, TappedEventArgs e)
    {
        var asset = GetActiveAsset();
        if (asset != null)
        {
            asset.Zoom = 1;
            asset.PanX = asset.PanY = 0;
            ZoomSlider.Value = 1;
            ZoomValueLabel.Text = "1.00x";
            _ = _storage.UpdateProjectAsync(_project!);
            _canvas.InvalidateSurface();
        }
    }
}
