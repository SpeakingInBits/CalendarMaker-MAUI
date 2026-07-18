using System.Text;
using CalendarMaker_MAUI.Models;
using SkiaSharp;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Shared SkiaSharp drawing routine for rendering event chips inside a day cell. Used by both the
/// on-screen <see cref="CalendarRenderer"/> and the PDF exporter so the preview matches the export.
/// </summary>
internal static class CalendarEventDrawing
{
    private static readonly SKColor DefaultChipColor = new(0x4E, 0x79, 0xA7);
    private static readonly SKColor OverflowChipColor = new(0x9E, 0x9E, 0x9E);

    // Platform emoji-capable typeface (e.g. Segoe UI Emoji on Windows) resolved by matching a
    // known emoji code point. Used as a fallback for glyphs the default typeface cannot render.
    private static readonly SKTypeface? EmojiTypeface = SKFontManager.Default.MatchCharacter(0x1F600);

    private const float OuterPad = 2f;
    private const float ChipGap = 1.5f;
    private const float TextPadX = 3f;
    private const float TextPadY = 1.5f;

    /// <summary>
    /// Draws the events occurring on <paramref name="date"/> as colored chips stacked below the day
    /// number inside <paramref name="cell"/>. Long titles word-wrap onto multiple lines within the
    /// cell width; events that do not fit are summarized with a "+N more" chip. Emoji in titles are
    /// rendered via an emoji-capable fallback font.
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

        float fontSize = Math.Clamp(cell.Height / 6f - 3f, 5f, 8f);
        float lineHeight = fontSize + 2f;
        float singleChipHeight = lineHeight + 2f * TextPadY;

        float left = cell.Left + OuterPad;
        float right = cell.Right - OuterPad;
        float innerWidth = right - left - 2f * TextPadX;

        float top = cell.Top + dayNumberHeight;
        float bottom = cell.Bottom - OuterPad;
        float availableHeight = bottom - top;

        if (innerWidth <= 1f || availableHeight < singleChipHeight)
        {
            return; // Cell too small to show any chips.
        }

        using var chipPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { IsAntialias = true };
        using var baseFont = new SKFont(SKTypeface.Default, fontSize) { Edging = SKFontEdging.SubpixelAntialias };
        using var emojiFont = EmojiTypeface is null ? null : new SKFont(EmojiTypeface, fontSize) { Edging = SKFontEdging.SubpixelAntialias };

        // Lay out as many event chips as fit, each wrapped to the cell width.
        var chips = new List<(List<string> Lines, SKColor Color)>();
        float usedHeight = 0f;
        int placed = 0;
        for (; placed < events.Count; placed++)
        {
            var lines = WrapText(events[placed].Title.Trim(), innerWidth, baseFont, emojiFont, textPaint);
            if (lines.Count == 0)
            {
                lines.Add(string.Empty);
            }

            float chipHeight = lines.Count * lineHeight + 2f * TextPadY;
            float needed = chipHeight + (chips.Count > 0 ? ChipGap : 0f);
            if (usedHeight + needed > availableHeight)
            {
                break;
            }

            chips.Add((lines, ParseColor(events[placed].ColorHex, DefaultChipColor)));
            usedHeight += needed;
        }

        // If even the first event is taller than the cell, show it truncated rather than nothing.
        if (chips.Count == 0)
        {
            int maxLines = Math.Max(1, (int)((availableHeight - 2f * TextPadY) / lineHeight));
            var lines = TruncateToLines(WrapText(events[0].Title.Trim(), innerWidth, baseFont, emojiFont, textPaint), maxLines, innerWidth, baseFont, emojiFont, textPaint);
            chips.Add((lines, ParseColor(events[0].ColorHex, DefaultChipColor)));
            usedHeight = lines.Count * lineHeight + 2f * TextPadY;
            placed = 1;
        }

        int leftover = events.Count - placed;

        // Reserve room for a "+N more" chip, dropping trailing events if necessary.
        if (leftover > 0)
        {
            float overflowNeeded = singleChipHeight + ChipGap;
            while (chips.Count > 1 && usedHeight + overflowNeeded > availableHeight)
            {
                var dropped = chips[^1];
                usedHeight -= dropped.Lines.Count * lineHeight + 2f * TextPadY + ChipGap;
                chips.RemoveAt(chips.Count - 1);
                leftover++;
            }
        }

        // Render the chips.
        float y = top;
        foreach (var (lines, color) in chips)
        {
            float chipHeight = lines.Count * lineHeight + 2f * TextPadY;
            var chipRect = new SKRect(left, y, right, y + chipHeight);
            DrawChip(canvas, chipPaint, textPaint, baseFont, emojiFont, chipRect, lines, color, lineHeight, fontSize);
            y = chipRect.Bottom + ChipGap;
        }

        // Render the overflow indicator if any events did not fit.
        if (leftover > 0 && y + singleChipHeight <= bottom + 0.5f)
        {
            var chipRect = new SKRect(left, y, right, y + singleChipHeight);
            DrawChip(canvas, chipPaint, textPaint, baseFont, emojiFont, chipRect, new List<string> { $"+{leftover} more" }, OverflowChipColor, lineHeight, fontSize);
        }
    }

    private static void DrawChip(SKCanvas canvas, SKPaint chipPaint, SKPaint textPaint, SKFont baseFont, SKFont? emojiFont, SKRect chipRect, List<string> lines, SKColor color, float lineHeight, float fontSize)
    {
        chipPaint.Color = color;
        float radius = Math.Min(4f, chipRect.Height / 3f);
        canvas.DrawRoundRect(chipRect, radius, radius, chipPaint);

        textPaint.Color = ContrastingTextColor(color);
        canvas.Save();
        canvas.ClipRect(chipRect);
        float baseline = chipRect.Top + TextPadY + fontSize;
        foreach (string line in lines)
        {
            DrawText(canvas, line, chipRect.Left + TextPadX, baseline, baseFont, emojiFont, textPaint);
            baseline += lineHeight;
        }
        canvas.Restore();
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines that each fit within <paramref name="maxWidth"/>,
    /// breaking on spaces and hard-breaking any single word that is wider than the line.
    /// </summary>
    private static List<string> WrapText(string text, float maxWidth, SKFont baseFont, SKFont? emojiFont, SKPaint paint)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return lines;
        }

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (lines.Count > 0)
            {
                string candidate = lines[^1] + " " + word;
                if (MeasureText(candidate, baseFont, emojiFont, paint) <= maxWidth)
                {
                    lines[^1] = candidate;
                    continue;
                }
            }

            string remaining = word;
            while (MeasureText(remaining, baseFont, emojiFont, paint) > maxWidth && remaining.Length > 1)
            {
                int fit = MaxCharsThatFit(remaining, maxWidth, baseFont, emojiFont, paint);
                lines.Add(remaining.Substring(0, fit));
                remaining = remaining.Substring(fit);
            }

            lines.Add(remaining);
        }

        return lines;
    }

    private static List<string> TruncateToLines(List<string> lines, int maxLines, float maxWidth, SKFont baseFont, SKFont? emojiFont, SKPaint paint)
    {
        if (lines.Count <= maxLines)
        {
            return lines;
        }

        var shown = lines.Take(maxLines).ToList();
        string last = shown[^1];
        while (last.Length > 0 && MeasureText(last + "…", baseFont, emojiFont, paint) > maxWidth)
        {
            last = last.Substring(0, last.Length - 1);
        }

        shown[^1] = last + "…";
        return shown;
    }

    private static int MaxCharsThatFit(string text, float maxWidth, SKFont baseFont, SKFont? emojiFont, SKPaint paint)
    {
        int count = 0;
        while (count < text.Length && MeasureText(text.Substring(0, count + 1), baseFont, emojiFont, paint) <= maxWidth)
        {
            count++;
        }

        return Math.Max(1, count); // Always make progress, even for a very narrow cell.
    }

    /// <summary>
    /// Measures text, using the emoji fallback font for any code points the default font lacks so
    /// wrapping accounts for emoji width.
    /// </summary>
    private static float MeasureText(string text, SKFont baseFont, SKFont? emojiFont, SKPaint paint)
    {
        float width = 0f;
        foreach ((string run, SKFont font) in SplitIntoFontRuns(text, baseFont, emojiFont))
        {
            width += font.MeasureText(run, paint);
        }

        return width;
    }

    /// <summary>
    /// Draws text left-to-right from <paramref name="x"/>, switching to the emoji fallback font for
    /// runs of glyphs the default font cannot render (so emoji actually appear).
    /// </summary>
    private static void DrawText(SKCanvas canvas, string text, float x, float baseline, SKFont baseFont, SKFont? emojiFont, SKPaint paint)
    {
        foreach ((string run, SKFont font) in SplitIntoFontRuns(text, baseFont, emojiFont))
        {
            canvas.DrawText(run, x, baseline, SKTextAlign.Left, font, paint);
            x += font.MeasureText(run, paint);
        }
    }

    /// <summary>
    /// Groups consecutive code points of <paramref name="text"/> into runs that share a font: the
    /// default font when it has the glyph, otherwise the emoji fallback font when available.
    /// </summary>
    private static IEnumerable<(string Text, SKFont Font)> SplitIntoFontRuns(string text, SKFont baseFont, SKFont? emojiFont)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var builder = new StringBuilder();
        SKFont currentFont = baseFont;

        int i = 0;
        while (i < text.Length)
        {
            int codePoint;
            int length;
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(c, text[i + 1]);
                length = 2;
            }
            else
            {
                codePoint = c;
                length = 1;
            }

            SKFont font = baseFont;
            if (emojiFont is not null && !baseFont.ContainsGlyph(codePoint) && emojiFont.ContainsGlyph(codePoint))
            {
                font = emojiFont;
            }

            if (builder.Length > 0 && font != currentFont)
            {
                yield return (builder.ToString(), currentFont);
                builder.Clear();
            }

            currentFont = font;
            builder.Append(text, i, length);
            i += length;
        }

        if (builder.Length > 0)
        {
            yield return (builder.ToString(), currentFont);
        }
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
