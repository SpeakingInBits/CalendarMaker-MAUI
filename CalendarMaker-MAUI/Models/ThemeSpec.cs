namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Defines the visual theme settings for a calendar, including font styles, sizes, and colors.
/// </summary>
public sealed class ThemeSpec
{
    /// <summary>
    /// Gets or sets the base font family used throughout the calendar.
    /// Default is "Segoe UI".
    /// </summary>
    public string BaseFontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Gets or sets the font size in points for calendar headers (e.g., month names).
    /// Default is 28 points.
    /// </summary>
    public double HeaderFontSizePt { get; set; } = 28;

    /// <summary>
    /// Gets or sets the font size in points for individual day numbers.
    /// Default is 11 points.
    /// </summary>
    public double DayFontSizePt { get; set; } = 11;

    /// <summary>
    /// Gets or sets the font size in points for calendar titles.
    /// Default is 36 points.
    /// </summary>
    public double TitleFontSizePt { get; set; } = 36;

    /// <summary>
    /// Gets or sets the font size in points for calendar subtitles.
    /// Default is 18 points.
    /// </summary>
    public double SubtitleFontSizePt { get; set; } = 18;

    /// <summary>
    /// Gets or sets the primary text color as a hexadecimal color string.
    /// Default is "#000000" (black).
    /// </summary>
    public string PrimaryTextColor { get; set; } = "#000000";

    /// <summary>
    /// Gets or sets the accent color used for emphasis as a hexadecimal color string.
    /// Default is "#0078D4" (blue).
    /// </summary>
    public string AccentColor { get; set; } = "#0078D4";

    /// <summary>
    /// Gets or sets the background color as a hexadecimal color string.
    /// Default is "#FFFFFF" (white).
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";
}