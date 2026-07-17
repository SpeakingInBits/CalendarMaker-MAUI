using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Tests for <see cref="CalendarEvent.OccursOn"/> across every recurrence rule.
/// </summary>
public class CalendarEventTests
{
    [Fact]
    public void None_MatchesOnlyTheExactDate()
    {
        var ev = new CalendarEvent { Recurrence = EventRecurrence.None, Year = 2025, Month = 7, Day = 4 };

        ev.OccursOn(new DateTime(2025, 7, 4)).Should().BeTrue();
        ev.OccursOn(new DateTime(2026, 7, 4)).Should().BeFalse(); // different year
        ev.OccursOn(new DateTime(2025, 7, 5)).Should().BeFalse(); // different day
    }

    [Fact]
    public void Annually_MatchesSameMonthAndDayEveryYear()
    {
        var birthday = new CalendarEvent { Recurrence = EventRecurrence.Annually, Month = 3, Day = 15 };

        birthday.OccursOn(new DateTime(2025, 3, 15)).Should().BeTrue();
        birthday.OccursOn(new DateTime(2031, 3, 15)).Should().BeTrue();
        birthday.OccursOn(new DateTime(2025, 3, 16)).Should().BeFalse();
    }

    [Fact]
    public void Weekly_MatchesEveryOccurrenceOfTheWeekday()
    {
        var ev = new CalendarEvent { Recurrence = EventRecurrence.Weekly, Weekday = DayOfWeek.Monday };

        ev.OccursOn(new DateTime(2025, 6, 2)).Should().BeTrue();  // Monday
        ev.OccursOn(new DateTime(2025, 6, 9)).Should().BeTrue();  // next Monday
        ev.OccursOn(new DateTime(2025, 6, 3)).Should().BeFalse(); // Tuesday
    }

    [Fact]
    public void MonthlyByWeekday_MatchesNthWeekdayAcrossYears()
    {
        // Third Thursday of October (moves with the year).
        var ev = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 10,
            Weekday = DayOfWeek.Thursday,
            WeekOfMonth = 3
        };

        ev.OccursOn(new DateTime(2025, 10, 16)).Should().BeTrue();  // 3rd Thursday 2025
        ev.OccursOn(new DateTime(2026, 10, 15)).Should().BeTrue();  // 3rd Thursday 2026
        ev.OccursOn(new DateTime(2026, 10, 16)).Should().BeFalse(); // Friday
        ev.OccursOn(new DateTime(2025, 10, 9)).Should().BeFalse();  // 2nd Thursday
        ev.OccursOn(new DateTime(2025, 11, 20)).Should().BeFalse(); // right weekday/occurrence, wrong month
    }

    [Fact]
    public void MonthlyByWeekday_LastOccurrence_MatchesFinalWeekdayOfMonth()
    {
        // Last Monday of May (e.g. Memorial Day). WeekOfMonth 5 means "last".
        var ev = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 5,
            Weekday = DayOfWeek.Monday,
            WeekOfMonth = 5
        };

        ev.OccursOn(new DateTime(2025, 5, 26)).Should().BeTrue();  // last Monday 2025
        ev.OccursOn(new DateTime(2025, 5, 19)).Should().BeFalse(); // an earlier Monday
    }
}
