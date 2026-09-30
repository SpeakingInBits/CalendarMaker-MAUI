using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Tests for computed members on <see cref="CalendarProject"/>.
/// </summary>
public class CalendarProjectTests
{
    private static CalendarProject WithSize(PageSize size) => new()
    {
        PageSpec = new PageSpec { Size = size }
    };

    [Theory]
    [InlineData(PageSize.FiveBySeven, "5x7")]
    [InlineData(PageSize.Letter, "Letter")]
    [InlineData(PageSize.Tabloid_11x17, "11x17")]
    [InlineData(PageSize.SuperB_13x19, "13x19")]
    public void PageSizeDisplay_KnownSizes_ReturnFriendlyLabels(PageSize size, string expected)
    {
        WithSize(size).PageSizeDisplay.Should().Be(expected);
    }

    [Fact]
    public void PageSizeDisplay_UnmappedSize_FallsBackToEnumName()
    {
        // A4 has no explicit label and should fall back to the enum name.
        WithSize(PageSize.A4).PageSizeDisplay.Should().Be("A4");
    }

    [Fact]
    public void NewProject_HasInitializedCollectionsAndDefaults()
    {
        var project = new CalendarProject();

        project.ImageAssets.Should().NotBeNull().And.BeEmpty();
        project.MonthPhotoLayouts.Should().NotBeNull().And.BeEmpty();
        project.PageSpec.Should().NotBeNull();
        project.LayoutSpec.Should().NotBeNull();
        project.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NewProject_SpansTwelveMonths()
    {
        var project = new CalendarProject { Year = 2026, StartMonth = 1 };

        project.MonthCount.Should().Be(12);
        project.EndDate.Should().Be(new DateTime(2026, 12, 1));
        project.BackCoverPageIndex.Should().Be(12);
        project.SupportsDoubleSided.Should().BeTrue();
        project.YearRangeDisplay.Should().Be("2026");
    }

    [Theory]
    [InlineData(2026, 10, 2027, 12, 15)]
    [InlineData(2026, 1, 2026, 12, 12)]
    [InlineData(2026, 5, 2026, 5, 1)]
    [InlineData(2026, 12, 2027, 1, 2)]
    [InlineData(2026, 6, 2026, 5, 0)]
    public void CountMonths_IsInclusiveOfBothEnds(int startYear, int startMonth, int endYear, int endMonth, int expected)
    {
        CalendarProject.CountMonths(startYear, startMonth, endYear, endMonth).Should().Be(expected);
    }

    [Fact]
    public void SetDateRange_OctoberThroughNextDecember_SpansFifteenMonths()
    {
        var project = new CalendarProject();

        project.SetDateRange(2026, 10, 2027, 12);

        project.Year.Should().Be(2026);
        project.StartMonth.Should().Be(10);
        project.MonthCount.Should().Be(15);
        project.StartDate.Should().Be(new DateTime(2026, 10, 1));
        project.EndDate.Should().Be(new DateTime(2027, 12, 1));
        project.BackCoverPageIndex.Should().Be(15);
        project.SupportsDoubleSided.Should().BeFalse();
        project.YearRangeDisplay.Should().Be("2026-2027");
    }

    [Theory]
    [InlineData(0, 2026, 10)]
    [InlineData(2, 2026, 12)]
    [InlineData(3, 2027, 1)]
    [InlineData(14, 2027, 12)]
    public void GetMonthDate_MapsPageIndexAcrossYearBoundaries(int monthIndex, int expectedYear, int expectedMonth)
    {
        var project = new CalendarProject();
        project.SetDateRange(2026, 10, 2027, 12);

        project.GetMonthDate(monthIndex).Should().Be(new DateTime(expectedYear, expectedMonth, 1));
    }

    [Fact]
    public void SetDateRange_EndBeforeStart_ThrowsAndLeavesProjectUnchanged()
    {
        var project = new CalendarProject { Year = 2026, StartMonth = 1 };

        var act = () => project.SetDateRange(2027, 3, 2027, 2);

        act.Should().Throw<ArgumentOutOfRangeException>();
        project.Year.Should().Be(2026);
        project.StartMonth.Should().Be(1);
        project.MonthCount.Should().Be(12);
    }

    [Fact]
    public void SetDateRange_LongerThanMaximum_Throws()
    {
        var project = new CalendarProject();

        var act = () => project.SetDateRange(2026, 1, 2028, 1); // 25 months

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100, CalendarProject.MaxMonthCount)]
    public void MonthCount_IsClampedToSupportedRange(int value, int expected)
    {
        var project = new CalendarProject { MonthCount = value };

        project.MonthCount.Should().Be(expected);
    }

    [Fact]
    public void Deserialize_ProjectSavedBeforeCustomDateRanges_DefaultsToTwelveMonths()
    {
        const string legacyJson = """{"Id":"abc","Name":"Old","Year":2025,"StartMonth":3}""";

        var project = System.Text.Json.JsonSerializer.Deserialize<CalendarProject>(legacyJson)!;

        project.MonthCount.Should().Be(12);
        project.EndDate.Should().Be(new DateTime(2026, 2, 1));
    }

    [Fact]
    public void Serialize_RoundTripsMonthCountWithoutComputedDates()
    {
        var project = new CalendarProject();
        project.SetDateRange(2026, 10, 2027, 12);

        string json = System.Text.Json.JsonSerializer.Serialize(project);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CalendarProject>(json)!;

        json.Should().NotContain("\"EndDate\"").And.NotContain("\"BackCoverPageIndex\"");
        loaded.MonthCount.Should().Be(15);
        loaded.EndDate.Should().Be(new DateTime(2027, 12, 1));
    }
}
