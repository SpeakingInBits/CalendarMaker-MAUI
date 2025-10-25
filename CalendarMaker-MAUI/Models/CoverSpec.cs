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
}