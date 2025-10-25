using SkiaSharp;
using CalendarMaker_MAUI.Models;
using System.Globalization;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of ICalendarRenderer that handles all calendar drawing operations.
/// Extracts rendering logic previously duplicated in DesignerPage and PdfExportService.
/// </summary>
public sealed class CalendarRenderer : ICalendarRenderer
{
    private readonly ICalendarEngine _calendarEngine;

    public CalendarRenderer(ICalendarEngine calendarEngine)
    {
        _calendarEngine = calendarEngine;
    }

    /// <inheritdoc />
    public void RenderCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month)
    {
        var weeks = _calendarEngine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

        const float headerHeight = 40f;
        const float dayOfWeekHeight = 20f;

        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + headerHeight);
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

        // Render day-of-week headers
        var dowRect = new SKRect(gridRect.Left, gridRect.Top, gridRect.Right, gridRect.Top + dayOfWeekHeight);
        RenderDayOfWeekHeaders(canvas, dowRect, project);

        // Render day grid
        var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, bounds.Bottom);
        RenderDayGrid(canvas, weeksArea, weeks, month, project);
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
                using var bitmap = SKBitmap.Decode(asset.Path);
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
        float imgW = bitmap.Width;
        float imgH = bitmap.Height;
        float rectW = rect.Width;
        float rectH = rect.Height;
        float imgAspect = imgW / imgH;
        float rectAspect = rectW / rectH;

        // Calculate base scale to cover the rectangle
        float baseScale = imgAspect > rectAspect ? rectH / imgH : rectW / imgW;

        // Apply zoom (clamped between 0.5x and 3x)
        float zoom = (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);
        float scale = baseScale * zoom;

        float targetW = imgW * scale;
        float targetH = imgH * scale;

        // Calculate pan limits (how much excess space we have)
        float excessX = Math.Max(0, (targetW - rectW) / 2f);
        float excessY = Math.Max(0, (targetH - rectH) / 2f);

        // Apply pan (clamped between -1 and 1)
        float px = (float)Math.Clamp(asset.PanX, -1, 1);
        float py = (float)Math.Clamp(asset.PanY, -1, 1);

        // Calculate final position
        float left = rect.Left - excessX + px * excessX;
        float top = rect.Top - excessY + py * excessY;
        var destRect = new SKRect(left, top, left + targetW, top + targetH);

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

    private void RenderDayOfWeekHeaders(SKCanvas canvas, SKRect bounds, CalendarProject project)
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

        using var gridPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            IsAntialias = false
        };

        float columnWidth = bounds.Width / 7f;

        for (int col = 0; col < 7; col++)
        {
            var cellRect = new SKRect(
         bounds.Left + col * columnWidth,
          bounds.Top,
               bounds.Left + (col + 1) * columnWidth,
        bounds.Bottom);

            string text = displayDays[col];
            float textWidth = textPaint.MeasureText(text);
            canvas.DrawText(text, cellRect.MidX - textWidth / 2, cellRect.MidY + textPaint.TextSize / 2.5f, textPaint);

            // Draw cell borders
            canvas.DrawLine(cellRect.Left, cellRect.Top, cellRect.Right, cellRect.Top, gridPaint);
            canvas.DrawLine(cellRect.Left, cellRect.Bottom, cellRect.Right, cellRect.Bottom, gridPaint);
            canvas.DrawLine(cellRect.Left, cellRect.Top, cellRect.Left, cellRect.Bottom, gridPaint);

            if (col == 6)
            {
                canvas.DrawLine(cellRect.Right, cellRect.Top, cellRect.Right, cellRect.Bottom, gridPaint);
            }
        }
    }

    private void RenderDayGrid(SKCanvas canvas, SKRect bounds, List<List<DateTime?>> weeks, int month, CalendarProject project)
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

        using var gridPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            IsAntialias = false
        };

        float columnWidth = bounds.Width / 7f;
        float rowHeight = bounds.Height / weeks.Count;

        // Draw day numbers
        for (int row = 0; row < weeks.Count; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                var cellRect = new SKRect(
                     bounds.Left + col * columnWidth,
                bounds.Top + row * rowHeight,
                            bounds.Left + (col + 1) * columnWidth,
             bounds.Top + (row + 1) * rowHeight);

                var date = weeks[row][col];
                if (date.HasValue && date.Value.Month == month)
                {
                    string dayText = date.Value.Day.ToString(CultureInfo.InvariantCulture);
                    canvas.DrawText(dayText, cellRect.Left + 2, cellRect.Top + textPaint.TextSize + 2, textPaint);
                }
            }
        }

        // Draw grid lines
        for (int col = 0; col <= 7; col++)
        {
            float x = bounds.Left + col * columnWidth;
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, gridPaint);
        }

        for (int row = 0; row <= weeks.Count; row++)
        {
            float y = bounds.Top + row * rowHeight;
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, gridPaint);
        }

        // Draw outer border
        canvas.DrawRect(bounds, gridPaint);
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

    #endregion
}
