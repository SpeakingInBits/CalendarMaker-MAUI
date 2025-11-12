using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service responsible for calculating photo slot positions and layout splits.
/// Extracts layout calculation logic from UI and export services to eliminate duplication
/// and improve testability.
/// </summary>
public interface ILayoutCalculator
{
    /// <summary>
    /// Computes photo slot rectangles for a given area and layout type.
    /// </summary>
    /// <param name="area">The bounding rectangle to divide into photo slots.</param>
    /// <param name="layout">The photo layout pattern to apply.</param>
    /// <returns>A list of rectangles representing individual photo slots.</returns>
    List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout);

    /// <summary>
    /// Computes the split between photo and calendar sections based on layout specifications and page orientation.
    /// Automatically adjusts placement for landscape vs portrait orientations.
    /// </summary>
    /// <param name="area">The total area to split.</param>
    /// <param name="spec">The layout specification containing placement and split ratio.</param>
    /// <param name="pageSpec">The page specification containing orientation information.</param>
    /// <returns>A tuple containing the photo rectangle and calendar rectangle.</returns>
    (SKRect photoRect, SKRect calendarRect) ComputeSplit(SKRect area, LayoutSpec spec, PageSpec pageSpec);
}
