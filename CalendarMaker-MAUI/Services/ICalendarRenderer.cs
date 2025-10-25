using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service responsible for rendering calendar elements to an SKCanvas.
/// Centralizes all drawing logic for calendar grids, photos, and covers.
/// </summary>
public interface ICalendarRenderer
{
    /// <summary>
    /// Renders a complete calendar page including photos and calendar grid.
    /// </summary>
    /// <param name="canvas">The SKCanvas to render to.</param>
    /// <param name="bounds">The bounding rectangle for the calendar section.</param>
    /// <param name="project">The calendar project containing theme and settings.</param>
    /// <param name="year">The year to render.</param>
    /// <param name="month">The month to render (1-12).</param>
    void RenderCalendarGrid(SKCanvas canvas, SKRect bounds, CalendarProject project, int year, int month);

    /// <summary>
    /// Renders photos in their designated slots with pan/zoom applied.
    /// </summary>
    /// <param name="canvas">The SKCanvas to render to.</param>
    /// <param name="photoSlots">The rectangles defining photo positions.</param>
    /// <param name="assets">The image assets to render.</param>
    /// <param name="role">The role filter for assets (e.g., "monthPhoto", "coverPhoto").</param>
    /// <param name="monthIndex">Optional month index for filtering month photos.</param>
    /// <param name="activeSlotIndex">The currently active slot for highlighting.</param>
    void RenderPhotoSlots(SKCanvas canvas, List<SKRect> photoSlots, List<ImageAsset> assets, string role, int? monthIndex = null, int activeSlotIndex = -1);

    /// <summary>
    /// Renders a single photo with pan/zoom transformations applied.
    /// </summary>
    /// <param name="canvas">The SKCanvas to render to.</param>
    /// <param name="bitmap">The bitmap to render.</param>
    /// <param name="rect">The destination rectangle.</param>
    /// <param name="asset">The asset containing pan/zoom settings.</param>
    void RenderPhotoWithTransform(SKCanvas canvas, SKBitmap bitmap, SKRect rect, ImageAsset asset);

    /// <summary>
    /// Renders an empty photo slot placeholder.
    /// </summary>
    /// <param name="canvas">The SKCanvas to render to.</param>
    /// <param name="rect">The slot rectangle.</param>
    /// <param name="isActive">Whether this slot is currently active.</param>
    /// <param name="hintText">Optional hint text to display.</param>
    void RenderEmptySlot(SKCanvas canvas, SKRect rect, bool isActive, string? hintText = null);

    /// <summary>
    /// Renders a highlight border around the active slot.
    /// </summary>
    /// <param name="canvas">The SKCanvas to render to.</param>
    /// <param name="rect">The rectangle to highlight.</param>
    /// <param name="color">The highlight color.</param>
    /// <param name="strokeWidth">The border width.</param>
    void RenderSlotHighlight(SKCanvas canvas, SKRect rect, SKColor color, float strokeWidth = 2f);
}
