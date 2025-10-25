namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Specifies predefined page sizes for calendar pages.
/// </summary>
public enum PageSize
{
    /// <summary>
    /// 5x7 inch page size.
    /// </summary>
    FiveBySeven,

    /// <summary>
    /// A4 page size (210 x 297 mm).
    /// </summary>
    A4,

    /// <summary>
    /// Letter page size (8.5 x 11 inches).
    /// </summary>
    Letter,

    /// <summary>
    /// Tabloid page size (11 x 17 inches).
    /// </summary>
    Tabloid_11x17,

    /// <summary>
    /// Super B page size (13 x 19 inches).
    /// </summary>
    SuperB_13x19,

    /// <summary>
    /// Custom page size with user-defined dimensions.
    /// </summary>
    Custom
}

/// <summary>
/// Specifies the orientation of a page.
/// </summary>
public enum PageOrientation
{
    /// <summary>
    /// Portrait orientation (vertical).
    /// </summary>
    Portrait,

    /// <summary>
    /// Landscape orientation (horizontal).
    /// </summary>
    Landscape
}

/// <summary>
/// Specifies page dimensions and orientation for calendar pages.
/// </summary>
public sealed class PageSpec
{
    /// <summary>
    /// Gets or sets the predefined page size. Default is Letter.
    /// </summary>
    public PageSize Size { get; set; } = PageSize.Letter;

    /// <summary>
    /// Gets or sets the page orientation. Default is Portrait.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// Gets or sets the custom page width in points (1 pt = 1/72 inch).
    /// This property is only used when Size is set to Custom.
    /// </summary>
    public double? CustomWidthPt { get; set; }

    /// <summary>
    /// Gets or sets the custom page height in points (1 pt = 1/72 inch).
    /// This property is only used when Size is set to Custom.
    /// </summary>
    public double? CustomHeightPt { get; set; }
}