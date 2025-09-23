using System.Globalization;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace CalendarMaker_MAUI.Views;

[QueryProperty(nameof(ProjectId), "projectId")]
public partial class DesignerPage : ContentPage
{
    private CalendarProject? _project;
    private readonly ICalendarEngine _engine;
    private readonly IProjectStorageService _storage;
    private readonly SKCanvasView _canvas;
    private int _monthIndex; // 0..11 relative to StartMonth

    public string? ProjectId { get; set; }

    public DesignerPage(ICalendarEngine engine, IProjectStorageService storage)
    {
        InitializeComponent();
        _engine = engine;
        _storage = storage;
        _canvas = new SKCanvasView();
        _canvas.PaintSurface += Canvas_PaintSurface;
        CanvasHost.Content = _canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");

        _monthIndex = 0; // start with first month
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_project == null && !string.IsNullOrEmpty(ProjectId))
        {
            var projects = await _storage.GetProjectsAsync();
            _project = projects.FirstOrDefault(p => p.Id == ProjectId);
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

        // Draw photo placeholder
        using var photoFill = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
        canvas.DrawRect(photoRect, photoFill);
        using var photoBorder = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f / (float)scale };
        canvas.DrawRect(photoRect, photoBorder);

        // Draw calendar grid
        DrawCalendarGrid(canvas, calRect, _project);

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
