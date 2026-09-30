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
    Task<byte[]> ExportBackCoverAsync(CalendarProject project);
    Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default);

    Task<byte[]> ExportDoubleSidedAsync(CalendarProject project, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PdfExportService : IPdfExportService
{
    private const float TargetDpi = 300f; // 300 DPI print quality
    private readonly ILayoutCalculator _layoutCalculator;
    private readonly IImageProcessor _imageProcessor;
    private readonly ICalendarRenderer _calendarRenderer;

    public PdfExportService(ILayoutCalculator layoutCalculator, IImageProcessor imageProcessor, ICalendarRenderer calendarRenderer)
    {
        _layoutCalculator = layoutCalculator;
        _imageProcessor = imageProcessor;
        _calendarRenderer = calendarRenderer;
    }

    public Task<byte[]> ExportMonthAsync(CalendarProject project, int monthIndex)
        => RenderDocumentAsync(project, new[] { (monthIndex, false, false) }, null, default);

    public Task<byte[]> ExportCoverAsync(CalendarProject project)
        => RenderDocumentAsync(project, new[] { (0, true, false) }, null, default);

    public Task<byte[]> ExportBackCoverAsync(CalendarProject project)
        => RenderDocumentAsync(project, new[] { (0, false, true) }, null, default);

    // Double-sided calendar export - creates 7 pages for folding
    public Task<byte[]> ExportDoubleSidedAsync(CalendarProject project, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var pages = new List<DoubleSidedPageSpec>();

        // Check if we need to include previous December
        bool includePreviousDecember = project.EnableDoubleSided;

        // Page ordering for double-sided calendar (14 pages total, 7 physical sheets)
        // Even pages are rotated 180 degrees for proper double-sided printing
        // Month indices are 0-based relative to StartMonth
        // For a January start: Month 0=Jan, Month 5=June, Month 11=December
        // Param order: PhotoMonthIndex, CalendarMonthIndex, UsePreviousYear, IsCovers, Rotated, SwapPhotoAndCalendar

        // Page 1: Month 5 photo (June) with Month 5 calendar (June)
        pages.Add(new DoubleSidedPageSpec(5, 5, false, false, false, false));

        // Page 2: Month 6 photo (July) with Month 4 calendar (May) (rotated 180°)
        pages.Add(new DoubleSidedPageSpec(6, 4, false, false, true, false));

        // Page 3: Month 4 photo (May) with Month 6 calendar (July)
        pages.Add(new DoubleSidedPageSpec(4, 6, false, false, false, false));

        // Page 4: Month 7 photo (August) with Month 3 calendar (April) (rotated 180°)
        pages.Add(new DoubleSidedPageSpec(7, 3, false, false, true, false));

        // Page 5: Month 3 photo (April) with Month 7 calendar (August)
        pages.Add(new DoubleSidedPageSpec(3, 7, false, false, false, false));

        // Page 6: Month 8 photo (September) with Month 2 calendar (March) (rotated 180°)
        pages.Add(new DoubleSidedPageSpec(8, 2, false, false, true, false));

        // Page 7: Month 2 photo (March) with Month 8 calendar (September)
        pages.Add(new DoubleSidedPageSpec(2, 8, false, false, false, false));

        // Page 8: Month 9 photo (October) with Month 1 calendar (February) (rotated 180°)
        pages.Add(new DoubleSidedPageSpec(9, 1, false, false, true, false));

        // Page 9: Month 1 photo (February) with Month 9 calendar (October)
        pages.Add(new DoubleSidedPageSpec(1, 9, false, false, false, false));

        // Page 10: Month 10 photo (November) with Month 0 calendar (January) (rotated 180°)
        pages.Add(new DoubleSidedPageSpec(10, 0, false, false, true, false));

        // Page 11: Month 0 photo (January) with Month 10 calendar (November)
        pages.Add(new DoubleSidedPageSpec(0, 10, false, false, false, false));

        // Page 12: Month 11 photo (December current year) with Month -1 calendar (Previous December) (rotated 180°)
        // When includePreviousDecember is true, calendar shows previous year's December
        pages.Add(new DoubleSidedPageSpec(11, -1, includePreviousDecember, false, true, false));

        // Page 13: Month -1 photo (Previous December) with Month 11 calendar (December of current year)
        // When includePreviousDecember is true, photo shows previous year's December (stored as MonthIndex=-2)
        pages.Add(new DoubleSidedPageSpec(-1, 11, includePreviousDecember, false, false, false));

        // Page 14: Front cover and rear cover (split page, rotated 180°)
        pages.Add(new DoubleSidedPageSpec(0, 0, false, true, true, false));

        return RenderDoubleSidedDocumentAsync(project, pages, progress, cancellationToken);
    }

    private record DoubleSidedPageSpec(int PhotoMonthIndex, int CalendarMonthIndex, bool UsePreviousYearPhoto, bool IsCoversPage, bool Rotated, bool SwapPhotoAndCalendar);

    private Task<byte[]> RenderDoubleSidedDocumentAsync(CalendarProject project, List<DoubleSidedPageSpec> pages, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var (wPtD, hPtD) = CalendarMaker_MAUI.Models.PageSizes.GetPoints(project.PageSpec);
            float pageWpt = (float)wPtD;
            float pageHpt = (float)hPtD;
            if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }

            int totalPages = pages.Count;
            var imageCache = new System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>();
            int completedPages = 0;
            object progressLock = new object();

            try
            {
                byte[][] renderedPages = new byte[totalPages][];
                int maxParallelism = GetOptimalParallelism();

                Parallel.For(0, totalPages, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = maxParallelism
                },
                pageIndex =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pageSpec = pages[pageIndex];
                    renderedPages[pageIndex] = RenderDoubleSidedPageToJpeg(project, pageSpec, pageWpt, pageHpt, TargetDpi, imageCache);

                    int currentCompleted;
                    lock (progressLock)
                    {
                        completedPages++;
                        currentCompleted = completedPages;
                    }

                    if (progress != null)
                    {
                        string pageName = pageSpec.IsCoversPage ? "Covers Page" :
                            $"Page {(pageIndex / 2) + 1} - {(pageSpec.Rotated ? "Back" : "Front")}";
                        progress.Report(new ExportProgress
                        {
                            CurrentPage = currentCompleted,
                            TotalPages = totalPages,
                            Status = $"Rendering {pageName}... ({currentCompleted}/{totalPages})"
                        });
                    }
                });

                progress?.Report(new ExportProgress
                {
                    CurrentPage = totalPages,
                    TotalPages = totalPages,
                    Status = "All pages rendered, creating PDF..."
                });

                if (progress != null)
                {
                    await Task.Delay(100, cancellationToken);
                }

                var doc = Document.Create(container =>
                {
                    for (int i = 0; i < totalPages; i++)
                    {
                        byte[] imgBytes = renderedPages[i];
                        container.Page(page =>
                        {
                            page.Size(new QuestPDF.Helpers.PageSize(pageWpt, pageHpt));
                            page.Margin(0);
                            page.DefaultTextStyle(x => x.FontSize(12));
                            // FitUnproportionally: the rasterized page bitmap can differ from the
                            // PDF page aspect by a sub-pixel rounding amount; aspect-preserving
                            // fitting would letterbox that difference into a hairline white edge
                            // on borderless pages. Stretching by <1px is invisible.
                            page.Content().Image(imgBytes).FitUnproportionally();
                        });
                    }
                });

                progress?.Report(new ExportProgress
                {
                    CurrentPage = totalPages,
                    TotalPages = totalPages,
                    Status = "Generating PDF file..."
                });

                using var stream = new MemoryStream();
                doc.GeneratePdf(stream);

                if (progress != null)
                {
                    await Task.Delay(50, cancellationToken);
                }

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
                foreach (var bmp in imageCache.Values)
                {
                    bmp?.Dispose();
                }
                imageCache.Clear();
            }
        }, cancellationToken);
    }

    private byte[] RenderDoubleSidedPageToJpeg(CalendarProject project, DoubleSidedPageSpec pageSpec, float pageWpt, float pageHpt, float targetDpi, System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>? imageCache = null)
    {
        float scale = targetDpi / 72f;
        int widthPx = Math.Max(1, (int)Math.Round(pageWpt * scale));
        int heightPx = Math.Max(1, (int)Math.Round(pageHpt * scale));

        using var skSurface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul));
        var sk = skSurface.Canvas;
        sk.Clear(SKColors.White);
        sk.Scale(scale);

        // Rotate 180 degrees for upside-down pages
        if (pageSpec.Rotated)
        {
            sk.Translate(pageWpt, pageHpt);
            sk.RotateDegrees(180);
        }

        // Match the single-sided path: borderless pages use the full page, otherwise apply margins.
        var m = project.Margins;
        SKRect contentRect = project.CoverSpec.BorderlessCalendar
            ? new SKRect(0, 0, pageWpt, pageHpt)
            : new SKRect((float)m.LeftPt, (float)m.TopPt, pageWpt - (float)m.RightPt, pageHpt - (float)m.BottomPt);

        if (pageSpec.IsCoversPage)
        {
            // Page 14: Front and back covers using TwoHorizontalStack layout
            // Top half: Back cover (rotated 180° because the whole page is already rotated)
            // Bottom half: Front cover (normal, but appears upside down because whole page is rotated)

            // Split the content into two horizontal halves
            var topHalf = new SKRect(contentRect.Left, contentRect.Top, contentRect.Right, contentRect.MidY - 2f);
            var bottomHalf = new SKRect(contentRect.Left, contentRect.MidY + 2f, contentRect.Right, contentRect.Bottom);

            // Draw back cover in top half (rotated 180° within the already-rotated page)
            sk.Save();
            sk.Translate(topHalf.MidX, topHalf.MidY);
            sk.RotateDegrees(180);
            sk.Translate(-topHalf.MidX, -topHalf.MidY);

            var backLayout = project.BackCoverPhotoLayout;
            var backSlots = _layoutCalculator.ComputePhotoSlots(topHalf, backLayout);
            foreach (var (rect, slotIndex) in backSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }
            sk.Restore();

            // Draw front cover in bottom half (normal orientation within the already-rotated page)
            var frontLayout = project.FrontCoverPhotoLayout;
            var frontSlots = _layoutCalculator.ComputePhotoSlots(bottomHalf, frontLayout);
            foreach (var (rect, slotIndex) in frontSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }
        }
        else
        {
            // Regular page: photo on one side, calendar on the other
            (SKRect photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, project.LayoutSpec, project.PageSpec);

            // Get photo layout for the photo month
            var photoLayout = project.MonthPhotoLayouts.TryGetValue(pageSpec.PhotoMonthIndex, out var perMonth)
                                ? perMonth
                                : project.LayoutSpec.PhotoLayout;

            // Draw photos
            var photoSlots = _layoutCalculator.ComputePhotoSlots(photoRect, photoLayout);
            foreach (var (rect, slotIndex) in photoSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);

                int photoMonthIndex = pageSpec.PhotoMonthIndex;

                // Handle previous December (month index -1)
                // When UsePreviousYearPhoto is true, we look for photos assigned to page -2 in designer
                // These are stored with MonthIndex = -2

                ImageAsset? asset = null;
                if (pageSpec.UsePreviousYearPhoto && pageSpec.PhotoMonthIndex == -1)
                {
                    // Look for photos assigned to previous year's December
                    // These are stored with MonthIndex = -2 in the designer
                    asset = project.ImageAssets
                            .Where(a => a.Role == "monthPhoto" && a.MonthIndex == -2 && (a.SlotIndex ?? 0) == slotIndex)
                            .OrderBy(a => a.Order)
                            .FirstOrDefault();
                }
                else
                {
                    asset = project.ImageAssets
                      .Where(a => a.Role == "monthPhoto" && a.MonthIndex == photoMonthIndex && (a.SlotIndex ?? 0) == slotIndex)
                     .OrderBy(a => a.Order)
                              .FirstOrDefault();
                }

                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }

            // Draw calendar for the calendar month
            bool applyCalendarBackground = IsMonthPageBorderless(project) &&
              project.CoverSpec.UseCalendarBackgroundOnBorderless;
            var (calYear, calMonth) = GetCalendarYearMonth(project, pageSpec.CalendarMonthIndex);
            _calendarRenderer.RenderCalendarGrid(sk, calRect, project, calYear, calMonth, applyCalendarBackground);
        }

        sk.Flush();
        using var snapshot = skSurface.Snapshot();
        // Quality 95: thin grid lines and small text show visible JPEG ringing at 85.
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    public Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var pages = new List<(int idx, bool cover, bool backCover)>();
        if (includeCover)
        {
            pages.Add((0, true, false));
        }

        for (int i = 0; i < project.MonthCount; i++)
        {
            pages.Add((i, false, false));
        }

        pages.Add((0, false, true)); // back cover
        return RenderDocumentAsync(project, pages, progress, cancellationToken);
    }

    private Task<byte[]> RenderDocumentAsync(CalendarProject project, IEnumerable<(int idx, bool cover, bool backCover)> pages, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var (wPtD, hPtD) = CalendarMaker_MAUI.Models.PageSizes.GetPoints(project.PageSpec);
            float pageWpt = (float)wPtD;
            float pageHpt = (float)hPtD;
            if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }

            var pagesList = pages.ToList();
            int totalPages = pagesList.Count;

            // Image cache to avoid re-decoding same images - use concurrent dictionary for thread safety
            var imageCache = new System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>();

            // Thread-safe counter for progress reporting
            int completedPages = 0;
            object progressLock = new object();

            try
            {
                // Parallel rendering of all pages
                byte[][] renderedPages = new byte[totalPages][];

                // Optimize parallel degree for mobile devices
                int maxParallelism = GetOptimalParallelism();

                Parallel.For(0, totalPages, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = maxParallelism
                },
                pageIndex =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var p = pagesList[pageIndex];

                    // Render page to JPEG bytes
                    renderedPages[pageIndex] = RenderPageToJpeg(project, p.idx, p.cover, p.backCover, pageWpt, pageHpt, TargetDpi, imageCache);

                    // Update progress in thread-safe manner
                    int currentCompleted;
                    lock (progressLock)
                    {
                        completedPages++;
                        currentCompleted = completedPages;
                    }

                    // Report progress
                    if (progress != null)
                    {
                        string pageName = p.cover ? "Front Cover" :
                                      p.backCover ? "Back Cover" :
                                      project.GetMonthDate(p.idx).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
                        progress.Report(new ExportProgress
                        {
                            CurrentPage = currentCompleted,
                            TotalPages = totalPages,
                            Status = $"Rendering {pageName}... ({currentCompleted}/{totalPages})"
                        });
                    }
                });

                // All pages rendered - ensure final rendering progress is shown
                progress?.Report(new ExportProgress
                {
                    CurrentPage = totalPages,
                    TotalPages = totalPages,
                    Status = "All pages rendered, creating PDF..."
                });

                // Small delay to ensure UI sees the status update
                if (progress != null)
                {
                    await Task.Delay(100, cancellationToken);
                }

                // All pages rendered, now create PDF document
                var doc = Document.Create(container =>
                {
                    for (int i = 0; i < totalPages; i++)
                    {
                        byte[] imgBytes = renderedPages[i];
                        container.Page(page =>
                        {
                            page.Size(new QuestPDF.Helpers.PageSize(pageWpt, pageHpt));
                            page.Margin(0);
                            page.DefaultTextStyle(x => x.FontSize(12));
                            // FitUnproportionally: the rasterized page bitmap can differ from the
                            // PDF page aspect by a sub-pixel rounding amount; aspect-preserving
                            // fitting would letterbox that difference into a hairline white edge
                            // on borderless pages. Stretching by <1px is invisible.
                            page.Content().Image(imgBytes).FitUnproportionally();
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

                // Small delay to ensure the "Generating PDF file..." message is visible
                if (progress != null)
                {
                    await Task.Delay(50, cancellationToken);
                }

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

    private byte[] RenderPageToJpeg(CalendarProject project, int monthIndex, bool renderCover, bool renderBackCover, float pageWpt, float pageHpt, float targetDpi, System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>? imageCache = null)
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

        // Use full page for borderless covers, otherwise use margins
        var m = project.Margins;
      SKRect contentRect;

        if (project.CoverSpec.BorderlessCalendar)
 {
      // Borderless mode - use full page
    contentRect = new SKRect(0, 0, pageWpt, pageHpt);
}
    else
        {
      // Normal margins
 contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, pageWpt - (float)m.RightPt, pageHpt - (float)m.BottomPt);
   }

        if (renderCover)
        {
            // Front cover - support multiple photo slots
            var layout = project.FrontCoverPhotoLayout;
            var slots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }
        }
        else if (renderBackCover)
        {
            // Back cover - support multiple photo slots
            var layout = project.BackCoverPhotoLayout;
            var slots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }
        }
        else
        {
            (SKRect photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, project.LayoutSpec, project.PageSpec);
            var layout = project.MonthPhotoLayouts.TryGetValue(monthIndex, out var perMonth)
                ? perMonth
                : project.LayoutSpec.PhotoLayout;
            sk.Save();
            var slots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .Where(a => a.Role == "monthPhoto" && a.MonthIndex == monthIndex && (a.SlotIndex ?? 0) == slotIndex)
                    .OrderBy(a => a.Order)
                    .FirstOrDefault();
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = _imageProcessor.GetOrLoadCached(asset.Path);
                    if (bmp != null)
                    {
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                    }
                }
                sk.Restore();
            }
            
            // Apply background to calendar area if this is a borderless month page
            bool applyCalendarBackground = IsMonthPageBorderless(project) &&
                project.CoverSpec.UseCalendarBackgroundOnBorderless;
            var (calYear, calMonth) = GetCalendarYearMonth(project, monthIndex);
            _calendarRenderer.RenderCalendarGrid(sk, calRect, project, calYear, calMonth, applyCalendarBackground);
        }

        sk.Flush();
        using var snapshot = skSurface.Snapshot();
        // Quality 95: thin grid lines and small text show visible JPEG ringing at 85.
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    private static int GetOptimalParallelism()
    {
        // Detect platform and optimize accordingly
        int cores = Environment.ProcessorCount;

#if ANDROID || IOS
        // On mobile devices, use fewer threads to prevent overheating and battery drain
        // Also helps with memory pressure on constrained devices
        return Math.Min(cores, 4); // Cap at 4 threads on mobile
#else
        // On desktop, use all available cores
        return cores;
#endif
    }

    private void DrawBitmapWithPanZoom(SKCanvas canvas, SKBitmap bmp, SKRect rect, ImageAsset asset)
    {
        // Shared pan/zoom math so the export matches the designer preview exactly.
        var dest = _imageProcessor.CalculateTransformedRect(bmp.Width, bmp.Height, rect, asset);

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    /// <summary>
    /// Maps a 0-based month page index (relative to StartMonth; -1 = previous year's December)
    /// to the actual calendar year and month.
    /// </summary>
    private static (int year, int month) GetCalendarYearMonth(CalendarProject project, int monthIndex)
    {
        if (monthIndex == -1)
        {
            return (project.Year - 1, 12);
        }

        DateTime date = project.GetMonthDate(monthIndex);
        return (date.Year, date.Month);
    }

    private static bool IsMonthPageBorderless(CalendarProject project)
    {
        // Check if borderless calendar mode is enabled
        return project.CoverSpec.BorderlessCalendar;
    }
}
