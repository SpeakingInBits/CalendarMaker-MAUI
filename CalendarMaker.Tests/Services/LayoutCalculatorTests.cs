using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;
using SkiaSharp;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="LayoutCalculator"/>, which computes photo slot rectangles
/// and the photo/calendar split for a page.
/// </summary>
public class LayoutCalculatorTests
{
    private const float Tolerance = 0.001f;
    private readonly LayoutCalculator _calc = new();
    private static readonly SKRect Area = new(0, 0, 400, 300);

    [Theory]
    [InlineData(PhotoLayout.Single, 1)]
    [InlineData(PhotoLayout.TwoVerticalSplit, 2)]
    [InlineData(PhotoLayout.Grid2x2, 4)]
    [InlineData(PhotoLayout.TwoHorizontalStack, 2)]
    [InlineData(PhotoLayout.ThreeLeftStack, 3)]
    [InlineData(PhotoLayout.ThreeRightStack, 3)]
    public void ComputePhotoSlots_ReturnsExpectedSlotCount(PhotoLayout layout, int expectedCount)
    {
        var slots = _calc.ComputePhotoSlots(Area, layout);

        slots.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void ComputePhotoSlots_Single_ReturnsWholeArea()
    {
        var slots = _calc.ComputePhotoSlots(Area, PhotoLayout.Single);

        slots[0].Should().Be(Area);
    }

    [Fact]
    public void ComputePhotoSlots_AllSlotsStayWithinArea()
    {
        foreach (PhotoLayout layout in Enum.GetValues<PhotoLayout>())
        {
            var slots = _calc.ComputePhotoSlots(Area, layout);
            foreach (var slot in slots)
            {
                slot.Left.Should().BeGreaterThanOrEqualTo(Area.Left - Tolerance);
                slot.Top.Should().BeGreaterThanOrEqualTo(Area.Top - Tolerance);
                slot.Right.Should().BeLessThanOrEqualTo(Area.Right + Tolerance);
                slot.Bottom.Should().BeLessThanOrEqualTo(Area.Bottom + Tolerance);
            }
        }
    }

    [Fact]
    public void ComputePhotoSlots_TwoVerticalSplit_LeavesGapBetweenColumns()
    {
        var slots = _calc.ComputePhotoSlots(Area, PhotoLayout.TwoVerticalSplit);

        // The two columns are separated by a 4pt gap.
        (slots[1].Left - slots[0].Right).Should().BeApproximately(4f, Tolerance);
        slots[0].Left.Should().Be(Area.Left);
        slots[1].Right.Should().Be(Area.Right);
    }

    [Fact]
    public void ComputePhotoSlots_TwoHorizontalStack_LeavesGapBetweenRows()
    {
        var slots = _calc.ComputePhotoSlots(Area, PhotoLayout.TwoHorizontalStack);

        (slots[1].Top - slots[0].Bottom).Should().BeApproximately(4f, Tolerance);
        slots[0].Top.Should().Be(Area.Top);
        slots[1].Bottom.Should().Be(Area.Bottom);
    }

    [Fact]
    public void ComputeSplit_Portrait_PhotoTop_PartitionsVertically()
    {
        var spec = new LayoutSpec { Placement = LayoutPlacement.PhotoTopCalendarBottom, SplitRatio = 0.5 };
        var page = new PageSpec { Orientation = PageOrientation.Portrait };

        var (photo, calendar) = _calc.ComputeSplit(Area, spec, page);

        photo.Top.Should().Be(Area.Top);
        photo.Bottom.Should().BeApproximately(Area.Top + Area.Height * 0.5f, Tolerance);
        calendar.Top.Should().BeApproximately(photo.Bottom, Tolerance);
        calendar.Bottom.Should().Be(Area.Bottom);
        // Full page width used by both.
        photo.Width.Should().BeApproximately(Area.Width, Tolerance);
        calendar.Width.Should().BeApproximately(Area.Width, Tolerance);
    }

    [Fact]
    public void ComputeSplit_Landscape_NormalizesVerticalPlacementToHorizontal()
    {
        // PhotoTopCalendarBottom in landscape becomes PhotoLeftCalendarRight.
        var spec = new LayoutSpec { Placement = LayoutPlacement.PhotoTopCalendarBottom, SplitRatio = 0.5 };
        var page = new PageSpec { Orientation = PageOrientation.Landscape };

        var (photo, calendar) = _calc.ComputeSplit(Area, spec, page);

        photo.Left.Should().Be(Area.Left);
        photo.Right.Should().BeApproximately(Area.Left + Area.Width * 0.5f, Tolerance);
        calendar.Left.Should().BeApproximately(photo.Right, Tolerance);
        calendar.Right.Should().Be(Area.Right);
        photo.Height.Should().BeApproximately(Area.Height, Tolerance);
    }

    [Fact]
    public void ComputeSplit_Portrait_NormalizesHorizontalPlacementToVertical()
    {
        // PhotoLeftCalendarRight in portrait becomes PhotoTopCalendarBottom.
        var spec = new LayoutSpec { Placement = LayoutPlacement.PhotoLeftCalendarRight, SplitRatio = 0.5 };
        var page = new PageSpec { Orientation = PageOrientation.Portrait };

        var (photo, calendar) = _calc.ComputeSplit(Area, spec, page);

        photo.Width.Should().BeApproximately(Area.Width, Tolerance);
        photo.Bottom.Should().BeApproximately(Area.Top + Area.Height * 0.5f, Tolerance);
        calendar.Top.Should().BeApproximately(photo.Bottom, Tolerance);
    }

    [Theory]
    [InlineData(0.05, 0.1)] // clamped up to the minimum
    [InlineData(0.95, 0.9)] // clamped down to the maximum
    [InlineData(0.7, 0.7)]  // within range, unchanged
    public void ComputeSplit_ClampsRatioBetweenTenthAndNinetieth(double ratio, double effectiveRatio)
    {
        var spec = new LayoutSpec { Placement = LayoutPlacement.PhotoTopCalendarBottom, SplitRatio = ratio };
        var page = new PageSpec { Orientation = PageOrientation.Portrait };

        var (photo, _) = _calc.ComputeSplit(Area, spec, page);

        photo.Height.Should().BeApproximately(Area.Height * (float)effectiveRatio, Tolerance);
    }
}
