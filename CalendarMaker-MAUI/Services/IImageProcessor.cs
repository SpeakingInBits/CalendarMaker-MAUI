using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service responsible for image processing operations including loading, caching, and transformations.
/// Centralizes bitmap operations to improve performance and reduce memory usage.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Loads a bitmap from a file path with optional caching.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="useCache">Whether to use the cache for this image.</param>
    /// <returns>The loaded SKBitmap, or null if loading failed.</returns>
    SKBitmap? LoadBitmap(string path, bool useCache = false);

    /// <summary>
    /// Gets or loads a bitmap from cache. If not in cache, loads and caches it.
    /// Thread-safe for parallel operations.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <returns>The cached or newly loaded SKBitmap, or null if loading failed.</returns>
    SKBitmap? GetOrLoadCached(string path);

    /// <summary>
    /// Calculates the pan/zoom transformation parameters for an image to fit within a rectangle.
    /// </summary>
    /// <param name="imageWidth">The width of the source image in pixels.</param>
    /// <param name="imageHeight">The height of the source image in pixels.</param>
    /// <param name="targetRect">The target rectangle to fit the image into.</param>
    /// <param name="asset">The asset containing pan/zoom settings.</param>
 /// <returns>A tuple containing the destination rectangle for drawing.</returns>
    SKRect CalculateTransformedRect(float imageWidth, float imageHeight, SKRect targetRect, ImageAsset asset);

    /// <summary>
    /// Calculates pan/zoom limits for an image within a target rectangle.
    /// </summary>
    /// <param name="imageWidth">The width of the source image in pixels.</param>
    /// <param name="imageHeight">The height of the source image in pixels.</param>
    /// <param name="targetRect">The target rectangle.</param>
    /// <param name="zoom">The zoom level (0.5 to 3.0).</param>
    /// <returns>A tuple containing (excessX, excessY) - the amount of panning space available.</returns>
 (float excessX, float excessY) CalculatePanLimits(float imageWidth, float imageHeight, SKRect targetRect, double zoom);

    /// <summary>
    /// Clears all cached bitmaps to free memory.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the current number of cached bitmaps.
    /// </summary>
    int CachedImageCount { get; }

    /// <summary>
    /// Estimates the total memory used by cached bitmaps in bytes.
    /// </summary>
 long EstimatedCacheMemoryBytes { get; }
}
