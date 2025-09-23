using System.Globalization;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using Microsoft.Maui.Storage;
using SkiaSharp;
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
        CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");
        PrevBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 11) % 12; UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        NextBtn.Clicked += (_, __) => { _monthIndex = (_monthIndex + 1) % 12; UpdateMonthLabel(); _canvas.InvalidateSurface(); };
        AddPhotoBtn.Clicked += async (_, __) => await PickAndAssignPhotoAsync();

        _monthIndex = 0; // start with first month
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        await ExportCurrentMonthAsync();
    }

    private async Task ExportCurrentMonthAsync()
    {
        if (_project == null) return;
        var bytes = await _pdf.ExportMonthAsync(_project, _monthIndex);
        var dir = FileSystem.Current.AppDataDirectory;
        var month = ((_project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var fileName = $"Calendar_{_project.Year}_{month:00}.pdf";
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Exported PDF",
            File = new ShareFile(path)
        });
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_project == null && !string.IsNullOrEmpty(ProjectId))
        {
            var projects = await _storage.GetProjectsAsync();
            _project = projects.FirstOrDefault(p => p.Id == ProjectId);
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

    private void Canvas_PaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        if (_project is null)
            return;

        // Calculate page in points and scale to canvas
        var (pageWpt, pageHpt) = PageSizes.GetPoints(_project.PageSpec);
        if (pageWpt <= 0 || pageHpt <= 0)
        {
            pageWpt = 612; // Letter default
            pageHpt = 792;
        }
        var scale = Math.Min(e.Info.Width / (float)pageWpt, e.Info.Height / (float)pageHpt);
        var offsetX = (e.Info.Width - (float)pageWpt * scale) / 2f;
        var offsetY = (e.Info.Height - (float)pageHpt * scale) / 2f;

        // Transform so that drawing in page points maps to pixels
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale((float)scale);

        var pageRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);

        using var pageBorder = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
        canvas.DrawRect(pageRect, pageBorder);

        // Margins in points
        var m = _project.Margins;
        var contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, (float)pageWpt - (float)m.RightPt, (float)pageHpt - (float)m.BottomPt);

        using var contentBorder = new SKPaint { Color = SKColors.Silver, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
        canvas.DrawRect(contentRect, contentBorder);

        // Split regions
        (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, _project.LayoutSpec);

        // Photo rendering
        DrawPhoto(canvas, photoRect);

        // Calendar grid
        DrawCalendarGrid(canvas, calRect, _project);

        canvas.Restore();
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

        // Cover / Contain based on project.LayoutSpec.PhotoFill
        SKRect dest = rect;
        var imgW = (float)bmp.Width;
        var imgH = (float)bmp.Height;
        var rectW = rect.Width;
        var rectH = rect.Height;
        var imgAspect = imgW / imgH;
        var rectAspect = rectW / rectH;

        if (_project.LayoutSpec.PhotoFill == PhotoFillMode.Cover)
        {
            // Scale to fill the rect (crop)
            if (imgAspect > rectAspect)
            {
                // Image is wider, crop width
                var targetH = rectH;
                var scale = targetH / imgH;
                var targetW = imgW * scale;
                var excess = (targetW - rectW) / 2f;
                dest = new SKRect(rect.Left - excess, rect.Top, rect.Left - excess + targetW, rect.Top + targetH);
            }
            else
            {
                // Image is taller, crop height
                var targetW = rectW;
                var scale = targetW / imgW;
                var targetH = imgH * scale;
                var excess = (targetH - rectH) / 2f;
                dest = new SKRect(rect.Left, rect.Top - excess, rect.Left + targetW, rect.Top - excess + targetH);
            }
        }
        else
        {
            // Contain: fully fit into rect (letterbox)
            if (imgAspect > rectAspect)
            {
                var targetW = rectW;
                var scale = targetW / imgW;
                var targetH = imgH * scale;
                var pad = (rectH - targetH) / 2f;
                dest = new SKRect(rect.Left, rect.Top + pad, rect.Left + targetW, rect.Top + pad + targetH);
            }
            else
            {
                var targetH = rectH;
                var scale = targetH / imgH;
                var targetW = imgW * scale;
                var pad = (rectW - targetW) / 2f;
                dest = new SKRect(rect.Left + pad, rect.Top, rect.Left + pad + targetW, rect.Top + targetH);
            }
        }

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(bmp, dest, paint);
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
        // Determine month/year
        var month = ((project.StartMonth - 1 + _monthIndex) % 12) + 1;
        var year = project.Year + (project.StartMonth - 1 + _monthIndex) / 12;

        var weeks = _engine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

        // Header area height in points
        float headerH = 40;
        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + headerH);
        var gridRect = new SKRect(bounds.Left, headerRect.Bottom, bounds.Right, bounds.Bottom);

        // Header text
        using var titlePaint = new SKPaint
        {
            Color = SKColor.Parse(project.Theme.PrimaryTextColor),
            TextSize = 18,
            IsAntialias = true
        };
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var titleWidth = titlePaint.MeasureText(title);
        canvas.DrawText(title, gridRect.MidX - titleWidth / 2, headerRect.MidY + titlePaint.TextSize / 2.5f, titlePaint);

        // Day of week headers
        float dowH = 20;
        var dowRect = new SKRect(gridRect.Left, gridRect.Top, gridRect.Right, gridRect.Top + dowH);

        string[] dows = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        // Rotate based on first day
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

        // Weeks grid cells
        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, gridRect.Bottom);
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
}
