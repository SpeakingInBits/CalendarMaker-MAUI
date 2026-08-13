using SkiaSharp;
using CalendarMaker_MAUI.Models;
using System.Globalization;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of ICalendarRenderer that handles all calendar drawing operations.
/// Used by both the on-screen designer preview and the PDF exporter so both surfaces
/// produce identical output.
/// </summary>
public sealed class CalendarRenderer : ICalendarRenderer
{
    /// <summary>
    /// Minimum inset (in points) kept between the calendar grid and the page edge in borderless
    /// mode. Borderless printing on consumer printers (e.g. Epson) enlarges the page slightly and
    /// clips the overspray, so anything closer than ~1/8" to the trim edge risks being cut off.
    /// </summary>
    private const float MinBorderlessInsetPt = 9f;

    private const float HeaderHeight = 40f;
    private const float DayOfWeekHeight = 20f;

    private readonly ICalendarEngine _calendarEngine;
    private readonly IImageProcessor _imageProcessor;

    public CalendarRenderer(ICalendarEngine calendarEngine, IImageProcessor imageProcessor)
    {
        _calendarEngine = calendarEngine;
        _imageProcessor = imageProcessor;
    }

    /// <inheritdoc />
    public void RenderCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month, IDictionary<DateTime, SKRect>? dayCellBounds = null)
    {
        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + HeaderHeight);
        var gridRect = new SKRect(bounds.Left, headerRect.Bottom, bounds.Right, bounds.Bottom);

        // Render month/year title
        using var titlePaint = new SKPaint
        {
            Color = SKColor.Parse(project.Theme.PrimaryTextColor),
            TextSize = 18,
            IsAntialias = true
        };

        string title = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        float titleWidth = titlePaint.MeasureText(title);
        canvas.DrawText(title, gridRect.MidX - titleWidth / 2, headerRect.MidY + titlePaint.TextSize / 2.5f, titlePaint);

        RenderGridContent(canvas, gridRect, project, year, month, dayCellBounds);
    }

    /// <summary>
    /// Renders the calendar grid with an optional background color. In borderless mode the grid is
    /// always inset by the configured paddings (clamped to a printable-safe minimum) so it cannot
    /// sit on the trim edge where borderless overspray would clip it.
    /// </summary>
    public void RenderCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month, bool applyBackground, IDictionary<DateTime, SKRect>? dayCellBounds = null)
    {
        SKRect calendarRect = bounds;

        if (project.CoverSpec.BorderlessCalendar)
        {
            // Inset the grid regardless of whether a background color is drawn: in borderless mode
            // the bounds reach the physical page edge, and printed overspray would clip the grid.
            float topPadding = Math.Max((float)project.CoverSpec.CalendarTopPaddingPt, MinBorderlessInsetPt);
            float sidePadding = Math.Max((float)project.CoverSpec.CalendarSidePaddingPt, MinBorderlessInsetPt);
            float bottomPadding = Math.Max((float)project.CoverSpec.CalendarBottomPaddingPt, MinBorderlessInsetPt);

            calendarRect = new SKRect(
                bounds.Left + sidePadding,
                bounds.Top + topPadding,
                bounds.Right - sidePadding,
                bounds.Bottom - bottomPadding);

            if (applyBackground && !string.IsNullOrEmpty(project.Theme.BackgroundColor))
            {
                using var bgPaint = new SKPaint
                {
                    Color = SKColor.Parse(project.Theme.BackgroundColor),
                    Style = SKPaintStyle.Fill
                };

                // Fill the full bounds; the grid area below the header is cleared back to white
                // so the color only shows in the padding and behind the month title.
                canvas.DrawRect(bounds, bgPaint);

                RenderCalendarGridWithHeaderInPadding(canvas, calendarRect, project, year, month, dayCellBounds);
                return;
            }
        }
        else if (applyBackground && !string.IsNullOrEmpty(project.Theme.BackgroundColor))
        {
            // Non-borderless mode: fill entire area with background
            using var bgPaint = new SKPaint
            {
                Color = SKColor.Parse(project.Theme.BackgroundColor),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        RenderCalendarGrid(canvas, calendarRect, project, year, month, dayCellBounds);
    }

    /// <inheritdoc />
    public void RenderPhotoSlots(SKCanvas canvas, List<SKRect> photoSlots, List<ImageAsset> assets, string role, int? monthIndex = null, int activeSlotIndex = -1)
    {
        for (int slotIndex = 0; slotIndex < photoSlots.Count; slotIndex++)
        {
            var rect = photoSlots[slotIndex];

            // Find asset for this slot
            var asset = FindAssetForSlot(assets, role, slotIndex, monthIndex);

            if (asset != null && File.Exists(asset.Path))
            {
                // Cached load: the bitmap is owned by the processor's cache, so it must not be
                // disposed here. This avoids re-decoding full-resolution photos on every repaint.
                var bitmap = _imageProcessor.LoadBitmap(asset.Path, useCache: true);
                if (bitmap != null)
                {
                    canvas.Save();
                    canvas.ClipRect(rect, antialias: true);
                    RenderPhotoWithTransform(canvas, bitmap, rect, asset);
                    canvas.Restore();
                }
            }
            else
            {
                // Render empty slot
                bool isActive = slotIndex == activeSlotIndex;
                string? hintText = isActive ? "Double-click to assign photo" : null;
                RenderEmptySlot(canvas, rect, isActive, hintText);
            }

            // Highlight active slot
            if (slotIndex == activeSlotIndex)
            {
                RenderSlotHighlight(canvas, rect, SKColors.DeepSkyBlue);
            }
        }
    }

    /// <inheritdoc />
    public void RenderPhotoWithTransform(SKCanvas canvas, SKBitmap bitmap, SKRect rect, ImageAsset asset)
    {
        // Use ImageProcessor to calculate the transformed rectangle
        var destRect = _imageProcessor.CalculateTransformedRect(bitmap.Width, bitmap.Height, rect, asset);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.Medium
        };

        canvas.DrawBitmap(bitmap, destRect, paint);
    }

    /// <inheritdoc />
    public void RenderEmptySlot(SKCanvas canvas, SKRect rect, bool isActive, string? hintText = null)
    {
        using var fillPaint = new SKPaint { Color = new SKColor(0xEE, 0xEE, 0xEE) };
        using var borderPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };

        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, borderPaint);

        // Draw hint text if provided
        if (!string.IsNullOrEmpty(hintText))
        {
            using var textPaint = new SKPaint
            {
                Color = SKColors.Gray,
                TextSize = 12,
                IsAntialias = true
            };

            float textWidth = textPaint.MeasureText(hintText);
            canvas.DrawText(hintText, rect.MidX - textWidth / 2, rect.MidY, textPaint);
        }
    }

    /// <inheritdoc />
    public void RenderSlotHighlight(SKCanvas canvas, SKRect rect, SKColor color, float strokeWidth = 2f)
    {
        using var highlightPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth
        };

        canvas.DrawRect(rect, highlightPaint);
    }

    #region Private Helper Methods

    /// <summary>
    /// Renders everything below the month title: day-of-week header text, day numbers and events,
    /// and a single pass of grid lines so no line is drawn twice.
    /// </summary>
    private void RenderGridContent(SKCanvas canvas, SKRect gridRect, CalendarProject project, int year, int month, IDictionary<DateTime, SKRect>? dayCellBounds)
    {
        var weeks = _calendarEngine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

        var dowRect = new SKRect(gridRect.Left, gridRect.Top, gridRect.Right, gridRect.Top + DayOfWeekHeight);
        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, gridRect.Bottom);

        RenderDayOfWeekHeaderText(canvas, dowRect, project);
        RenderDayCells(canvas, weeksArea, weeks, month, project, dayCellBounds);
        RenderGridLines(canvas, dowRect, weeksArea, weeks.Count);
    }

    private void RenderDayOfWeekHeaderText(SKCanvas canvas, SKRect bounds, CalendarProject project)
    {
        string[] dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        int shift = (int)project.FirstDayOfWeek;
        string[] displayDays = Enumerable.Range(0, 7)
            .Select(i => dayNames[(i + shift) % 7])
            .ToArray();

        using var textPaint = new SKPaint
        {
            Color = SKColor.Parse(project.Theme.PrimaryTextColor),
            TextSize = 10,
            IsAntialias = true
        };

        float columnWidth = bounds.Width / 7f;

        for (int col = 0; col < 7; col++)
        {
            float cellMidX = bounds.Left + (col + 0.5f) * columnWidth;
            string text = displayDays[col];
            float textWidth = textPaint.MeasureText(text);
            canvas.DrawText(text, cellMidX - textWidth / 2, bounds.MidY + textPaint.TextSize / 2.5f, textPaint);
        }
    }

    private void RenderDayCells(SKCanvas canvas, SKRect bounds, List<List<DateTime?>> weeks, int month, CalendarProject project, IDictionary<DateTime, SKRect>? dayCellBounds)
    {
        if (weeks.Count == 0)
        {
            return;
        }

        using var textPaint = new SKPaint
        {
            Color = SKColor.Parse(project.Theme.PrimaryTextColor),
            TextSize = 10,
            IsAntialias = true
        };

        float columnWidth = bounds.Width / 7f;
        float rowHeight = bounds.Height / weeks.Count;

        for (int row = 0; row < weeks.Count; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                var date = weeks[row][col];
                if (date.HasValue && date.Value.Month == month)
                {
                    var cellRect = new SKRect(
                        bounds.Left + col * columnWidth,
                        bounds.Top + row * rowHeight,
                        bounds.Left + (col + 1) * columnWidth,
                        bounds.Top + (row + 1) * rowHeight);

                    string dayText = date.Value.Day.ToString(CultureInfo.InvariantCulture);
                    canvas.DrawText(dayText, cellRect.Left + 2, cellRect.Top + textPaint.TextSize + 2, textPaint);

                    CalendarEventDrawing.DrawDayEvents(canvas, cellRect, project, date.Value);
                    if (dayCellBounds != null)
                    {
                        dayCellBounds[date.Value.Date] = cellRect;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws all grid lines (day-of-week header and day cells) exactly once each, antialiased.
    /// Antialiasing matters: the preview canvas scales page points by an arbitrary factor, and
    /// non-antialiased hairlines snap to device pixels unevenly (lines vanish or double up).
    /// </summary>
    private void RenderGridLines(SKCanvas canvas, SKRect dowRect, SKRect weeksArea, int weekCount)
    {
        using var gridPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            IsAntialias = true
        };

        float columnWidth = dowRect.Width / 7f;

        // Vertical lines span the day-of-week header and the day grid.
        for (int col = 0; col <= 7; col++)
        {
            float x = dowRect.Left + col * columnWidth;
            canvas.DrawLine(x, dowRect.Top, x, weeksArea.Bottom, gridPaint);
        }

        // Horizontal lines: header top, header bottom (= day grid top), then each week row boundary
        // including the outer bottom edge.
        canvas.DrawLine(dowRect.Left, dowRect.Top, dowRect.Right, dowRect.Top, gridPaint);
        canvas.DrawLine(dowRect.Left, dowRect.Bottom, dowRect.Right, dowRect.Bottom, gridPaint);

        if (weekCount > 0)
        {
            float rowHeight = weeksArea.Height / weekCount;
            for (int row = 1; row <= weekCount; row++)
            {
                float y = weeksArea.Top + row * rowHeight;
                canvas.DrawLine(weeksArea.Left, y, weeksArea.Right, y, gridPaint);
            }
        }
    }

    private ImageAsset? FindAssetForSlot(List<ImageAsset> assets, string role, int slotIndex, int? monthIndex)
    {
        if (role == "monthPhoto" && monthIndex.HasValue)
        {
            return assets
                .Where(a => a.Role == role && a.MonthIndex == monthIndex && (a.SlotIndex ?? 0) == slotIndex)
                .OrderBy(a => a.Order)
                .FirstOrDefault();
        }
        else
        {
            return assets
                .FirstOrDefault(a => a.Role == role && (a.SlotIndex ?? 0) == slotIndex);
        }
    }

    /// <summary>
    /// Renders the calendar grid so the month title sits in the colored padding area surrounding
    /// the grid, with the grid area itself cleared back to white.
    /// </summary>
    private void RenderCalendarGridWithHeaderInPadding(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month, IDictionary<DateTime, SKRect>? dayCellBounds = null)
    {
        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + HeaderHeight);
        var gridRect = new SKRect(bounds.Left, headerRect.Bottom, bounds.Right, bounds.Bottom);

        // Render month/year title in a color that contrasts with the background it sits on
        SKColor titleColor = GetContrastingTextColor(project.Theme.BackgroundColor);
        using var titlePaint = new SKPaint
        {
            Color = titleColor,
            TextSize = 18,
            IsAntialias = true
        };

        string title = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        float titleWidth = titlePaint.MeasureText(title);
        canvas.DrawText(title, gridRect.MidX - titleWidth / 2, headerRect.MidY + titlePaint.TextSize / 2.5f, titlePaint);

        // Clear the grid area (below the header) back to white so the background color only
        // remains in the padding and behind the title.
        using var clearPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(gridRect, clearPaint);

        RenderGridContent(canvas, gridRect, project, year, month, dayCellBounds);
    }

    private SKColor GetContrastingTextColor(string? backgroundColor)
    {
        if (string.IsNullOrEmpty(backgroundColor))
        {
            return SKColors.Black;
        }

        try
        {
            SKColor bgColor = SKColor.Parse(backgroundColor);

            // Calculate relative luminance
            float luminance = (0.299f * bgColor.Red + 0.587f * bgColor.Green + 0.114f * bgColor.Blue) / 255f;

            // Use white text for dark backgrounds, black for light backgrounds
            return luminance > 0.5f ? SKColors.Black : SKColors.White;
        }
        catch
        {
            return SKColors.Black;
        }
    }

    #endregion
}
