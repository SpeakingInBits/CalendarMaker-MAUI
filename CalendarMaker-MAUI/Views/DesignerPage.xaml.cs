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
    private double _startPanX, _startPanY;
    private float _dragExcessX, _dragExcessY;
    private bool _dragIsCover;

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
        CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");
        PrevBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 11) % 12; UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        NextBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 1) % 12; UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        AddPhotoBtn.Clicked += async (_, __) => await PickAndAssignPhotoAsync();
        AddCoverPhotoBtn.Clicked += async (_, __) => await PickAndAssignCoverPhotoAsync();

        CoverSwitch.Toggled += (_, __) => _canvas.InvalidateSurface();
        TitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.TitleText = TitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        SubtitleEntry.TextChanged += (_, __) => { if (_project != null) { _project.CoverSpec.SubtitleText = SubtitleEntry.Text; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        FlipBtn.Clicked += (_, __) => FlipLayout();
        SplitSlider.ValueChanged += (_, e) => { SplitValueLabel.Text = e.NewValue.ToString("P0"); if (_project != null) { _project.LayoutSpec.SplitRatio = e.NewValue; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        StartMonthPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.StartMonth = StartMonthPicker.SelectedIndex + 1; _monthIndex = 0; UpdateMonthLabel(); _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };
        FirstDowPicker.SelectedIndexChanged += (_, __) => { if (_project != null) { _project.FirstDayOfWeek = (DayOfWeek)FirstDowPicker.SelectedIndex; _ = _storage.UpdateProjectAsync(_project); } _canvas.InvalidateSurface(); };

        _monthIndex = 0; // start with first month
        PopulateStaticPickers();
    }

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
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select a cover photo",
            FileTypes = FilePickerFileType.Images
        });
        if (result == null) return;
        var asset = await _assets.ImportMonthPhotoAsync(_project, -1, result);
        if (asset != null)
        {
            asset.Role = "coverPhoto";
            asset.MonthIndex = null;
            await _storage.UpdateProjectAsync(_project);
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
            await DisplayAlert("Save Failed", result.Exception?.Message ?? "Unknown error", "OK");
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
            }
            UpdateMonthLabel();
            _canvas.InvalidateSurface();
        }
    }

    private void UpdateMonthLabel()
    {
        if (_project == null) { MonthLabel.Text = string.Empty; return; }
        var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var year = _project.Year + (_project.StartMonth - 1 + _monthIndex) / 12;
        MonthLabel.Text = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }

    private async Task PickAndAssignPhotoAsync()
    {
        if (_project == null) return;
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select a photo",
            FileTypes = FilePickerFileType.Images
        });
        if (result == null) return;
        var asset = await _assets.ImportMonthPhotoAsync(_project, _monthIndex, result);
        if (asset != null)
        {
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
        if (pageWpt <= 0 || pageHpt <= 0)
        {
            pageWpt = 612; pageHpt = 792;
        }
        var scale = Math.Min(e.Info.Width / (float)pageWpt, e.Info.Height / (float)pageHpt);
        var offsetX = (e.Info.Width - (float)pageWpt * scale) / 2f;
        var offsetY = (e.Info.Height - (float)pageHpt * scale) / 2f;

        _pageScale = scale;
        _pageOffsetX = offsetX;
        _pageOffsetY = offsetY;

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
                var imgAspect = (float)bmp.Width / (float)bmp.Height;
                var rectAspect = bounds.Width / bounds.Height;
                SKRect dest;
                if (imgAspect > rectAspect)
                {
                    var targetH = bounds.Height; var scale = targetH / bmp.Height; var targetW = (float)bmp.Width * scale; var excess = (targetW - bounds.Width) / 2f;
                    var px = (float)Math.Clamp(asset.PanX, -1, 1);
                    dest = new SKRect(bounds.Left - excess + px * excess, bounds.Top, bounds.Left - excess + px * excess + targetW, bounds.Top + targetH);
                }
                else
                {
                    var targetW = bounds.Width; var scale = targetW / bmp.Width; var targetH = (float)bmp.Height * scale; var excess = (targetH - bounds.Height) / 2f;
                    var py = (float)Math.Clamp(asset.PanY, -1, 1);
                    dest = new SKRect(bounds.Left, bounds.Top - excess + py * excess, bounds.Left + targetW, bounds.Top - excess + py * excess + targetH);
                }
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                canvas.Save();
                canvas.ClipRect(bounds, antialias: true);
                canvas.DrawBitmap(bmp, dest, paint);
                canvas.Restore();
            }
        }
        else
        {
            canvas.DrawRect(bounds, new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) });
        }

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

        var imgW = (float)bmp.Width;
        var imgH = (float)bmp.Height;
        var rectW = rect.Width;
        var rectH = rect.Height;
        var imgAspect = imgW / imgH;
        var rectAspect = rectW / rectH;
        SKRect dest;

        if (imgAspect > rectAspect)
        {
            var targetH = rectH; var scale = targetH / imgH; var targetW = imgW * scale; var excess = (targetW - rectW) / 2f;
            var px = (float)Math.Clamp(asset.PanX, -1, 1);
            dest = new SKRect(rect.Left - excess + px * excess, rect.Top, rect.Left - excess + px * excess + targetW, rect.Top + targetH);
        }
        else
        {
            var targetW = rectW; var scale = targetW / imgW; var targetH = imgH * scale; var excess = (targetH - rectH) / 2f;
            var py = (float)Math.Clamp(asset.PanY, -1, 1);
            dest = new SKRect(rect.Left, rect.Top - excess + py * excess, rect.Left + targetW, rect.Top - excess + py * excess + targetH);
        }

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.Save();
        canvas.ClipRect(rect, antialias: true);
        canvas.DrawBitmap(bmp, dest, paint);
        canvas.Restore();
    }

    private (SKRect photo, SKRect cal) ComputeSplit(SKRect area, LayoutSpec spec)
    {
        var ratio = (float)Math.Clamp(spec.SplitRatio, 0.1, 0.9);
        return spec.Placement switch
        {
            LayoutPlacement.PhotoLeftCalendarRight =>
                (new SKRect(area.Left, area.Top, area.Left + area.Width * ratio, area.Bottom),
                 new SKRect(area.Left + area.Width * ratio, area.Top, area.Right, area.Bottom)),
            LayoutPlacement.PhotoRightCalendarLeft =>
                (new SKRect(area.Left + area.Width * (1 - ratio), area.Top, area.Right, area.Bottom),
                 new SKRect(area.Left, area.Top, area.Left + area.Width * (1 - ratio), area.Bottom)),
            LayoutPlacement.PhotoTopCalendarBottom =>
                (new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * ratio),
                 new SKRect(area.Left, area.Top + area.Height * ratio, area.Right, area.Bottom)),
            LayoutPlacement.PhotoBottomCalendarTop =>
                (new SKRect(area.Left, area.Top + area.Height * (1 - ratio), area.Right, area.Bottom),
                 new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * (1 - ratio))),
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

        using var titlePaint = new SKPaint
        {
            Color = SKColor.Parse(project.Theme.PrimaryTextColor),
            TextSize = 18,
            IsAntialias = true
        };
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

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (_project == null)
            return;

        // map to page coordinate system
        var pagePt = new SKPoint((float)((e.Location.X - _pageOffsetX) / _pageScale), (float)((e.Location.Y - _pageOffsetY) / _pageScale));
        var isCover = CoverSwitch.IsToggled;
        var hitRect = isCover ? _lastContentRect : _lastPhotoRect;

        if (e.ActionType == SKTouchAction.Pressed)
        {
            if (hitRect.Contains(pagePt))
            {
                var asset = isCover
                    ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
                    : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    using var bmp = SKBitmap.Decode(asset.Path);
                    if (bmp != null)
                    {
                        // compute excess for current rect
                        var imgAspect = (float)bmp.Width / (float)bmp.Height;
                        var rectAspect = hitRect.Width / hitRect.Height;
                        if (imgAspect > rectAspect)
                        {
                            var targetH = hitRect.Height; var scale = targetH / bmp.Height; var targetW = (float)bmp.Width * scale; _dragExcessX = (targetW - hitRect.Width) / 2f; _dragExcessY = 0f;
                        }
                        else
                        {
                            var targetW = hitRect.Width; var scale = targetW / bmp.Width; var targetH = (float)bmp.Height * scale; _dragExcessY = (targetH - hitRect.Height) / 2f; _dragExcessX = 0f;
                        }
                        _isDragging = true;
                        _dragStartPagePt = pagePt;
                        _startPanX = asset.PanX;
                        _startPanY = asset.PanY;
                        _dragIsCover = isCover;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
        else if (e.ActionType == SKTouchAction.Moved && _isDragging)
        {
            var dx = pagePt.X - _dragStartPagePt.X;
            var dy = pagePt.Y - _dragStartPagePt.Y;
            var asset = _dragIsCover
                ? _project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto")
                : _project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == _monthIndex);
            if (asset != null)
            {
                double newPanX = _startPanX + (_dragExcessX > 0 ? dx / _dragExcessX : 0);
                double newPanY = _startPanY + (_dragExcessY > 0 ? dy / _dragExcessY : 0);
                asset.PanX = Math.Clamp(newPanX, -1, 1);
                asset.PanY = Math.Clamp(newPanY, -1, 1);
                _canvas.InvalidateSurface();
            }
            e.Handled = true;
            return;
        }
        else if ((e.ActionType == SKTouchAction.Released || e.ActionType == SKTouchAction.Cancelled) && _isDragging)
        {
            _isDragging = false;
            _ = _storage.UpdateProjectAsync(_project);
            e.Handled = true;
            return;
        }

        e.Handled = false;
    }
}
