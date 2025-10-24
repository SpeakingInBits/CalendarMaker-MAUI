namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Provides utility methods for retrieving page dimensions in various units.
/// </summary>
public static class PageSizes
{
    /// <summary>
    /// Gets the width and height of a page size in inches before applying orientation.
    /// </summary>
    /// <param name="size">The page size to get dimensions for.</param>
    /// <returns>A tuple containing the width and height in inches. Returns (0, 0) for custom sizes.</returns>
    public static (double widthIn, double heightIn) GetInches(PageSize size)
    {
        return size switch
        {
            PageSize.FiveBySeven => (5.0, 7.0),
            PageSize.A4 => (8.27, 11.69),
            PageSize.Letter => (8.5, 11.0),
            PageSize.Tabloid_11x17 => (11.0, 17.0),
            PageSize.SuperB_13x19 => (13.0, 19.0),
            PageSize.Custom => (0, 0),
            _ => (8.5, 11.0)
        };
    }

    /// <summary>
    /// Gets the width and height of a page in points (1 point = 1/72 inch) with orientation applied.
    /// </summary>
    /// <param name="spec">The page specification containing size, orientation, and optional custom dimensions.</param>
    /// <returns>A tuple containing the width and height in points, adjusted for the specified orientation.</returns>
    public static (double widthPt, double heightPt) GetPoints(PageSpec spec)
    {
        if (spec.Size == PageSize.Custom && spec.CustomWidthPt.HasValue && spec.CustomHeightPt.HasValue)
            return (spec.CustomWidthPt.Value, spec.CustomHeightPt.Value);

        var (wIn, hIn) = GetInches(spec.Size);
        var widthPt = wIn * 72.0;
        var heightPt = hIn * 72.0;
        if (spec.Orientation == PageOrientation.Landscape)
            return (heightPt, widthPt);
        return (widthPt, heightPt);
    }
}
