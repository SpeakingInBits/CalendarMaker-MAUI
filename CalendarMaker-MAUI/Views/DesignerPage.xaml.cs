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

    public DesignerPage(ICalendarEngine engine, IProjectStorageService storage, IAssetService assets, IPdfExportService pdf)
    {
        InitializeComponent();
        _engine = engine;
        _storage = storage;
        _assets = assets;
        _pdf = pdf;

        // Ensure CanvasHost exists before assigning
        _canvas = new SKCanvasView { IgnorePixelScaling = false };
        _canvas.PaintSurface += Canvas_PaintSurface;
        _canvas.EnableTouchEvents = true;
        _canvas.Touch += OnCanvasTouch;

        if (CanvasHost != null)
            CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");
        PrevBtn.Clicked += (_, __) => NavigatePage(-1);
        NextBtn.Clicked += (_, __) => NavigatePage(1);
        AddPhotoBtn.Clicked += async (_, __) => await ImportPhotosToProjectAsync();
        ExportBtn.Clicked += OnExportClicked;
        ExportCoverBtn.Clicked += OnExportCoverClicked;
        ExportYearBtn.Clicked += OnExportYearClicked;

        FlipBtn.Clicked += (_, __) => FlipLayout();
        SplitSlider.ValueChanged += (_, e) => { SplitValueLabel.Text = e.NewValue.ToString("P0"); if (_project != null) { _project.LayoutSpec.SplitRatio = e.NewValue; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        ZoomSlider.ValueChanged += (_, e) => { ZoomValueLabel.Text = $"{e.NewValue:F2}x"; UpdateAssetZoom(e.NewValue); };
        YearEntry.TextChanged += OnYearChanged;
        StartMonthPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.StartMonth = StartMonthPicker.SelectedIndex + 1; _pageIndex = -1; _activeSlotIndex = 0; SyncZoomUI(); UpdatePageLabel(); _ = _storage.UpdateProjectAsync(_project); _canvas.InvalidateSurface(); } };
        FirstDowPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.FirstDayOfWeek = (DayOfWeek)FirstDowPicker.SelectedIndex; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        _pageIndex = -1; // start with front cover
        PopulateStaticPickers();
    }

    private void NavigatePage(int direction)
    {
        _pageIndex += direction;
        // Wrap around: -1 (front cover) to 12 (back cover) = 14 total pages
        if (_pageIndex < -1) _pageIndex = 12;
        if (_pageIndex > 12) _pageIndex = -1;
        
        _activeSlotIndex = 0;
        SyncZoomUI();
        UpdatePageLabel();
        _canvas.InvalidateSurface();
    }

    private async Task ImportPhotosToProjectAsync()
    {
        if (_project == null) return;
        
        var results = await FilePicker.PickMultipleAsync(new PickOptions 
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
        if (_pageIndex == -1)
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
                monthIndex = _pageIndex;
            }
            
            await _assets.AssignPhotoToSlotAsync(_project, selected.Id, monthIndex ?? 0, slotIndex, role);
            SyncZoomUI();
            _canvas.InvalidateSurface();
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
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
            else // Month page
            {
                await _assets.RemovePhotoFromSlotAsync(_project, _pageIndex, _activeSlotIndex, "monthPhoto");
            }
            
            _canvas.InvalidateSurface();
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        };

        // Close without changes
        modal.Cancelled += async (_, __) =>
        {
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        };

        try
        {
            await Shell.Current.Navigation.PushModalAsync(modal, true);
        }
        catch
        {
            // Fallback if Shell not available
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
        else if (_pageIndex >= 0 && _pageIndex <= 11) // Month pages
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
        
        if (_pageIndex == -1)
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
        else if (_pageIndex >= 0 && _pageIndex <= 11) // Month pages
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
            _ = _storage.UpdateProjectAsync(_project);
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
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        };

        await Shell.Current.Navigation.PushModalAsync(progressModal, true);

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
                    try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
                    await this.DisplayAlertAsync("Export Cancelled", "The export was cancelled.", "OK");
                });
                return;
            }
            catch (Exception ex)
            {
                exportException = ex;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await Shell.Current.Navigation.PopModalAsync(); } catch { }

                if (exportException != null)
                {
                    await this.DisplayAlertAsync("Export Failed", exportException.Message, "OK");
                }
                else if (exportCompleted && exportedBytes != null)
                {
                    var fileName = $"Calendar_{_project.Year}_FullYear.pdf";
                    await SaveBytesAsync(fileName, exportedBytes);
                }
            });
        });
    }

    private async Task SaveBytesAsync(string suggestedFileName, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, default);
        if (!result.IsSuccessful)
        {
            await this.DisplayAlertAsync("Save Failed", result.Exception?.Message ?? "Unknown error", "OK");
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

        var m = _project.Margins;
        var contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, (float)pageWpt - (float)m.RightPt, (float)pageHpt - (float)m.BottomPt);
        _lastContentRect = contentRect;
        using var contentBorder = new SKPaint { Color = SKColors.Silver, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
        canvas.DrawRect(contentRect, contentBorder);

        if (_pageIndex == -1) // Front cover
        {
            var layout = _project.FrontCoverPhotoLayout;
            _lastPhotoSlots = ComputePhotoSlots(contentRect, layout, true);
            _lastPhotoRect = contentRect;
            System.Diagnostics.Debug.WriteLine($"Front Cover: Layout={layout}, Slots={_lastPhotoSlots.Count}, ActiveSlot={_activeSlotIndex}");
            DrawCover(canvas, contentRect, _project, true);
        }
        else if (_pageIndex == 12) // Back cover
        {
            var layout = _project.BackCoverPhotoLayout;
            _lastPhotoSlots = ComputePhotoSlots(contentRect, layout, true);
            _lastPhotoRect = contentRect;
            System.Diagnostics.Debug.WriteLine($"Back Cover: Layout={layout}, Slots={_lastPhotoSlots.Count}, ActiveSlot={_activeSlotIndex}");
            DrawCover(canvas, contentRect, _project, false);
        }
        else // Month page
        {
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, _project.LayoutSpec);
            _lastPhotoRect = photoRect;
            _lastPhotoSlots = ComputePhotoSlots(photoRect, _project.LayoutSpec.PhotoLayout, false);
            DrawPhotos(canvas, _lastPhotoSlots);
            DrawCalendarGrid(canvas, calRect, _project);
        }

        canvas.Restore();
    }

    private List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout, bool isCover = false)
    {
        if (!isCover && _project != null && _pageIndex >= 0 && _pageIndex <= 11 && _project.MonthPhotoLayouts.TryGetValue(_pageIndex, out var perMonth))
            layout = perMonth;
        const float gap = 4f;
        var list = new List<SKRect>();
        switch (layout)
        {
            case PhotoLayout.TwoVerticalSplit:
                {
                    float halfW = (area.Width - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top, area.Right, area.Bottom));
                    break;
                }
            case PhotoLayout.Grid2x2:
                {
                    float halfW = (area.Width - gap) / 2f;
                    float halfH = (area.Height - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top, area.Right, area.Top + halfH));
                    list.Add(new SKRect(area.Left, area.Top + halfH + gap, area.Left + halfW, area.Bottom));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top + halfH + gap, area.Right, area.Bottom));
                    break;
                }
            case PhotoLayout.TwoHorizontalStack:
                {
                    float halfH = (area.Height - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Right, area.Top + halfH));
                    list.Add(new SKRect(area.Left, area.Top + halfH + gap, area.Right, area.Bottom));
                    break;
                }
            case PhotoLayout.ThreeLeftStack:
                {
                    float halfW = (area.Width - gap) / 2f;
                    float halfH = (area.Height - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH));
                    list.Add(new SKRect(area.Left, area.Top + halfH + gap, area.Left + halfW, area.Bottom));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top, area.Right, area.Bottom));
                    break;
                }
            case PhotoLayout.ThreeRightStack:
                {
                    float halfW = (area.Width - gap) / 2f;
                    float halfH = (area.Height - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top, area.Right, area.Top + halfH));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top + halfH + gap, area.Right, area.Bottom));
                    break;
                }
            default:
                list.Add(area);
                break;
        }
        return list;
    }

    private void DrawPhotos(SKCanvas canvas, List<SKRect> slots)
    {
        if (_project == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var rect = slots[i];
            var asset = _project.ImageAssets
                .Where(a => a.Role == "monthPhoto" && a.MonthIndex == _pageIndex && (a.SlotIndex ?? 0) == i)
                .OrderBy(a => a.Order)
                .FirstOrDefault();
            if (asset == null || string.IsNullOrEmpty(asset.Path) || !File.Exists(asset.Path))
            {
                using var photoFill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
                using var photoBorder = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
                canvas.DrawRect(rect, photoFill);
                canvas.DrawRect(rect, photoBorder);
                
                if (i == _activeSlotIndex)
                {
                    using var hintPaint = new SKPaint { Color = SKColors.Gray, TextSize = 12, IsAntialias = true };
                    var hintText = "Double-click to assign photo";
                    var textWidth = hintPaint.MeasureText(hintText);
                    canvas.DrawText(hintText, rect.MidX - textWidth / 2, rect.MidY, hintPaint);
                }
            }
            else
            {
                using var bmp = SKBitmap.Decode(asset.Path);
                if (bmp == null)
                {
                    canvas.DrawRect(rect, new SKPaint { Color = SKColors.LightGray });
                }
                else
                {
                    DrawBitmapWithPanZoom(canvas, bmp, rect, asset);
                }
            }

            if (i == _activeSlotIndex)
            {
                using var hi = new SKPaint { Color = SKColors.DeepSkyBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
                canvas.DrawRect(rect, hi);
            }
        }
    }

    private void DrawCover(SKCanvas canvas, SKRect bounds, CalendarProject project, bool isFrontCover)
    {
        var role = isFrontCover ? "coverPhoto" : "backCoverPhoto";
        
        for (int i = 0; i < _lastPhotoSlots.Count; i++)
        {
            var rect = _lastPhotoSlots[i];
            var asset = project.ImageAssets
                .FirstOrDefault(a => a.Role == role && (a.SlotIndex ?? 0) == i);
            
            if (asset != null && File.Exists(asset.Path))
            {
                using var bmp = SKBitmap.Decode(asset.Path);
                if (bmp != null)
                {
                    canvas.Save();
                    canvas.ClipRect(rect, antialias: true);
                    DrawBitmapWithPanZoom(canvas, bmp, rect, asset);
                    canvas.Restore();
                }
            }
            else
            {
                using var fill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
                using var border = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
                canvas.DrawRect(rect, fill);
                canvas.DrawRect(rect, border);
                
                if (i == _activeSlotIndex)
                {
                    using var hintPaint = new SKPaint { Color = SKColors.Gray, TextSize = 12, IsAntialias = true };
                    var hintText = "Double-click to assign photo";
                    var textWidth = hintPaint.MeasureText(hintText);
                    canvas.DrawText(hintText, rect.MidX - textWidth / 2, rect.MidY, hintPaint);
                }
            }
            
            if (i == _activeSlotIndex)
            {
                using var hi = new SKPaint { Color = SKColors.DeepSkyBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
                canvas.DrawRect(rect, hi);
            }
        }
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

    private (SKRect photo, SKRect cal) ComputeSplit(SKRect area, LayoutSpec spec)
    {
        var ratio = (float)Math.Clamp(spec.SplitRatio, 0.1, 0.9);
        return spec.Placement switch
        {
            LayoutPlacement.PhotoLeftCalendarRight => (new SKRect(area.Left, area.Top, area.Left + area.Width * ratio, area.Bottom), new SKRect(area.Left + area.Width * ratio, area.Top, area.Right, area.Bottom)),
            LayoutPlacement.PhotoRightCalendarLeft => (new SKRect(area.Left + area.Width * (1 - ratio), area.Top, area.Right, area.Bottom), new SKRect(area.Left, area.Top, area.Left + area.Width * (1 - ratio), area.Bottom)),
            LayoutPlacement.PhotoTopCalendarBottom => (new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * ratio), new SKRect(area.Left, area.Top + area.Height * ratio, area.Right, area.Bottom)),
            LayoutPlacement.PhotoBottomCalendarTop => (new SKRect(area.Left, area.Top + area.Height * (1 - ratio), area.Right, area.Bottom), new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * (1 - ratio))),
            _ => (area, area)
        };
    }

    private void DrawCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
        float Stroke1px() => 1f / Math.Max(_pageScale, 0.0001f);
        float Snap(float v) => (float)Math.Round(v * _pageScale) / Math.Max(_pageScale, 0.0001f);

        var month = ((project.StartMonth - 1 + _pageIndex) % 12) + 1;
        var year = project.Year + (project.StartMonth - 1 + _pageIndex) / 12;
        var weeks = _engine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

        float headerH = 40;
        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + headerH);
        var gridRect = new SKRect(bounds.Left, headerRect.Bottom, bounds.Right, bounds.Bottom);

        using var titlePaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = 18, IsAntialias = true };
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var titleWidth = titlePaint.MeasureText(title);
        canvas.DrawText(title, gridRect.MidX - titleWidth / 2, headerRect.MidY + titlePaint.TextSize / 2.5f, titlePaint);

        float dowH = 20;
        var dowRect = new SKRect(gridRect.Left, gridRect.Top, gridRect.Right, gridRect.Top + dowH);
        string[] dows = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        int shift = (int)project.FirstDayOfWeek;
        var displayDows = Enumerable.Range(0, 7).Select(i => dows[(i + shift) % 7]).ToArray();

        using var gridPen = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = Stroke1px(), IsAntialias = false };
        using var textPaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = 10, IsAntialias = true };

        float colW = dowRect.Width / 7f;
        for (int c = 0; c < 7; c++)
        {
            var cell = new SKRect(dowRect.Left + c * colW, dowRect.Top, dowRect.Left + (c + 1) * colW, dowRect.Bottom);
            var t = displayDows[c];
            var tw = textPaint.MeasureText(t);
            canvas.DrawText(t, cell.MidX - tw / 2, cell.MidY + textPaint.TextSize / 2.5f, textPaint);
            var x0 = Snap(cell.Left);
            var x1 = Snap(cell.Right);
            var y0 = Snap(cell.Top);
            var y1 = Snap(cell.Bottom);
            canvas.DrawLine(x0, y0, x1, y0, gridPen);
            canvas.DrawLine(x0, y1, x1, y1, gridPen);
            canvas.DrawLine(x0, y0, x0, y1, gridPen);
            if (c == 6)
                canvas.DrawLine(x1, y0, x1, y1, gridPen);
        }

        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, bounds.Bottom);
        int rows = weeks.Count;
        if (rows <= 0)
        {
            var xa = Snap(weeksArea.Left); var xb = Snap(weeksArea.Right);
            var ya = Snap(weeksArea.Top); var yb = Snap(weeksArea.Bottom);
            canvas.DrawRect(new SKRect(xa, ya, xb, yb), gridPen);
            return;
        }

        float rowH = weeksArea.Height / rows;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                var left = weeksArea.Left + c * colW;
                var top = weeksArea.Top + r * rowH;
                var right = weeksArea.Left + (c + 1) * colW;
                var bottom = weeksArea.Top + (r + 1) * rowH;
                var cell = new SKRect(left, top, right, bottom);

                var date = weeks[r][c];
                if (date.HasValue && date.Value.Month == month)
                {
                    var dayStr = date.Value.Day.ToString(CultureInfo.InvariantCulture);
                    canvas.DrawText(dayStr, cell.Left + 2, cell.Top + textPaint.TextSize + 2, textPaint);
                }
            }
        }

        var wx0 = Snap(weeksArea.Left);
        var wx1 = Snap(weeksArea.Right);
        var wy0 = Snap(weeksArea.Top);
        var wy1 = Snap(weeksArea.Bottom);

        for (int c = 0; c <= 7; c++)
        {
            var x = Snap(weeksArea.Left + c * colW);
            canvas.DrawLine(x, wy0, x, wy1, gridPen);
        }
        for (int r = 0; r <= rows; r++)
        {
            var y = Snap(weeksArea.Top + r * rowH);
            canvas.DrawLine(wx0, y, wx1, y, gridPen);
        }

        canvas.DrawRect(new SKRect(wx0, wy0, wx1, wy1), gridPen);
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
