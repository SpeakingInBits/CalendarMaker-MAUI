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
        
        if (includePreviousDecember)
        {
            // Include previous year's December as the starting point
      // This will be the photo for Page 7 front (Dec prev year photo + Dec current year month)
            // We'll mark it with UsePreviousYearPhoto = true
     }

    // Page 1 Front: June (0) photo + June (0) calendar
        pages.Add(new DoubleSidedPageSpec(0, 0, false, false, false));
        // Page 1 Back: May (11) photo + July (1) calendar (upside down)
        pages.Add(new DoubleSidedPageSpec(11, 1, false, false, true));

        // Page 2 Front: May (11) photo + July (1) calendar
        pages.Add(new DoubleSidedPageSpec(11, 1, false, false, false));
        // Page 2 Back: April (10) photo + August (2) calendar (upside down)
        pages.Add(new DoubleSidedPageSpec(10, 2, false, false, true));

        // Page 3 Front: April (10) photo + August (2) calendar
     pages.Add(new DoubleSidedPageSpec(10, 2, false, false, false));
        // Page 3 Back: March (9) photo + September (3) calendar (upside down)
   pages.Add(new DoubleSidedPageSpec(9, 3, false, false, true));

 // Page 4 Front: March (9) photo + September (3) calendar
     pages.Add(new DoubleSidedPageSpec(9, 3, false, false, false));
        // Page 4 Back: Feb (8) photo + October (4) calendar (upside down)
        pages.Add(new DoubleSidedPageSpec(8, 4, false, false, true));

    // Page 5 Front: Feb (8) photo + October (4) calendar
        pages.Add(new DoubleSidedPageSpec(8, 4, false, false, false));
   // Page 5 Back: January (7) photo + November (5) calendar (upside down)
        pages.Add(new DoubleSidedPageSpec(7, 5, false, false, true));

 // Page 6 Front: January (7) photo + November (5) calendar
pages.Add(new DoubleSidedPageSpec(7, 5, false, false, false));
      // Page 6 Back: Dec photo + Dec current (6) calendar (upside down)
        // If EnableDoubleSided is true, use previous year's December photo
        pages.Add(new DoubleSidedPageSpec(6, 6, includePreviousDecember, false, true));

        // Page 7 Front: Dec photo + Dec current (6) calendar
     // If EnableDoubleSided is true, use previous year's December photo
        pages.Add(new DoubleSidedPageSpec(6, 6, includePreviousDecember, false, false));
     // Page 7 Back: Covers page (front upside down on top, back normal on bottom)
        pages.Add(new DoubleSidedPageSpec(0, 0, false, true, true));

        return RenderDoubleSidedDocumentAsync(project, pages, progress, cancellationToken);
    }

    private record DoubleSidedPageSpec(int PhotoMonthIndex, int CalendarMonthIndex, bool UsePreviousYearPhoto, bool IsCoversPage, bool Rotated);

    private Task<byte[]> RenderDoubleSidedDocumentAsync(CalendarProject project, List<DoubleSidedPageSpec> pages, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var (wPtD, hPtD) = CalendarMaker_MAUI.Models.PageSizes.GetPoints(project.PageSpec);
            float pageWpt = (float)wPtD;
            float pageHpt = (float)hPtD;
            if (pageWpt <= 0 || pageHpt <= 0) { pageWpt = 612; pageHpt = 792; }

            var totalPages = pages.Count;
            var imageCache = new System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>();
            int completedPages = 0;
            var progressLock = new object();

            try
            {
                var renderedPages = new byte[totalPages][];
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
                        var pageName = pageSpec.IsCoversPage ? "Covers Page" :
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
                        var imgBytes = renderedPages[i];
                        container.Page(page =>
                        {
                            page.Size(new QuestPDF.Helpers.PageSize(pageWpt, pageHpt));
                            page.Margin(0);
                            page.DefaultTextStyle(x => x.FontSize(12));
                            page.Content().Image(imgBytes).FitArea();
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

        var m = project.Margins;
        var contentRect = new SKRect((float)m.LeftPt, (float)m.TopPt, pageWpt - (float)m.RightPt, pageHpt - (float)m.BottomPt);

        if (pageSpec.IsCoversPage)
        {
            // Page 7 back: Front cover (upside down) on top half, Back cover (normal) on bottom half
            var topHalf = new SKRect(contentRect.Left, contentRect.Top, contentRect.Right, contentRect.MidY - 2f);
            var bottomHalf = new SKRect(contentRect.Left, contentRect.MidY + 2f, contentRect.Right, contentRect.Bottom);

            // Draw front cover upside down in top half
            sk.Save();
            sk.Translate(topHalf.MidX, topHalf.MidY);
            sk.RotateDegrees(180);
            sk.Translate(-topHalf.MidX, -topHalf.MidY);

            var frontLayout = project.FrontCoverPhotoLayout;
            var frontSlots = ComputePhotoSlots(topHalf, frontLayout);
            foreach (var (rect, slotIndex) in frontSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                    if (bmp != null)
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                }
                sk.Restore();
            }
            sk.Restore();

            // Draw back cover normal in bottom half
            var backLayout = project.BackCoverPhotoLayout;
            var backSlots = ComputePhotoSlots(bottomHalf, backLayout);
            foreach (var (rect, slotIndex) in backSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets.FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                    if (bmp != null)
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                }
                sk.Restore();
            }
        }
        else
        {
            // Regular page: photo on one side, calendar on the other
            (SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, project.LayoutSpec);

            // Get photo layout for the photo month
            var photoLayout = project.MonthPhotoLayouts.TryGetValue(pageSpec.PhotoMonthIndex, out var perMonth)
                ? perMonth
                : project.LayoutSpec.PhotoLayout;

            // Draw photos
            var photoSlots = ComputePhotoSlots(photoRect, photoLayout);
            foreach (var (rect, slotIndex) in photoSlots.Select((r, i) => (r, i)))
            {
                sk.Save();
                sk.ClipRect(rect, antialias: true);

                var photoMonthIndex = pageSpec.PhotoMonthIndex;
  
  // When UsePreviousYearPhoto is true and we're looking for December (month 6 in 0-indexed from start),
           // we need to find photos assigned to the "previous December" page (index -2 in designer)
         // For the double-sided export, these photos are stored with a special indicator
  // In the designer, page -2 represents previous December, which maps to month index 6 
       // but we need to identify them as "previous year" photos
  
          ImageAsset? asset = null;
   if (pageSpec.UsePreviousYearPhoto && pageSpec.PhotoMonthIndex == 6)
     {
          // Look for photos assigned to previous year's December
            // These would be stored with MonthIndex = 6 but marked specially
      // For now, we'll use the same month 6 photos - the user assigns them via page -2 in the designer
        asset = project.ImageAssets
        .Where(a => a.Role == "monthPhoto" && a.MonthIndex == photoMonthIndex && (a.SlotIndex ?? 0) == slotIndex)
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
    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
        if (bmp != null)
   DrawBitmapWithPanZoom(sk, bmp, rect, asset);
      }
        sk.Restore();
 }

     // Draw calendar for the calendar month
       DrawCalendarGrid(sk, calRect, project, pageSpec.CalendarMonthIndex);
     }

        sk.Flush();
        using var snapshot = skSurface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    public Task<byte[]> ExportYearAsync(CalendarProject project, bool includeCover = true, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var pages = new List<(int idx, bool cover, bool backCover)>();
        if (includeCover) pages.Add((0, true, false));
        for (int i = 0; i < 12; i++) pages.Add((i, false, false));
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
            var totalPages = pagesList.Count;

            // Image cache to avoid re-decoding same images - use concurrent dictionary for thread safety
            var imageCache = new System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>();
            
            // Thread-safe counter for progress reporting
            int completedPages = 0;
            var progressLock = new object();
            
            try
            {
                // Parallel rendering of all pages
                var renderedPages = new byte[totalPages][];
                
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
                        var pageName = p.cover ? "Front Cover" : 
                                      p.backCover ? "Back Cover" : 
                                      new DateTime(project.Year, ((project.StartMonth - 1 + p.idx) % 12) + 1, 1).ToString("MMMM", CultureInfo.InvariantCulture);
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
                        var imgBytes = renderedPages[i];
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
        
        if (renderCover && project.CoverSpec.BorderlessFrontCover)
        {
            // Front cover with borderless - use full page
            contentRect = new SKRect(0, 0, pageWpt, pageHpt);
        }
        else if (renderBackCover && project.CoverSpec.BorderlessBackCover)
        {
            // Back cover with borderless - use full page
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
            var slots = ComputePhotoSlots(contentRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .FirstOrDefault(a => a.Role == "coverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                    if (bmp != null)
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                }
                sk.Restore();
            }
        }
        else if (renderBackCover)
        {
            // Back cover - support multiple photo slots
            var layout = project.BackCoverPhotoLayout;
            var slots = ComputePhotoSlots(contentRect, layout);
            foreach (var (rect, slotIndex) in slots.Select((r, i) => (r, i)))
            {
                sk.Save(); sk.ClipRect(rect, antialias: true);
                var asset = project.ImageAssets
                    .FirstOrDefault(a => a.Role == "backCoverPhoto" && (a.SlotIndex ?? 0) == slotIndex);
                if (asset != null && File.Exists(asset.Path))
                {
                    var bmp = GetOrLoadBitmap(asset.Path, imageCache);
                    if (bmp != null)
                        DrawBitmapWithPanZoom(sk, bmp, rect, asset);
                }
                sk.Restore();
            }
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

    private static SKBitmap? GetOrLoadBitmap(string path, System.Collections.Concurrent.ConcurrentDictionary<string, SKBitmap>? cache)
    {
        if (cache == null)
        {
            // No caching, decode on demand
            return SKBitmap.Decode(path);
        }

        // Thread-safe get-or-add
        return cache.GetOrAdd(path, p => SKBitmap.Decode(p));
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
