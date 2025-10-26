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
    private readonly IImageProcessor _imageProcessor;

    public CalendarRenderer(ICalendarEngine calendarEngine, IImageProcessor imageProcessor)
    {
        _calendarEngine = calendarEngine;
        _imageProcessor = imageProcessor;
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

    /// <summary>
    /// Renders the calendar grid with an optional background color.
    /// </summary>
    public void RenderCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month, bool applyBackground)
    {
      SKRect calendarRect = bounds;
   
        // Apply padding to the calendar grid if in borderless mode
        if (applyBackground && project.CoverSpec.BorderlessCalendar)
        {
         float padding = (float)project.CoverSpec.CalendarPaddingPt;
    calendarRect = new SKRect(
     bounds.Left + padding,
  bounds.Top + padding,
         bounds.Right - padding,
    bounds.Bottom - padding
        );

         // Draw background color ONLY in the padding area (not under the calendar grid)
   if (!string.IsNullOrEmpty(project.Theme.BackgroundColor))
   {
    using var bgPaint = new SKPaint
      {
Color = SKColor.Parse(project.Theme.BackgroundColor),
Style = SKPaintStyle.Fill
   };
   
         // Draw background in the full bounds area
       canvas.DrawRect(bounds, bgPaint);
 
   // Draw white/transparent rectangle over the calendar grid area (EXCEPT the header)
      // to "cut out" the background, leaving the header in the colored area
     RenderCalendarGridWithHeaderInPadding(canvas, calendarRect, project, year, month);
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

      // Render the standard calendar grid
    RenderCalendarGrid(canvas, calendarRect, project, year, month);
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
                var bitmap = _imageProcessor.LoadBitmap(asset.Path, useCache: false);
                if (bitmap != null)
                {
                    using (bitmap)
                    {
                        canvas.Save();
                        canvas.ClipRect(rect, antialias: true);
                        RenderPhotoWithTransform(canvas, bitmap, rect, asset);
                        canvas.Restore();
                    }
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

    private void RenderCalendarGridWithHeaderInPadding(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month)
    {
        var weeks = _calendarEngine.BuildMonthGrid(year, month, project.FirstDayOfWeek);

        const float headerHeight = 40f;
        const float dayOfWeekHeight = 20f;

   var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + headerHeight);
  var gridRect = new SKRect(bounds.Left, headerRect.Bottom, bounds.Right, bounds.Bottom);

   // Render month/year title - this will appear in the colored padding area
  // Determine text color based on background
    SKColor titleColor = GetContrastingTextColor(project.Theme.BackgroundColor);
        using var titlePaint = new SKPaint
        {
 Color = titleColor,
     TextSize = 18,
     IsAntialias = true
        };

     string title = new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        float titleWidth = titlePaint.MeasureText(title);
     canvas.DrawText(title, gridRect.MidX - titleWidth / 2, headerRect.MidY + titlePaint.TextSize / 2.5f, titlePaint);

   // NOW draw white rectangle to cut out background, but only BELOW the header
  // This leaves the header in the colored area
     using var clearPaint = new SKPaint
        {
     Color = SKColors.White,
  Style = SKPaintStyle.Fill
        };
   canvas.DrawRect(gridRect, clearPaint);

        // Render day-of-week headers
   var dowRect = new SKRect(gridRect.Left, gridRect.Top, gridRect.Right, gridRect.Top + dayOfWeekHeight);
        RenderDayOfWeekHeaders(canvas, dowRect, project);

        // Render day grid
   var weeksArea = new SKRect(gridRect.Left, dowRect.Bottom, gridRect.Right, bounds.Bottom);
   RenderDayGrid(canvas, weeksArea, weeks, month, project);
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
