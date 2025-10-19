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
    
    // Enable borderless printing for covers - uses no margins
    public bool BorderlessFrontCover { get; set; } = false;
    public bool BorderlessBackCover { get; set; } = false;
}

