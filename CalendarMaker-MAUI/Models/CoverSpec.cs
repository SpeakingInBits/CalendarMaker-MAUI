namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Defines the layout options for the cover page of a calendar project.
/// </summary>
public enum CoverLayout
{
    /// <summary>
    /// A single full-size photo covering the entire cover page.
    /// </summary>
    FullPhoto,

    /// <summary>
    /// A grid layout with 2 rows and 3 columns of photos (6 photos total).
    /// </summary>
    Grid2x3,

    /// <summary>
    /// A grid layout with 3 rows and 3 columns of photos (9 photos total).
    /// </summary>
    Grid3x3
}

/// <summary>
/// Specifies the configuration settings for the front and back covers of a calendar project.
/// </summary>
public sealed class CoverSpec
{
    /// <summary>
    /// Gets or sets the layout style for the cover page.
    /// Default value is <see cref="CoverLayout.FullPhoto"/>.
    /// </summary>
    public CoverLayout Layout { get; set; } = CoverLayout.FullPhoto;

    /// <summary>
    /// Gets or sets a value indicating whether borderless printing is enabled for the front cover.
    /// When true, the front cover will use no margins for edge-to-edge printing.
    /// Default value is false.
    /// </summary>
    public bool BorderlessFrontCover { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether borderless printing is enabled for the back cover.
    /// When true, the back cover will use no margins for edge-to-edge printing.
    /// Default value is false.
    /// </summary>
    public bool BorderlessBackCover { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the entire calendar uses borderless mode.
    /// When true, all pages (covers and months) will have zero margins for edge-to-edge printing.
    /// Default value is false.
    /// </summary>
    public bool BorderlessCalendar { get; set; } = false;

    /// <summary>
    /// Gets or sets the padding in points around the calendar grid on month pages when in borderless mode.
    /// This creates a colored background area around the calendar grid.
    /// Default value is 20 points (approximately 0.28 inches).
    /// </summary>
    public double CalendarPaddingPt { get; set; } = 20.0;

    /// <summary>
    /// Gets or sets a value indicating whether the calendar area should have a background color
    /// when borderless mode is active on month pages. When true, the calendar half of borderless
    /// pages will use the theme's BackgroundColor, creating visual separation from the borderless photo.
    /// Default value is true.
    /// </summary>
    public bool UseCalendarBackgroundOnBorderless { get; set; } = true;
}