namespace CalendarMaker_MAUI.Models;

public enum CoverLayout
{
    FullPhoto,
    Grid2x3,
    Grid3x3
}

public sealed class CoverSpec
{
    public CoverLayout Layout { get; set; } = CoverLayout.FullPhoto;
    public string? TitleText { get; set; }
    public string? SubtitleText { get; set; }
    public string? BackCoverTitle { get; set; }
    public string? BackCoverSubtitle { get; set; }
    public string? TitleFontFamily { get; set; } = "Segoe UI";
    public string? SubtitleFontFamily { get; set; } = "Segoe UI";
    public double TitleFontSizePt { get; set; } = 36;
    public double SubtitleFontSizePt { get; set; } = 18;
    public string TitleColorHex { get; set; } = "#000000";
    public string SubtitleColorHex { get; set; } = "#000000";
}
