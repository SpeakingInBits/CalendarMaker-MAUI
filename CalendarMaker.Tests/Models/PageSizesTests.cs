using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Tests for <see cref="PageSizes"/> dimension lookups and point conversion.
/// </summary>
public class PageSizesTests
{
    [Theory]
    [InlineData(PageSize.FiveBySeven, 5.0, 7.0)]
    [InlineData(PageSize.A4, 8.27, 11.69)]
    [InlineData(PageSize.Letter, 8.5, 11.0)]
    [InlineData(PageSize.Tabloid_11x17, 11.0, 17.0)]
    [InlineData(PageSize.SuperB_13x19, 13.0, 19.0)]
    [InlineData(PageSize.Custom, 0.0, 0.0)]
    public void GetInches_ReturnsExpectedDimensions(PageSize size, double expectedW, double expectedH)
    {
        var (w, h) = PageSizes.GetInches(size);

        w.Should().Be(expectedW);
        h.Should().Be(expectedH);
    }

    [Fact]
    public void GetPoints_LetterPortrait_ConvertsInchesToPoints()
    {
        var spec = new PageSpec { Size = PageSize.Letter, Orientation = PageOrientation.Portrait };

        var (w, h) = PageSizes.GetPoints(spec);

        w.Should().BeApproximately(8.5 * 72.0, 0.001);  // 612
        h.Should().BeApproximately(11.0 * 72.0, 0.001);  // 792
    }

    [Fact]
    public void GetPoints_Landscape_SwapsWidthAndHeight()
    {
        var portrait = new PageSpec { Size = PageSize.Letter, Orientation = PageOrientation.Portrait };
        var landscape = new PageSpec { Size = PageSize.Letter, Orientation = PageOrientation.Landscape };

        var (pw, ph) = PageSizes.GetPoints(portrait);
        var (lw, lh) = PageSizes.GetPoints(landscape);

        lw.Should().BeApproximately(ph, 0.001);
        lh.Should().BeApproximately(pw, 0.001);
    }

    [Fact]
    public void GetPoints_CustomWithDimensions_UsesCustomValues()
    {
        var spec = new PageSpec
        {
            Size = PageSize.Custom,
            CustomWidthPt = 500,
            CustomHeightPt = 650
        };

        var (w, h) = PageSizes.GetPoints(spec);

        w.Should().Be(500);
        h.Should().Be(650);
    }

    [Fact]
    public void GetPoints_CustomWithDimensions_IgnoresOrientationSwap()
    {
        // Custom dimensions are taken verbatim and not swapped for landscape.
        var spec = new PageSpec
        {
            Size = PageSize.Custom,
            CustomWidthPt = 500,
            CustomHeightPt = 650,
            Orientation = PageOrientation.Landscape
        };

        var (w, h) = PageSizes.GetPoints(spec);

        w.Should().Be(500);
        h.Should().Be(650);
    }
}
