using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Additional edge-case coverage for <see cref="CalendarEvent.OccursOn"/> beyond the
/// happy-path recurrence tests.
/// </summary>
public class CalendarEventRecurrenceEdgeCaseTests
{
    [Fact]
    public void Annually_LeapDay_MatchesOnlyInLeapYears()
    {
        var leapBirthday = new CalendarEvent { Recurrence = EventRecurrence.Annually, Month = 2, Day = 29 };

        leapBirthday.OccursOn(new DateTime(2024, 2, 29)).Should().BeTrue();  // leap year
        leapBirthday.OccursOn(new DateTime(2025, 2, 28)).Should().BeFalse(); // non-leap, no Feb 29
        leapBirthday.OccursOn(new DateTime(2028, 2, 29)).Should().BeTrue();  // next leap year
    }

    [Fact]
    public void Weekly_MatchesAcrossMonthAndYearBoundaries()
    {
        var ev = new CalendarEvent { Recurrence = EventRecurrence.Weekly, Weekday = DayOfWeek.Wednesday };

        ev.OccursOn(new DateTime(2025, 12, 31)).Should().BeTrue(); // Wednesday
        ev.OccursOn(new DateTime(2026, 1, 7)).Should().BeTrue();   // Wednesday, next year
        ev.OccursOn(new DateTime(2026, 1, 8)).Should().BeFalse();  // Thursday
    }

    [Fact]
    public void MonthlyByWeekday_FirstOccurrence_MatchesFirstWeekdayOfMonth()
    {
        // First Monday of September (US Labor Day).
        var laborDay = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 9,
            Weekday = DayOfWeek.Monday,
            WeekOfMonth = 1
        };

        laborDay.OccursOn(new DateTime(2025, 9, 1)).Should().BeTrue();  // 1st Monday 2025
        laborDay.OccursOn(new DateTime(2025, 9, 8)).Should().BeFalse(); // 2nd Monday
        laborDay.OccursOn(new DateTime(2026, 9, 7)).Should().BeTrue();  // 1st Monday 2026
    }

    [Fact]
    public void MonthlyByWeekday_FourthAndLast_DistinguishedWhenMonthHasFiveWeekdays()
    {
        // October 2025 has five Fridays (3, 10, 17, 24, 31).
        var fourthFriday = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 10,
            Weekday = DayOfWeek.Friday,
            WeekOfMonth = 4
        };
        var lastFriday = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 10,
            Weekday = DayOfWeek.Friday,
            WeekOfMonth = 5 // "last"
        };

        fourthFriday.OccursOn(new DateTime(2025, 10, 24)).Should().BeTrue();
        fourthFriday.OccursOn(new DateTime(2025, 10, 31)).Should().BeFalse();

        lastFriday.OccursOn(new DateTime(2025, 10, 31)).Should().BeTrue();
        lastFriday.OccursOn(new DateTime(2025, 10, 24)).Should().BeFalse();
    }

    [Fact]
    public void MonthlyByWeekday_WrongMonth_DoesNotMatch()
    {
        var ev = new CalendarEvent
        {
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 11,
            Weekday = DayOfWeek.Thursday,
            WeekOfMonth = 4
        };

        // 4th Thursday exists in other months too, but the rule is pinned to November.
        ev.OccursOn(new DateTime(2025, 10, 23)).Should().BeFalse();
    }
}
