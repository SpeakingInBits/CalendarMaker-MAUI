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

    private int _monthIndex; // 0..11 relative to StartMonth

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
        PrevBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 11) % 12; _activeSlotIndex = 0; SyncZoomUI(); UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        NextBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 1) % 12; _activeSlotIndex = 0; SyncZoomUI(); UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        AddPhotoBtn.Clicked += async (_, __) => await ImportPhotosToProjectAsync();
        ExportBtn.Clicked += OnExportClicked;
        ExportCoverBtn.Clicked += OnExportCoverClicked;
        ExportYearBtn.Clicked += OnExportYearClicked;

        CoverSwitch.Toggled += (_, __) => { _activeSlotIndex = 0; SyncZoomUI(); _canvas.InvalidateSurface(); };
        TitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.TitleText = TitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        SubtitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.SubtitleText = SubtitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        FlipBtn.Clicked += (_, __) => FlipLayout();
        SplitSlider.ValueChanged += (_, e) => { SplitValueLabel.Text = e.NewValue.ToString("P0"); if (_project != null) { _project.LayoutSpec.SplitRatio = e.NewValue; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        ZoomSlider.ValueChanged += (_, e) => { ZoomValueLabel.Text = $"{e.NewValue:F2}x"; UpdateAssetZoom(e.NewValue); };
        StartMonthPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.StartMonth = StartMonthPicker.SelectedIndex + 1; _monthIndex = 0; _activeSlotIndex = 0; SyncZoomUI(); UpdateMonthLabel(); _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        FirstDowPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.FirstDayOfWeek = (DayOfWeek)FirstDowPicker.SelectedIndex; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        _monthIndex = 0; // start with first month
        PopulateStaticPickers();
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

        // Always open the modal, even if there are no unassigned photos yet.
        var unassignedPhotos = await _assets.GetUnassignedPhotosAsync(_project);

        string slotDescription;
        if (CoverSwitch.IsToggled)
        {
            slotDescription = "Cover";
        }
        else
        {
            var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
            var year = _project.Year + (_project.StartMonth - 1 + _monthIndex) / 12;
            var monthName = new DateTime(year, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);
            slotDescription = $"{monthName} - Slot {_activeSlotIndex + 1}";
        }

        var modal = new PhotoSelectorModal(unassignedPhotos, slotDescription);

        // Assign selected photo to the active target
        modal.PhotoSelected += async (_, args) =>
        {
            if (_project == null) return;
            var selected = args.SelectedAsset;
            var role = CoverSwitch.IsToggled ? "coverPhoto" : "monthPhoto";
            int? slotIndex = CoverSwitch.IsToggled ? null : _activeSlotIndex;
            await _assets.AssignPhotoToSlotAsync(_project, selected.Id, _monthIndex, slotIndex, role);
            SyncZoomUI();
            _canvas.InvalidateSurface();
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        };

        // Remove any existing photo from the active target
        modal.RemoveRequested += async (_, __) =>
        {
            if (_project == null) return;
            if (CoverSwitch.IsToggled)
            {
                var existingCover = _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto");
                if (existingCover != null)
                {
                    existingCover.Role = "unassigned";
                    existingCover.MonthIndex = null;
                    existingCover.SlotIndex = null;
                    await _storage.UpdateProjectAsync(_project);
                }
            }
            else
            {
                await _assets.RemovePhotoFromSlotAsync(_project, _monthIndex, _activeSlotIndex, "monthPhoto");
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
        _project.MonthPhotoLayouts[_monthIndex] = layout;
        _ = _storage.UpdateProjectAsync(_project);
        _activeSlotIndex = 0;
        _canvas.InvalidateSurface();
    }

    private void UpdateMonthLabel()
    {
        if (_project == null) { MonthLabel.Text = string.Empty; return; }
        var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var year = _project.Year + (_project.StartMonth - 1 + _monthIndex) / 12;
        MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        SyncPhotoLayoutPicker();
    }

    private void SyncPhotoLayoutPicker()
    {
        if (_project == null) return;
        var layout = _project.MonthPhotoLayouts.TryGetValue(_monthIndex, out var l)
            ? l
            : _project.LayoutSpec.PhotoLayout;
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
            // yield to let UI update the button immediately
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
            var bytes = await _pdf.ExportMonthAsync(_project, _monthIndex);
            var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
            var fileName = $"Calendar_{_project.Year}_{month:00}.pdf";
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
        await WithBusyButtonAsync(sender as Button, async () =>
        {
            if (_project == null) return;
            var bytes = await _pdf.ExportYearAsync(_project, includeCover: true);
            var fileName = $"Calendar_{_project.Year}_FullYear.pdf";
            await SaveBytesAsync(fileName, bytes);
        }, busyText: "Exporting year…");
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
            TitleEntry.Text = _project.CoverSpec.TitleText;
            SubtitleEntry.Text = _project.CoverSpec.SubtitleText;
            SplitSlider.Value = _project.LayoutSpec.SplitRatio;
            SplitValueLabel.Text = _project.LayoutSpec.SplitRatio.ToString("P0");
            StartMonthPicker.SelectedIndex = Math.Clamp(_project.StartMonth - 1, 0, 11);
            FirstDowPicker.SelectedIndex = (int)_project.FirstDayOfWeek;
            _activeSlotIndex = 0;
            SyncPhotoLayoutPicker();
            SyncZoomUI();
            UpdateMonthLabel();
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
        if (CoverSwitch.IsToggled)
            return _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto");
        return _project.ImageAssets
            .Where(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex && (a.SlotIndex ?? 0) == _activeSlotIndex)
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

        if (CoverSwitch.IsToggled)
        {
            DrawCover(canvas, contentRect, _project);
            _lastPhotoRect = contentRect;
            _lastPhotoSlots = new List<SKRect> { contentRect };
        }
        else
        {
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, _project.LayoutSpec);
            _lastPhotoRect = photoRect;
            _lastPhotoSlots = ComputePhotoSlots(photoRect, _project.LayoutSpec.PhotoLayout);
            DrawPhotos(canvas, _lastPhotoSlots);
            DrawCalendarGrid(canvas, calRect, _project);
        }

        canvas.Restore();
    }

    private List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout)
    {
        // Resolve per-month override
        if (_project != null && _project.MonthPhotoLayouts.TryGetValue(_monthIndex, out var perMonth))
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
                    // 2 photos stacked vertically on left (taking 50% width), 1 photo on right (taking 50% width)
                    float halfW = (area.Width - gap) / 2f;
                    float halfH = (area.Height - gap) / 2f;
                    list.Add(new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH));
                    list.Add(new SKRect(area.Left, area.Top + halfH + gap, area.Left + halfW, area.Bottom));
                    list.Add(new SKRect(area.Left + halfW + gap, area.Top, area.Right, area.Bottom));
                    break;
                }
            case PhotoLayout.ThreeRightStack:
                {
                    // 1 photo on left (taking 50% width), 2 photos stacked vertically on right (taking 50% width)
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
                .Where(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex && (a.SlotIndex ?? 0) == i)
                .OrderBy(a => a.Order)
                .FirstOrDefault();
            if (asset == null || string.IsNullOrEmpty(asset.Path) || !File.Exists(asset.Path))
            {
                using var photoFill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
                using var photoBorder = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
                canvas.DrawRect(rect, photoFill);
                canvas.DrawRect(rect, photoBorder);
                
                // Draw hint text for double-click
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

            // Active slot highlight
            if (i == _activeSlotIndex && !CoverSwitch.IsToggled)
            {
                using var hi = new SKPaint { Color = SKColors.DeepSkyBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
                canvas.DrawRect(rect, hi);
            }
        }
    }

    private void DrawCover(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
        var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto");
        if (asset != null && File.Exists(asset.Path))
        {
            using var bmp = SKBitmap.Decode(asset.Path);
            if (bmp != null)
            {
                DrawBitmapWithPanZoom(canvas, bmp, bounds, asset);
            }
        }
        else
        {
            using var fill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
            canvas.DrawRect(bounds, fill);
            
            // Draw hint for cover photo
            using var hintPaint = new SKPaint { Color = SKColors.Gray, TextSize = 16, IsAntialias = true };
            var hintText = "Double-click to assign cover photo";
            var textWidth = hintPaint.MeasureText(hintText);
            canvas.DrawText(hintText, bounds.MidX - textWidth / 2, bounds.MidY, hintPaint);
        }

        // subtitle hint
        if (string.IsNullOrWhiteSpace(project.CoverSpec.SubtitleText))
            return;
        using var hint = new SKPaint { Color = new SKColor(0, 0, 0, 60), TextSize = 10, IsAntialias = true };
        var text = "Drag to reposition • Double-tap to center • Use zoom";
        var tw = hint.MeasureText(text);
        canvas.DrawText(text, bounds.MidX - tw / 2, bounds.Top + 40, hint);
    }

    private void DrawBitmapWithPanZoom(SKCanvas canvas, SKBitmap bmp, SKRect rect, ImageAsset asset)
    {
        var imgW = (float)bmp.Width;
        var imgH = (float)bmp.Height;
        var rectW = rect.Width;
        var rectH = rect.Height;
        var imgAspect = imgW / imgH;
        var rectAspect = rectW / rectH;

        // baseline cover scale to avoid letterboxing, then apply zoom
        float scale = (imgAspect > rectAspect ? rectH / imgH : rectW / imgW) * (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);

        var targetW = imgW * scale;
        var targetH = imgH * scale;
        var excessX = Math.Max(0, (targetW - rectW) / 2f);
        var excessY = Math.Max(0, (targetH - rectH) / 2f);

        var px = (float)Math.Clamp(asset.PanX, -1, 1);
        var py = (float)Math.Clamp(asset.PanY, -1, 1);

        // Allow panning on both axes when zoomed exceeds container
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

        // Convert touch location from DIPs to pixels using canvas size vs view width ratio
        var loc = e.Location;
        float density = 1f;
        try
        {
            var canvasSize = _canvas.CanvasSize; // pixels
            if (_canvas.Width > 0)
                density = (float)(canvasSize.Width / (float)_canvas.Width);
        }
        catch { }
        var touchPx = new SKPoint((float)loc.X * density, (float)loc.Y * density);

        var pagePt = new SKPoint((float)((touchPx.X - _pageOffsetX) / _pageScale), (float)((touchPx.Y - _pageOffsetY) / _pageScale));
        var isCover = CoverSwitch.IsToggled;
        var hitRect = isCover ? _lastContentRect : _lastPhotoRect;

        // Helper to get the slot index at a point without changing selection
        int HitTestSlot(SKPoint pt)
        {
            if (isCover) return 0;
            if (_lastPhotoSlots.Count == 0) return -1;
            return _lastPhotoSlots.FindIndex(r => r.Contains(pt));
        }

        // Determine the rect of the currently active slot (may change on Pressed)
        SKRect CurrentTargetRect()
        {
            return isCover ? hitRect : (_lastPhotoSlots.Count > _activeSlotIndex ? _lastPhotoSlots[_activeSlotIndex] : hitRect);
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _isPointerDown = true;

                // Only change selection on an explicit Press inside a slot
                if (!isCover)
                {
                    var hitIdx = HitTestSlot(pagePt);
                    if (hitIdx >= 0 && hitIdx != _activeSlotIndex)
                    {
                        _activeSlotIndex = hitIdx;
                        SyncZoomUI();
                        _canvas.InvalidateSurface();
                    }
                }

                var targetRectPressed = CurrentTargetRect();
                var assetPressed = isCover
                    ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
                    : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex && (a.SlotIndex ?? 0) == _activeSlotIndex);

                _pressedOnAsset = targetRectPressed.Contains(pagePt) && assetPressed != null && File.Exists(assetPressed.Path);
                _dragStartPagePt = pagePt;
                if (_pressedOnAsset)
                {
                    using var bmp = SKBitmap.Decode(assetPressed!.Path);
                    if (bmp != null)
                    {
                        // compute excess based on current zoom (for potential drag)
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
                // Do not change selection on hover/move; only pan if dragging with pressed asset
                if (_isPointerDown && _pressedOnAsset)
                {
                    var assetMove = isCover
                        ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
                        : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex && (a.SlotIndex ?? 0) == _activeSlotIndex);
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

                // Only handle actions if released inside the current target
                var targetRectReleased = CurrentTargetRect();
                if (targetRectReleased.Contains(pagePt))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastTapAt).TotalMilliseconds < 300 && SKPoint.Distance(pagePt, _lastTapPt) < 10)
                    {
                        // Double-click detected - show photo selector
                        _ = ShowPhotoSelectorAsync();
                    }
                    // No single-click reset of pan/zoom
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
        // Helper to draw crisp 1-device-pixel lines and snap coordinates
        float Stroke1px() => 1f / Math.Max(_pageScale, 0.0001f);
        float Snap(float v) => (float)Math.Round(v * _pageScale) / Math.Max(_pageScale, 0.0001f);

        var month = ((project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var year = project.Year + (project.StartMonth - 1 + _monthIndex) / 12;
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
            // Optional: light separators for header
            var x0 = Snap(cell.Left);
            var x1 = Snap(cell.Right);
            var y0 = Snap(cell.Top);
            var y1 = Snap(cell.Bottom);
            canvas.DrawLine(x0, y0, x1, y0, gridPen); // top
            canvas.DrawLine(x0, y1, x1, y1, gridPen); // bottom
            canvas.DrawLine(x0, y0, x0, y1, gridPen); // left
            if (c == 6)
                canvas.DrawLine(x1, y0, x1, y1, gridPen); // right edge only once
        }

        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, bounds.Bottom);
        int rows = weeks.Count;
        if (rows <= 0)
        {
            // Still draw the outer border for the area where days would be
            var xa = Snap(weeksArea.Left); var xb = Snap(weeksArea.Right);
            var ya = Snap(weeksArea.Top); var yb = Snap(weeksArea.Bottom);
            canvas.DrawRect(new SKRect(xa, ya, xb, yb), gridPen);
            return;
        }

        float rowH = weeksArea.Height / rows;

        // Draw day numbers (no per-cell rectangles to avoid double-stroke seams)
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

        // Single-pass grid lines snapped to device pixels
        var wx0 = Snap(weeksArea.Left);
        var wx1 = Snap(weeksArea.Right);
        var wy0 = Snap(weeksArea.Top);
        var wy1 = Snap(weeksArea.Bottom);

        // Vertical lines (including outer edges)
        for (int c = 0; c <= 7; c++)
        {
            var x = Snap(weeksArea.Left + c * colW);
            canvas.DrawLine(x, wy0, x, wy1, gridPen);
        }
        // Horizontal lines (including outer edges)
        for (int r = 0; r <= rows; r++)
        {
            var y = Snap(weeksArea.Top + r * rowH);
            canvas.DrawLine(wx0, y, wx1, y, gridPen);
        }

        // Final outer border reinforcement
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
