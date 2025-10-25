using SkiaSharp;
using CalendarMaker_MAUI.Models;
using System.Collections.Concurrent;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of IImageProcessor that handles bitmap loading, caching, and transformations.
/// Uses a thread-safe concurrent dictionary for caching during parallel export operations.
/// </summary>
public sealed class ImageProcessor : IImageProcessor, IDisposable
{
    private readonly ConcurrentDictionary<string, SKBitmap> _cache = new();
    private bool _disposed;

    /// <inheritdoc />
    public SKBitmap? LoadBitmap(string path, bool useCache = false)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
        System.Diagnostics.Debug.WriteLine($"ImageProcessor: File not found - {path}");
            return null;
        }

        try
        {
      if (useCache)
            {
          return GetOrLoadCached(path);
     }

            return SKBitmap.Decode(path);
        }
        catch (Exception ex)
      {
System.Diagnostics.Debug.WriteLine($"ImageProcessor: Error loading bitmap from {path} - {ex.Message}");
   return null;
     }
    }

    /// <inheritdoc />
    public SKBitmap? GetOrLoadCached(string path)
 {
      if (string.IsNullOrEmpty(path))
        {
   return null;
        }

        // Thread-safe get-or-add operation
        return _cache.GetOrAdd(path, p =>
        {
        try
            {
          var bitmap = SKBitmap.Decode(p);
    if (bitmap == null)
       {
         System.Diagnostics.Debug.WriteLine($"ImageProcessor: Failed to decode {p}");
}
          return bitmap;
     }
 catch (Exception ex)
     {
   System.Diagnostics.Debug.WriteLine($"ImageProcessor: Error caching bitmap from {p} - {ex.Message}");
     return null!;
            }
        });
    }

    /// <inheritdoc />
    public SKRect CalculateTransformedRect(float imageWidth, float imageHeight, SKRect targetRect, ImageAsset asset)
    {
    if (imageWidth <= 0 || imageHeight <= 0)
     {
          return targetRect;
   }

float rectWidth = targetRect.Width;
        float rectHeight = targetRect.Height;
        float imageAspect = imageWidth / imageHeight;
        float rectAspect = rectWidth / rectHeight;

        // Calculate base scale to cover the rectangle (crop to fill)
        float baseScale = imageAspect > rectAspect 
         ? rectHeight / imageHeight 
     : rectWidth / imageWidth;

        // Apply zoom (clamped between 0.5x and 3x)
        float zoom = (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);
     float scale = baseScale * zoom;

        float targetWidth = imageWidth * scale;
        float targetHeight = imageHeight * scale;

        // Calculate pan limits
        float excessX = Math.Max(0, (targetWidth - rectWidth) / 2f);
        float excessY = Math.Max(0, (targetHeight - rectHeight) / 2f);

        // Apply pan (clamped between -1 and 1)
      float panX = (float)Math.Clamp(asset.PanX, -1, 1);
   float panY = (float)Math.Clamp(asset.PanY, -1, 1);

        // Calculate final position
        float left = targetRect.Left - excessX + panX * excessX;
        float top = targetRect.Top - excessY + panY * excessY;

return new SKRect(left, top, left + targetWidth, top + targetHeight);
    }

    /// <inheritdoc />
    public (float excessX, float excessY) CalculatePanLimits(float imageWidth, float imageHeight, SKRect targetRect, double zoom)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return (0f, 0f);
        }

        float rectWidth = targetRect.Width;
        float rectHeight = targetRect.Height;
  float imageAspect = imageWidth / imageHeight;
        float rectAspect = rectWidth / rectHeight;

        // Calculate base scale
        float baseScale = imageAspect > rectAspect 
? rectHeight / imageHeight 
            : rectWidth / imageWidth;

        // Apply zoom
     float zoomClamped = (float)Math.Clamp(zoom <= 0 ? 1 : zoom, 0.5, 3.0);
      float scale = baseScale * zoomClamped;

        float targetWidth = imageWidth * scale;
        float targetHeight = imageHeight * scale;

      float excessX = Math.Max(0, (targetWidth - rectWidth) / 2f);
        float excessY = Math.Max(0, (targetHeight - rectHeight) / 2f);

        return (excessX, excessY);
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        foreach (var bitmap in _cache.Values)
        {
bitmap?.Dispose();
        }
 _cache.Clear();
        
      System.Diagnostics.Debug.WriteLine("ImageProcessor: Cache cleared");
    }

    /// <inheritdoc />
    public int CachedImageCount => _cache.Count;

    /// <inheritdoc />
    public long EstimatedCacheMemoryBytes
    {
        get
        {
            long total = 0;
  foreach (var bitmap in _cache.Values)
 {
      if (bitmap != null)
       {
    // Each pixel is typically 4 bytes (RGBA)
         total += bitmap.Width * bitmap.Height * 4;
                }
            }
  return total;
        }
    }

    /// <summary>
  /// Disposes of all cached bitmaps.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
      return;
        }

        ClearCache();
        _disposed = true;
    }
}
