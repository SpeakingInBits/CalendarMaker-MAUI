namespace CalendarMaker_MAUI.Services;

using System.Globalization;
using CalendarMaker_MAUI.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;

public class ExportProgress
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanCancel { get; set; } = true;
}

public interface IPdfExportService
{
    Task<byte[]> ExportMonthAsync(CalendarProject project, int monthIndex);
    Task<byte[]> ExportCoverAsync(CalendarProject project);
    Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PdfExportService : IPdfExportService
{
    private const float TargetDpi = 300f; // 300 DPI print quality

    public Task<byte[]> ExportMonthAsync(CalendarProject project, int monthIndex)
        => RenderDocumentAsync(project, new[] { (monthIndex, false) }, null, default);

    public Task<byte[]> ExportCoverAsync(CalendarProject project)
        => RenderDocumentAsync(project, new[] { (0, true) }, null, default);

    public Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var pages = new List<(int idx, bool cover)>();
        if (includeCover) pages.Add((0, true));
        for (int i = 0; i < 12; i++) pages.Add((i, false));
        return RenderDocumentAsync(project, pages, progress, cancellationToken);
    }

    private Task<byte[]> RenderDocumentAsync(CalendarProject project, IEnumerable<(int idx, bool cover)> pages, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var (wPtD, hPtD) = CalendarMaker_MAUI.Models.PageSizes.GetPoints(project.PageSpec);
            float pageWpt = (float)wPtD;
            float pageHpt = (float)hPtD;
            if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }

            var pagesList = pages.ToList();
            var totalPages = pagesList.Count;

            // Image cache to avoid re-decoding same images
            var imageCache = new Dictionary<string, SKBitmap>();
            
            try
            {
                // Stream processing: render pages one at a time and add to document
                var doc = Document.Create(container =>
                {
                    int currentPage = 0;
                    foreach (var p in pagesList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        currentPage++;
                        
                        // Report progress
                        if (progress != null)
                        {
                            var month = p.cover ? "Cover" : new DateTime(project.Year, ((project.StartMonth - 1 + p.idx) % 12) + 1, 1).ToString("MMMM", CultureInfo.InvariantCulture);
                            progress.Report(new ExportProgress 
                            { 
                                CurrentPage = currentPage, 
                                TotalPages = totalPages, 
                                Status = $"Rendering {month}..."
                            });
                        }

                        var imgBytes = RenderPageToPng(project, p.idx, p.cover, pageWpt, pageHpt, TargetDpi, imageCache);
                        
                        container.Page(page =>
                        {
                            page.Size(new QuestPDF.Helpers.PageSize(pageWpt, pageHpt));
                            page.Margin(0);
                            page.DefaultTextStyle(x => x.FontSize(12));
                            page.Content().Image(imgBytes).FitArea();
                        });
                    }
                });

                // Final progress update - before PDF generation
                progress?.Report(new ExportProgress 
                { 
                    CurrentPage = totalPages, 
                    TotalPages = totalPages, 
                    Status = "Generating PDF file..."
                });

                using var stream = new MemoryStream();
                doc.GeneratePdf(stream);
                
                // Update after PDF generation complete
                progress?.Report(new ExportProgress 
                { 
                    CurrentPage = totalPages, 
                    TotalPages = totalPages, 
                    Status = "Export complete!"
                });
                
                return stream.ToArray();
            }
            finally
            {
                // Clean up cached bitmaps
                foreach (var bmp in imageCache.Values)
                {
                    bmp?.Dispose();
                }
                imageCache.Clear();
            }
        }, cancellationToken);
    }

    private byte[] RenderPageToPng(CalendarProject project, int monthIndex, bool renderCover, float pageWpt, float pageHpt, float targetDpi, Dictionary<string, SKBitmap>? imageCache = null)
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
            // Clip to keep image overflow hidden
            sk.Save(); sk.ClipRect(contentRect, antialias: true);
            var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto");
            if (asset != null && File.Exists(asset.Path))
            {
                var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                if (bmp != null)
                    DrawBitmapWithPanZoom(sk, bmp, contentRect, asset);
            }
            sk.Restore();
            DrawCoverText(sk, contentRect, project);
        }
        else
        {
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, project.LayoutSpec);
            var layout = project.MonthPhotoLayouts.TryGetValue(monthIndex, out var perMonth)
                ? perMonth
                : project.LayoutSpec.PhotoLayout;
            sk.Save();
            var slots = ComputePhotoSlots(photoRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .Where(a => a.Role == "monthPhoto" && a.MonthIndex == monthIndex && (a.SlotIndex ?? 0) == slotIndex)
                    .OrderBy(a => a.Order)
                    .FirstOrDefault();
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                    if (bmp != null)
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                }
                sk.Restore();
            }
            DrawCalendarGrid(sk, calRect, project, monthIndex);
        }

        sk.Flush();
        using var snapshot = skSurface.Snapshot();
        // Use JPEG with 85% quality - much faster encoding than PNG
        // and adequate for intermediate format before PDF embedding
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    private static SKBitmap? GetOrLoadBitmap(string path, Dictionary<string, SKBitmap>? cache)
    {
        if (cache == null)
        {
            // No caching, decode on demand
            return SKBitmap.Decode(path);
        }

        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var bitmap = SKBitmap.Decode(path);
        if (bitmap != null)
        {
            cache[path] = bitmap;
        }
        return bitmap;
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

    private static List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout)
    {
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

    private static void DrawBitmapWithPanZoom(SKCanvas canvas, SKBitmap bmp, SKRect rect, ImageAsset asset)
    {
        var imgW = (float)bmp.Width;
        var imgH = (float)bmp.Height;
        var rectW = rect.Width;
        var rectH = rect.Height;
        var imgAspect = imgW / imgH;
        var rectAspect = rectW / rectH;

        float baseScale = imgAspect > rectAspect ? rectH / imgH : rectW / imgW;
        float scale = baseScale * (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);
        var targetW = imgW * scale; var targetH = imgH * scale;
        var excessX = Math.Max(0, (targetW - rectW) / 2f);
        var excessY = Math.Max(0, (targetH - rectH) / 2f);
        var px = (float)Math.Clamp(asset.PanX, -1, 1);
        var py = (float)Math.Clamp(asset.PanY, -1, 1);

        var left = rect.Left - excessX + px * excessX;
        var top = rect.Top - excessY + py * excessY;
        var dest = new SKRect(left, top, left + targetW, top + targetH);

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static void DrawCoverText(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
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
