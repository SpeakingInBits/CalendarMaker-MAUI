namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Defines the placement of photo and calendar sections on a calendar page.
/// </summary>
public enum LayoutPlacement
{
    /// <summary>
    /// Photo section is positioned at the top with calendar section at the bottom.
    /// </summary>
    PhotoTopCalendarBottom,

    /// <summary>
    /// Photo section is positioned at the bottom with calendar section at the top.
    /// </summary>
    PhotoBottomCalendarTop,

    /// <summary>
    /// Photo section is positioned on the left with calendar section on the right.
    /// </summary>
    PhotoLeftCalendarRight,

    /// <summary>
    /// Photo section is positioned on the right with calendar section on the left.
    /// </summary>
    PhotoRightCalendarLeft
}

/// <summary>
/// Defines how photos should fill their allocated space.
/// </summary>
public enum PhotoFillMode
{
    /// <summary>
    /// Photo is scaled to cover the entire area, potentially cropping parts of the image.
    /// </summary>
    Cover,

    /// <summary>
    /// Photo is scaled to fit within the area while maintaining aspect ratio, potentially leaving empty space.
    /// </summary>
    Contain
}

/// <summary>
/// Defines horizontal text alignment options.
/// </summary>
public enum TextAlignment
{
    /// <summary>
    /// Text is aligned to the left edge.
    /// </summary>
    Left,

    /// <summary>
    /// Text is centered horizontally.
    /// </summary>
    Center,

    /// <summary>
    /// Text is aligned to the right edge.
    /// </summary>
    Right
}

/// <summary>
/// Defines the arrangement of multiple photos within the photo section.
/// </summary>
public enum PhotoLayout
{
    /// <summary>
    /// Single photo occupying the entire photo area.
    /// </summary>
    Single,

    /// <summary>
    /// Two photos arranged side-by-side with a vertical divider.
    /// </summary>
    TwoVerticalSplit,

    /// <summary>
    /// Four photos arranged in a 2x2 grid layout.
    /// </summary>
    Grid2x2,

    /// <summary>
    /// Two photos stacked vertically with a horizontal divider.
    /// </summary>
    TwoHorizontalStack,

    /// <summary>
    /// Three photos with two stacked vertically on the left and one on the right.
    /// </summary>
    ThreeLeftStack,

    /// <summary>
    /// Three photos with two stacked vertically on the right and one on the left.
    /// </summary>
    ThreeRightStack
}

/// <summary>
/// Specifies the layout configuration for a calendar page, including photo and calendar placement, sizing, and styling.
/// </summary>
public sealed class LayoutSpec
{
    /// <summary>
    /// Gets or sets the placement of photo and calendar sections on the page.
    /// </summary>
    public LayoutPlacement Placement { get; set; } = LayoutPlacement.PhotoTopCalendarBottom;

    /// <summary>
    /// Gets or sets the portion of the page dedicated to the first element in the placement order.
    /// Value ranges from 0.0 to 1.0, where 0.5 means equal split between sections.
    /// </summary>
    public double SplitRatio { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets how photos should fill their allocated space.
    /// </summary>
    public PhotoFillMode PhotoFill { get; set; } = PhotoFillMode.Cover;

    /// <summary>
    /// Gets or sets the horizontal text alignment for calendar elements.
    /// </summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    /// <summary>
    /// Gets or sets the arrangement of multiple photos within the photo section.
    /// </summary>
    public PhotoLayout PhotoLayout { get; set; } = PhotoLayout.Single;
}