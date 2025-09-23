namespace CalendarMaker_MAUI.Services;

using System.Globalization;
using CalendarMaker_MAUI.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;

public interface IPdfExportService
{
    Task<byte[]> ExportMonthAsync(CalendarProject project, int monthIndex);
    Task<byte[]> ExportCoverAsync(CalendarProject project);
    Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true);
}

public sealed class PdfExportService : IPdfExportService
{
    // Render target DPI for raster export. Increase for sharper text (larger files).
    private const float TargetDpi = 300f; // 300 DPI print quality

    public Task<byte[]> ExportMonthAsync(CalendarProject project, int monthIndex)
        => RenderDocumentAsync(project, new[] { (monthIndex, false) });

    public Task<byte[]> ExportCoverAsync(CalendarProject project)
        => RenderDocumentAsync(project, new[] { (0, true) });

    public Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true)
    {
        var pages = new List<(int idx, bool cover)>();
        if (includeCover) pages.Add((0, true));
        for (int i = 0; i < 12; i++) pages.Add((i, false));
        return RenderDocumentAsync(project, pages);
    }

    private Task<byte[]> RenderDocumentAsync(CalendarProject project, IEnumerable<(int idx, bool cover)> pages)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var (wPtD, hPtD) = CalendarMaker_MAUI.Models.PageSizes.GetPoints(project.PageSpec);
        float pageWpt = (float)wPtD;
        float pageHpt = (float)hPtD;
        if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }

        // Pre-render bitmaps (PNG bytes) at high DPI for each requested page
        var rendered = pages.Select(p => RenderPageToPng(project, p.idx, p.cover, pageWpt, pageHpt, TargetDpi)).ToList();

        var doc = Document.Create(container =>
        {
            foreach (var img in rendered)
            {
                container.Page(page =>
                {
                    page.Size(new QuestPDF.Helpers.PageSize(pageWpt, pageHpt));
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.Content().Image(img).FitArea();
                });
            }
        });

        using var stream = new MemoryStream();
        doc.GeneratePdf(stream);
        return Task.FromResult(stream.ToArray());
    }

    private byte[] RenderPageToPng(CalendarProject project, int monthIndex, bool renderCover, float pageWpt, float pageHpt, float targetDpi)
    {
        // Points to pixels scale factor
        float scale = targetDpi / 72f;
        int widthPx = Math.Max(1, (int)Math.Round(pageWpt * scale));
        int heightPx = Math.Max(1, (int)Math.Round(pageHpt * scale));

        using var skSurface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul));
        var sk = skSurface.Canvas;
        sk.Clear(SKColors.White);
        // Draw using page points coordinate system scaled to target DPI
        sk.Scale(scale);

        var m = project.Margins;
        var contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, pageWpt - (float)m.RightPt, pageHpt - (float)m.BottomPt);

        if (renderCover)
        {
            DrawCover(sk, contentRect, project);
        }
        else
        {
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, project.LayoutSpec);
            DrawPhoto(sk, photoRect, project, monthIndex);
            DrawCalendarGrid(sk, calRect, project, monthIndex);
        }

        sk.Flush();
        using var snapshot = skSurface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static (SKRect photo, SKRect cal) ComputeSplit(SKRect area, LayoutSpec spec)
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

    private static void DrawPhoto(SKCanvas canvas, SKRect rect, CalendarProject project, int monthIndex)
    {
        var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "monthPhoto" && a.MonthIndex == monthIndex);
        if (asset == null || string.IsNullOrEmpty(asset.Path) || !File.Exists(asset.Path))
        {
            canvas.DrawRect(rect, new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) });
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
        SKRect dest = rect;

        if (project.LayoutSpec.PhotoFill == PhotoFillMode.Cover)
        {
            if (imgAspect > rectAspect)
            {
                var targetH = rectH; var scale = targetH / imgH; var targetW = imgW * scale; var excess = (targetW - rectW) / 2f;
                dest = new SKRect(rect.Left - excess, rect.Top, rect.Left - excess + targetW, rect.Top + targetH);
            }
            else
            {
                var targetW = rectW; var scale = targetW / imgW; var targetH = imgH * scale; var excess = (targetH - rectH) / 2f;
                dest = new SKRect(rect.Left, rect.Top - excess, rect.Left + targetW, rect.Top - excess + targetH);
            }
        }
        else
        {
            if (imgAspect > rectAspect)
            {
                var targetW = rectW; var scale = targetW / imgW; var targetH = imgH * scale; var pad = (rectH - targetH) / 2f;
                dest = new SKRect(rect.Left, rect.Top + pad, rect.Left + targetW, rect.Top + pad + targetH);
            }
            else
            {
                var targetH = rectH; var scale = targetH / imgH; var targetW = imgW * scale; var pad = (rectW - targetW) / 2f;
                dest = new SKRect(rect.Left + pad, rect.Top, rect.Left + pad + targetW, rect.Top + targetH);
            }
        }

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static void DrawCover(SKCanvas canvas, SKRect bounds, CalendarProject project)
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
                if (project.LayoutSpec.PhotoFill == PhotoFillMode.Cover)
                {
                    if (imgAspect > rectAspect)
                    {
                        var targetH = bounds.Height; var scale = targetH / bmp.Height; var targetW = (float)bmp.Width * scale; var excess = (targetW - bounds.Width) / 2f;
                        dest = new SKRect(bounds.Left - excess, bounds.Top, bounds.Left - excess + targetW, bounds.Top + targetH);
                    }
                    else
                    {
                        var targetW = bounds.Width; var scale = targetW / bmp.Width; var targetH = (float)bmp.Height * scale; var excess = (targetH - bounds.Height) / 2f;
                        dest = new SKRect(bounds.Left, bounds.Top - excess, bounds.Left + targetW, bounds.Top - excess + targetH);
                    }
                }
                else
                {
                    if (imgAspect > rectAspect)
                    {
                        var targetW = bounds.Width; var scale = targetW / bmp.Width; var targetH = (float)bmp.Height * scale; var pad = (bounds.Height - targetH) / 2f;
                        dest = new SKRect(bounds.Left, bounds.Top + pad, bounds.Left + targetW, bounds.Top + pad + targetH);
                    }
                    else
                    {
                        var targetH = bounds.Height; var scale = targetH / bmp.Height; var targetW = (float)bmp.Width * scale; var pad = (bounds.Width - targetW) / 2f;
                        dest = new SKRect(bounds.Left + pad, bounds.Top, bounds.Left + pad + targetW, bounds.Top + targetH);
                    }
                }
                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
                canvas.DrawBitmap(bmp, dest, paint);
            }
        }
        else
        {
            canvas.DrawRect(bounds, new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) });
        }

        using var titlePaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = (float)project.Theme.TitleFontSizePt, IsAntialias = true };
        using var subtitlePaint = new SKPaint { Color = SKColor.Parse(project.Theme.PrimaryTextColor), TextSize = (float)project.Theme.SubtitleFontSizePt, IsAntialias = true };
        var title = project.CoverSpec.TitleText ?? string.Empty;
        var subtitle = project.CoverSpec.SubtitleText ?? string.Empty;
        var tw = titlePaint.MeasureText(title);
        canvas.DrawText(title, bounds.MidX - tw / 2, bounds.Top + titlePaint.TextSize + 10, titlePaint);
        var sw = subtitlePaint.MeasureText(subtitle);
        canvas.DrawText(subtitle, bounds.MidX - sw / 2, bounds.Top + titlePaint.TextSize + 20 + subtitlePaint.TextSize, subtitlePaint);
    }

    private static void DrawCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int monthIndex)
    {
        var month = ((project.StartMonth - 1 + monthIndex) % 12) + 1;
        var year = project.Year + (project.StartMonth - 1 + monthIndex) / 12;
        var engine = new CalendarEngine();
        var weeks = engine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

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
