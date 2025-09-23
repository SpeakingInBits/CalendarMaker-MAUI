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

    // gesture helpers
    private float _pageScale = 1f;
    private float _pageOffsetX, _pageOffsetY;
    private SKRect _lastPhotoRect;
    private SKRect _lastContentRect;
    private bool _isDragging;
    private SKPoint _dragStartPagePt;
    private double _startPanX, _startPanY, _startZoom;
    private float _dragExcessX, _dragExcessY;
    private bool _dragIsCover;
    private DateTime _lastTapAt = DateTime.MinValue;
    private SKPoint _lastTapPt;

    public string? ProjectId { get; set; }

    public DesignerPage(ICalendarEngine engine, IProjectStorageService storage, IAssetService assets, IPdfExportService pdf)
    {
        InitializeComponent();
        _engine = engine;
        _storage = storage;
        _assets = assets;
        _pdf = pdf;
        _canvas = new SKCanvasView();
        _canvas.PaintSurface += Canvas_PaintSurface;
        _canvas.EnableTouchEvents = true;
        _canvas.Touch += OnCanvasTouch;
        _canvas.Loaded += (_, __) => TryHookKeys();
        CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");
        PrevBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 11) % 12; SyncZoomUI(); UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        NextBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 1) % 12; SyncZoomUI(); UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        AddPhotoBtn.Clicked += async (_, __) => await PickAndAssignPhotoAsync();
        AddCoverPhotoBtn.Clicked += async (_, __) => await PickAndAssignCoverPhotoAsync();

        CoverSwitch.Toggled += (_, __) => { SyncZoomUI(); _canvas.InvalidateSurface(); };
        TitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.TitleText = TitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        SubtitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.SubtitleText = SubtitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        FlipBtn.Clicked += (_, __) => FlipLayout();
        SplitSlider.ValueChanged += (_, e) => { SplitValueLabel.Text = e.NewValue.ToString("P0"); if (_project != null) { _project.LayoutSpec.SplitRatio = e.NewValue; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        ZoomSlider.ValueChanged += (_, e) => { ZoomValueLabel.Text = $"{e.NewValue:F2}x"; UpdateAssetZoom(e.NewValue); };
        StartMonthPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.StartMonth = StartMonthPicker.SelectedIndex + 1; _monthIndex = 0; SyncZoomUI(); UpdateMonthLabel(); _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        FirstDowPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.FirstDayOfWeek = (DayOfWeek)FirstDowPicker.SelectedIndex; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        _monthIndex = 0; // start with first month
        PopulateStaticPickers();
    }

    private void TryHookKeys()
    {
#if WINDOWS
        try
        {
            var fe = _canvas.Handler?.PlatformView as Microsoft.Maui.Platform.ContentPanel;
            if (fe != null)
            {
                fe.IsTabStop = true;
                fe.KeyDown += OnCanvasKeyDown;
            }
        }
        catch { }
#endif
    }

    private void UpdateMonthLabel()
    {
        if (_project == null) { MonthLabel.Text = string.Empty; return; }
        var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var year = _project.Year + (_project.StartMonth - 1 + _monthIndex) / 12;
        MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
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

    private void PopulateStaticPickers()
    {
        StartMonthPicker.ItemsSource = Enumerable.Range(1, 12).Select(i => new DateTime(2000, i, 1).ToString("MMMM", CultureInfo.InvariantCulture)).ToList();
        FirstDowPicker.ItemsSource = Enum.GetNames(typeof(DayOfWeek));
    }

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

    private async Task PickAndAssignCoverPhotoAsync()
    {
        if (_project == null) return;
        var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select a cover photo", FileTypes = FilePickerFileType.Images });
        if (result == null) return;
        var asset = await _assets.ImportMonthPhotoAsync(_project, -1, result);
        if (asset != null)
        {
            asset.Role = "coverPhoto";
            asset.MonthIndex = null;
            asset.PanX = asset.PanY = 0; asset.Zoom = 1;
            await _storage.UpdateProjectAsync(_project);
            SyncZoomUI();
            _canvas.InvalidateSurface();
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_project == null) return;
        var bytes = await _pdf.ExportMonthAsync(_project, _monthIndex);
        var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var fileName = $"Calendar_{_project.Year}_{month:00}.pdf";
        await SaveBytesAsync(fileName, bytes);
    }

    private async void OnExportCoverClicked(object? sender, EventArgs e)
    {
        if (_project == null) return;
        var bytes = await _pdf.ExportCoverAsync(_project);
        var fileName = $"Calendar_{_project.Year}_Cover.pdf";
        await SaveBytesAsync(fileName, bytes);
    }

    private async void OnExportYearClicked(object? sender, EventArgs e)
    {
        if (_project == null) return;
        var bytes = await _pdf.ExportYearAsync(_project, includeCover: true);
        var fileName = $"Calendar_{_project.Year}_FullYear.pdf";
        await SaveBytesAsync(fileName, bytes);
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

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_project == null && !string.IsNullOrEmpty(ProjectId))
        {
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
                SyncZoomUI();
            }
            UpdateMonthLabel();
            _canvas.InvalidateSurface();
        }
    }

    private void SyncZoomUI()
    {
        var asset = GetActiveAsset();
        if (asset != null)
        {
            ZoomSlider.Value = Math.Clamp(asset.Zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
            ZoomValueLabel.Text = $"{ZoomSlider.Value:F2}x";
        }
    }

    private ImageAsset? GetActiveAsset()
    {
        if (_project == null) return null;
        return CoverSwitch.IsToggled
            ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
            : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex);
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

    private async Task PickAndAssignPhotoAsync()
    {
        if (_project == null) return;
        var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select a photo", FileTypes = FilePickerFileType.Images });
        if (result == null) return;
        var asset = await _assets.ImportMonthPhotoAsync(_project, _monthIndex, result);
        if (asset != null)
        {
            asset.PanX = asset.PanY = 0; asset.Zoom = 1;
            await _storage.UpdateProjectAsync(_project);
            SyncZoomUI();
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
        }
        else
        {
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, _project.LayoutSpec);
            _lastPhotoRect = photoRect;
            DrawPhoto(canvas, photoRect);
            DrawCalendarGrid(canvas, calRect, _project);
        }

        canvas.Restore();
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
            canvas.DrawRect(bounds, new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) });
        }

        // subtle hint overlay when centered and zoom = 1
        DrawDragHint(canvas, bounds, asset);

        // Title / Subtitle
        using var titlePaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = (float)project.Theme.TitleFontSizePt, IsAntialias = true };
        using var subtitlePaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = (float)project.Theme.SubtitleFontSizePt, IsAntialias = true };
        var title = project.CoverSpec.TitleText ?? string.Empty;
        var subtitle = project.CoverSpec.SubtitleText ?? string.Empty;
        var tw = titlePaint.MeasureText(title);
        canvas.DrawText(title, bounds.MidX - tw / 2, bounds.Top + titlePaint.TextSize + 10, titlePaint);
        var sw = subtitlePaint.MeasureText(subtitle);
        canvas.DrawText(subtitle, bounds.MidX - sw / 2, bounds.Top + titlePaint.TextSize + 20 + subtitlePaint.TextSize, subtitlePaint);
    }

    private void DrawPhoto(SKCanvas canvas, SKRect rect)
    {
        if (_project == null) return;
        var asset = _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex);
        if (asset == null || string.IsNullOrEmpty(asset.Path) || !File.Exists(asset.Path))
        {
            using var photoFill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
            canvas.DrawRect(rect, photoFill);
            using var photoBorder = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
            canvas.DrawRect(rect, photoBorder);
            return;
        }

        using var bmp = SKBitmap.Decode(asset.Path);
        if (bmp == null)
        {
            canvas.DrawRect(rect, new SKPaint { Color = SKColors.LightGray });
            return;
        }

        DrawBitmapWithPanZoom(canvas, bmp, rect, asset);
        DrawDragHint(canvas, rect, asset);
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

    private void DrawDragHint(SKCanvas canvas, SKRect rect, ImageAsset? asset)
    {
        if (asset == null) return;
        if (_isDragging) return;
        if (Math.Abs(asset.PanX) > 0.001 || Math.Abs(asset.PanY) > 0.001 || Math.Abs(asset.Zoom - 1) > 0.001)
            return;
        using var hint = new SKPaint { Color = new SKColor(0, 0, 0, 60), TextSize = 10, IsAntialias = true };
        var text = "Drag to reposition • Double-click to center • Use zoom";
        var tw = hint.MeasureText(text);
        canvas.DrawText(text, rect.MidX - tw / 2, rect.MidY, hint);
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (_project == null)
            return;

        var pagePt = new SKPoint((float)((e.Location.X - _pageOffsetX) / _pageScale), (float)((e.Location.Y - _pageOffsetY) / _pageScale));
        var isCover = CoverSwitch.IsToggled;
        var hitRect = isCover ? _lastContentRect : _lastPhotoRect;
        var asset = isCover
            ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
            : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                if (hitRect.Contains(pagePt) && asset != null && File.Exists(asset.Path))
                {
                    using var bmp = SKBitmap.Decode(asset.Path);
                    if (bmp != null)
                    {
                        // compute excess based on current zoom
                        var imgW = (float)bmp.Width; var imgH = (float)bmp.Height;
                        var rectW = hitRect.Width; var rectH = hitRect.Height;
                        var imgAspect = imgW / imgH; var rectAspect = rectW / rectH;
                        float baseScale = imgAspect > rectAspect ? rectH / imgH : rectW / imgW;
                        float scale = baseScale * (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);
                        var targetW = imgW * scale; var targetH = imgH * scale;
                        _dragExcessX = Math.Max(0, (targetW - rectW) / 2f);
                        _dragExcessY = Math.Max(0, (targetH - rectH) / 2f);

                        _isDragging = true;
                        _dragStartPagePt = pagePt;
                        _startPanX = asset.PanX;
                        _startPanY = asset.PanY;
                        _startZoom = asset.Zoom;
                        _dragIsCover = isCover;
                        e.Handled = true;
                        return;
                    }
                }
                break;
            case SKTouchAction.Moved:
                if (_isDragging && asset != null)
                {
                    var dx = pagePt.X - _dragStartPagePt.X;
                    var dy = pagePt.Y - _dragStartPagePt.Y;
                    double newPanX = _startPanX + (_dragExcessX > 0 ? dx / _dragExcessX : 0);
                    double newPanY = _startPanY + (_dragExcessY > 0 ? dy / _dragExcessY : 0);
                    asset.PanX = Math.Clamp(newPanX, -1, 1);
                    asset.PanY = Math.Clamp(newPanY, -1, 1);
                    _canvas.InvalidateSurface();
                    e.Handled = true;
                    return;
                }
                break;
            case SKTouchAction.Released:
                if (_isDragging)
                {
                    _isDragging = false;
                    _ = _storage.UpdateProjectAsync(_project);
                    e.Handled = true;
                    return;
                }
                // detect double-click to reset
                if (hitRect.Contains(pagePt))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastTapAt).TotalMilliseconds < 300 && SKPoint.Distance(pagePt, _lastTapPt) < 10)
                    {
                        if (asset != null)
                        {
                            asset.PanX = asset.PanY = 0; asset.Zoom = 1;
                            SyncZoomUI();
                            _ = _storage.UpdateProjectAsync(_project);
                            _canvas.InvalidateSurface();
                        }
                    }
                    _lastTapAt = now;
                    _lastTapPt = pagePt;
                }
                break;
            case SKTouchAction.Cancelled:
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

        using var gridPen = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
        using var textPaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = 10, IsAntialias = true };

        float colW = dowRect.Width / 7f;
        for (int c = 0; c < 7; c++)
        {
            var cell = new SKRect(dowRect.Left + c * colW, dowRect.Top, dowRect.Left + (c + 1) * colW, dowRect.Bottom);
            var t = displayDows[c];
            var tw = textPaint.MeasureText(t);
            canvas.DrawText(t, cell.MidX - tw / 2, cell.MidY + textPaint.TextSize / 2.5f, textPaint);
            canvas.DrawRect(cell, gridPen);
        }

        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, bounds.Bottom);
        int rows = weeks.Count;
        float rowH = weeksArea.Height / rows;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                var cell = new SKRect(weeksArea.Left + c * colW, weeksArea.Top + r * rowH, weeksArea.Left + (c + 1) * colW, weeksArea.Top + (r + 1) * rowH);
                canvas.DrawRect(cell, gridPen);
                var date = weeks[r][c];
                if (date.HasValue && date.Value.Month == month)
                {
                    var dayStr = date.Value.Day.ToString(CultureInfo.InvariantCulture);
                    canvas.DrawText(dayStr, cell.Left + 2, cell.Top + textPaint.TextSize + 2, textPaint);
                }
            }
        }
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
