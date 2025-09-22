namespace CalendarMaker_MAUI.Models;

public enum PageSize
{
    FiveBySeven,
    A4,
    Letter,
    Tabloid_11x17,
    SuperB_13x19,
    Custom
}

public enum PageOrientation
{
    Portrait,
    Landscape
}

public sealed class PageSpec
{
    public PageSize Size { get; set; } = PageSize.Letter;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    // For custom sizes, in points (1 pt = 1/72 in)
    public double? CustomWidthPt { get; set; }
    public double? CustomHeightPt { get; set; }
}
