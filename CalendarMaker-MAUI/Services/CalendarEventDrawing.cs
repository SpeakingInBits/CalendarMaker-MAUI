using CalendarMaker_MAUI.Models;
using SkiaSharp;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Shared SkiaSharp drawing routine for rendering event chips inside a day cell. Used by both the
/// on-screen <see cref="CalendarRenderer"/> and the PDF exporter so the preview matches the export.
/// </summary>
internal static class CalendarEventDrawing
{
    /// <summary>
    /// Draws the events occurring on <paramref name="date"/> as small colored chips stacked below the
    /// day number inside <paramref name="cell"/>. Excess events are summarized with a "+N" chip.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="cell">The day cell rectangle.</param>
    /// <param name="project">The project supplying the event list.</param>
    /// <param name="date">The date whose events should be drawn.</param>
    /// <param name="dayNumberHeight">Vertical space reserved at the top of the cell for the day number.</param>
    public static void DrawDayEvents(SKCanvas canvas, SKRect cell, CalendarProject project, DateTime date, float dayNumberHeight = 14f)
    {
        var events = CalendarEventService.GetEventsForDate(project.Events, date);
        if (events.Count == 0)
        {
            return;
        }

        const float horizontalPad = 2f;
        const float chipGap = 1.5f;
        float chipHeight = Math.Clamp(cell.Height / 6f, 6f, 11f);
        float fontSize = Math.Clamp(chipHeight - 3f, 5f, 8f);

        float top = cell.Top + dayNumberHeight;
        float available = cell.Bottom - top - horizontalPad;
        if (available < chipHeight)
        {
            return; // Cell too short to show chips.
        }

        int maxChips = Math.Max(1, (int)((available + chipGap) / (chipHeight + chipGap)));
        bool needsOverflow = events.Count > maxChips;
        int drawCount = needsOverflow ? Math.Max(1, maxChips - 1) : Math.Min(events.Count, maxChips);

        using var chipPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { TextSize = fontSize, IsAntialias = true };

        float left = cell.Left + horizontalPad;
        float right = cell.Right - horizontalPad;
        float y = top;

        for (int i = 0; i < drawCount; i++)
        {
            var ev = events[i];
            var chipRect = new SKRect(left, y, right, y + chipHeight);
            SKColor color = ParseColor(ev.ColorHex, new SKColor(0x4E, 0x79, 0xA7));
            chipPaint.Color = color;
            float radius = chipHeight / 3f;
            canvas.DrawRoundRect(chipRect, radius, radius, chipPaint);

            string label = ev.Title.Trim();
            if (!string.IsNullOrEmpty(label))
            {
                textPaint.Color = ContrastingTextColor(color);
                DrawClippedText(canvas, textPaint, label, chipRect, fontSize);
            }

            y += chipHeight + chipGap;
        }

        if (needsOverflow)
        {
            int remaining = events.Count - drawCount;
            var chipRect = new SKRect(left, y, right, y + chipHeight);
            chipPaint.Color = new SKColor(0x9E, 0x9E, 0x9E);
            float radius = chipHeight / 3f;
            canvas.DrawRoundRect(chipRect, radius, radius, chipPaint);
            textPaint.Color = SKColors.White;
            DrawClippedText(canvas, textPaint, $"+{remaining}", chipRect, fontSize);
        }
    }

    private static void DrawClippedText(SKCanvas canvas, SKPaint textPaint, string text, SKRect chipRect, float fontSize)
    {
        canvas.Save();
        canvas.ClipRect(chipRect);
        float baseline = chipRect.MidY + fontSize / 2.8f;
        canvas.DrawText(text, chipRect.Left + 2f, baseline, textPaint);
        canvas.Restore();
    }

    private static SKColor ParseColor(string? hex, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        return SKColor.TryParse(hex, out SKColor parsed) ? parsed : fallback;
    }

    private static SKColor ContrastingTextColor(SKColor background)
    {
        float luminance = (0.299f * background.Red + 0.587f * background.Green + 0.114f * background.Blue) / 255f;
        return luminance > 0.6f ? SKColors.Black : SKColors.White;
    }
}
