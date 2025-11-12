using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of ILayoutCalculator that computes photo slot positions and layout splits.
/// This service consolidates layout calculation logic that was previously duplicated
/// in DesignerPage and PdfExportService.
/// </summary>
public sealed class LayoutCalculator : ILayoutCalculator
{
    private const float SlotGap = 4f;

    /// <inheritdoc />
    public List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout)
    {
        return layout switch
        {
            PhotoLayout.TwoVerticalSplit => ComputeTwoVerticalSplit(area),
            PhotoLayout.Grid2x2 => ComputeGrid2x2(area),
            PhotoLayout.TwoHorizontalStack => ComputeTwoHorizontalStack(area),
            PhotoLayout.ThreeLeftStack => ComputeThreeLeftStack(area),
            PhotoLayout.ThreeRightStack => ComputeThreeRightStack(area),
            _ => [area] // Single photo (default)
        };
    }

    /// <inheritdoc />
    public (SKRect photoRect, SKRect calendarRect) ComputeSplit(SKRect area, LayoutSpec spec, PageSpec pageSpec)
    {
        float ratio = (float)Math.Clamp(spec.SplitRatio, 0.1, 0.9);

        // Determine the effective placement based on orientation
        LayoutPlacement effectivePlacement = spec.Placement;

        // Auto-adjust placement based on orientation
        if (pageSpec.Orientation == PageOrientation.Landscape)
        {
            // In landscape, convert vertical placements to horizontal
            effectivePlacement = spec.Placement switch
            {
                LayoutPlacement.PhotoTopCalendarBottom => LayoutPlacement.PhotoLeftCalendarRight,
                LayoutPlacement.PhotoBottomCalendarTop => LayoutPlacement.PhotoRightCalendarLeft,
                // Keep horizontal placements as-is
                _ => spec.Placement
            };
        }
        else // Portrait
        {
            // In portrait, convert horizontal placements to vertical
            effectivePlacement = spec.Placement switch
            {
                LayoutPlacement.PhotoLeftCalendarRight => LayoutPlacement.PhotoTopCalendarBottom,
                LayoutPlacement.PhotoRightCalendarLeft => LayoutPlacement.PhotoBottomCalendarTop,
                // Keep vertical placements as-is
                _ => spec.Placement
            };
        }

        return effectivePlacement switch
        {
            LayoutPlacement.PhotoLeftCalendarRight => (
             new SKRect(area.Left, area.Top, area.Left + area.Width * ratio, area.Bottom),
             new SKRect(area.Left + area.Width * ratio, area.Top, area.Right, area.Bottom)
           ),
            LayoutPlacement.PhotoRightCalendarLeft => (
               new SKRect(area.Left + area.Width * (1 - ratio), area.Top, area.Right, area.Bottom),
                 new SKRect(area.Left, area.Top, area.Left + area.Width * (1 - ratio), area.Bottom)
              ),
            LayoutPlacement.PhotoTopCalendarBottom => (
                new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * ratio),
                new SKRect(area.Left, area.Top + area.Height * ratio, area.Right, area.Bottom)
            ),
            LayoutPlacement.PhotoBottomCalendarTop => (
           new SKRect(area.Left, area.Top + area.Height * (1 - ratio), area.Right, area.Bottom),
          new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * (1 - ratio))
                  ),
            _ => (area, area)
        };
    }

    private List<SKRect> ComputeTwoVerticalSplit(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        return
        [
            new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Bottom)
        ];
    }

    private List<SKRect> ComputeGrid2x2(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
        return
        [
            new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Top + halfH),
            new SKRect(area.Left, area.Top + halfH + SlotGap, area.Left + halfW, area.Bottom),
            new SKRect(area.Left + halfW + SlotGap, area.Top + halfH + SlotGap, area.Right, area.Bottom)
        ];
    }

    private List<SKRect> ComputeTwoHorizontalStack(SKRect area)
    {
        float halfH = (area.Height - SlotGap) / 2f;
        return
        [
            new SKRect(area.Left, area.Top, area.Right, area.Top + halfH),
            new SKRect(area.Left, area.Top + halfH + SlotGap, area.Right, area.Bottom)
        ];
    }

    private List<SKRect> ComputeThreeLeftStack(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
        return
        [
            new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH),
            new SKRect(area.Left, area.Top + halfH + SlotGap, area.Left + halfW, area.Bottom),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Bottom)
        ];
    }

    private List<SKRect> ComputeThreeRightStack(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
        return
        [
            new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Top + halfH),
            new SKRect(area.Left + halfW + SlotGap, area.Top + halfH + SlotGap, area.Right, area.Bottom)
        ];
    }
}