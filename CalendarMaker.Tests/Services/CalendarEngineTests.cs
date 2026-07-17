using CalendarMaker_MAUI.Services;
using FluentAssertions;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="CalendarEngine"/>, which builds the week/day matrix for a month.
/// </summary>
public class CalendarEngineTests
{
    private readonly CalendarEngine _engine = new();

    [Fact]
    public void BuildMonthGrid_EveryWeekHasSevenDays()
    {
        var weeks = _engine.BuildMonthGrid(2025, 1, DayOfWeek.Sunday);

        weeks.Should().OnlyContain(week => week.Count == 7);
    }

    [Fact]
    public void BuildMonthGrid_ContainsEveryDayOfMonthExactlyOnce()
    {
        var weeks = _engine.BuildMonthGrid(2025, 1, DayOfWeek.Sunday);

        var days = weeks.SelectMany(w => w)
            .Where(d => d.HasValue && d.Value.Month == 1)
            .Select(d => d!.Value.Day)
            .ToList();

        days.Should().BeEquivalentTo(Enumerable.Range(1, 31));
    }

    [Fact]
    public void BuildMonthGrid_January2025_SundayStart_HasThreeLeadingNulls()
    {
        // Jan 1 2025 falls on a Wednesday; with a Sunday start that is offset 3.
        var weeks = _engine.BuildMonthGrid(2025, 1, DayOfWeek.Sunday);

        var firstWeek = weeks[0];
        firstWeek[0].Should().BeNull();
        firstWeek[1].Should().BeNull();
        firstWeek[2].Should().BeNull();
        firstWeek[3].Should().Be(new DateTime(2025, 1, 1));
        firstWeek[6].Should().Be(new DateTime(2025, 1, 4));
    }

    [Fact]
    public void BuildMonthGrid_January2025_MondayStart_ShiftsLeadingNulls()
    {
        // Jan 1 2025 is a Wednesday; with a Monday start that is offset 2.
        var weeks = _engine.BuildMonthGrid(2025, 1, DayOfWeek.Monday);

        weeks[0][0].Should().BeNull();
        weeks[0][1].Should().BeNull();
        weeks[0][2].Should().Be(new DateTime(2025, 1, 1));
    }

    [Fact]
    public void BuildMonthGrid_LeapYearFebruary_Includes29Days()
    {
        var weeks = _engine.BuildMonthGrid(2024, 2, DayOfWeek.Sunday);

        var maxDay = weeks.SelectMany(w => w)
            .Where(d => d.HasValue && d.Value.Month == 2)
            .Max(d => d!.Value.Day);

        maxDay.Should().Be(29);
    }

    [Fact]
    public void BuildMonthGrid_NonLeapYearFebruary_Includes28Days()
    {
        var weeks = _engine.BuildMonthGrid(2025, 2, DayOfWeek.Sunday);

        var maxDay = weeks.SelectMany(w => w)
            .Where(d => d.HasValue && d.Value.Month == 2)
            .Max(d => d!.Value.Day);

        maxDay.Should().Be(28);
    }

    [Theory]
    [InlineData(2025, 1)]
    [InlineData(2025, 2)]
    [InlineData(2024, 2)]
    [InlineData(2025, 12)]
    [InlineData(2026, 3)]
    public void BuildMonthGrid_ProducesBetweenFourAndSixWeeks(int year, int month)
    {
        var weeks = _engine.BuildMonthGrid(year, month, DayOfWeek.Sunday);

        weeks.Count.Should().BeInRange(4, 6);
    }

    [Fact]
    public void BuildMonthGrid_FirstWeekOfMonthWithNoOffset_StartsOnDayOne()
    {
        // June 1 2025 is a Sunday, so with a Sunday start there is no leading padding.
        var weeks = _engine.BuildMonthGrid(2025, 6, DayOfWeek.Sunday);

        weeks[0][0].Should().Be(new DateTime(2025, 6, 1));
    }
}
