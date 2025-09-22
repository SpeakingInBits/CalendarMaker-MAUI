namespace CalendarMaker_MAUI.Models;

public sealed class ThemeSpec
{
    public string BaseFontFamily { get; set; } = "Segoe UI";
    public double HeaderFontSizePt { get; set; } = 28;
    public double DayFontSizePt { get; set; } = 11;
    public double TitleFontSizePt { get; set; } = 36;
    public double SubtitleFontSizePt { get; set; } = 18;
    public string PrimaryTextColor { get; set; } = "#000000";
    public string AccentColor { get; set; } = "#0078D4";
    public string BackgroundColor { get; set; } = "#FFFFFF";
}
