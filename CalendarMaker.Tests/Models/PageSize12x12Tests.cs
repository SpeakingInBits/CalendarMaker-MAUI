using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Tests covering the 12x12 square page size (issue #54).
/// </summary>
public class PageSize12x12Tests
{
    [Fact]
    public void GetInches_Square12x12_ReturnsTwelveByTwelve()
    {
        var (w, h) = PageSizes.GetInches(PageSize.Square_12x12);

        w.Should().Be(12.0);
        h.Should().Be(12.0);
    }

    [Fact]
    public void GetPoints_Square12x12_Portrait_Returns864Points()
    {
        var spec = new PageSpec { Size = PageSize.Square_12x12, Orientation = PageOrientation.Portrait };

        var (w, h) = PageSizes.GetPoints(spec);

        w.Should().BeApproximately(12.0 * 72.0, 0.001); // 864
        h.Should().BeApproximately(12.0 * 72.0, 0.001); // 864
    }

    [Fact]
    public void GetPoints_Square12x12_IsSquareRegardlessOfOrientation()
    {
        var portrait = new PageSpec { Size = PageSize.Square_12x12, Orientation = PageOrientation.Portrait };
        var landscape = new PageSpec { Size = PageSize.Square_12x12, Orientation = PageOrientation.Landscape };

        var (pw, ph) = PageSizes.GetPoints(portrait);
        var (lw, lh) = PageSizes.GetPoints(landscape);

        // A square page has identical dimensions, so swapping for landscape is a no-op.
        pw.Should().Be(ph);
        lw.Should().Be(pw);
        lh.Should().Be(ph);
    }

    [Fact]
    public void PageSizeDisplay_Square12x12_Returns12x12()
    {
        var project = new CalendarProject
        {
            PageSpec = new PageSpec { Size = PageSize.Square_12x12 }
        };

        project.PageSizeDisplay.Should().Be("12x12");
    }

    [Fact]
    public void Square12x12_IsDeclaredLast_SoExistingEnumValuesAreUnchanged()
    {
        // Saved projects serialize the enum as an integer, so the pre-existing members must keep
        // their original values and the new one must sort after Custom.
        ((int)PageSize.FiveBySeven).Should().Be(0);
        ((int)PageSize.A4).Should().Be(1);
        ((int)PageSize.Letter).Should().Be(2);
        ((int)PageSize.Tabloid_11x17).Should().Be(3);
        ((int)PageSize.SuperB_13x19).Should().Be(4);
        ((int)PageSize.Custom).Should().Be(5);
        ((int)PageSize.Square_12x12).Should().BeGreaterThan((int)PageSize.Custom);
    }
}
