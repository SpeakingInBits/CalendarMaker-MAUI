using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="CalendarEventService"/> resolution and event-building helpers.
/// </summary>
public class CalendarEventServiceTests
{
    [Fact]
    public void GetEventsForDate_ReturnsOnlyMatchingEvents()
    {
        var events = new List<CalendarEvent>
        {
            new() { Title = "July 4th", Recurrence = EventRecurrence.Annually, Month = 7, Day = 4 },
            new() { Title = "Payday", Recurrence = EventRecurrence.Weekly, Weekday = DayOfWeek.Friday },
            new() { Title = "One-off", Recurrence = EventRecurrence.None, Year = 2025, Month = 7, Day = 4 }
        };

        // 2025-07-04 is a Friday, so all three match.
        var onJuly4 = CalendarEventService.GetEventsForDate(events, new DateTime(2025, 7, 4));
        onJuly4.Should().HaveCount(3);

        // 2025-07-11 is a Friday but not July 4th, so only the weekly event matches.
        var onJuly11 = CalendarEventService.GetEventsForDate(events, new DateTime(2025, 7, 11));
        onJuly11.Select(e => e.Title).Should().ContainSingle().Which.Should().Be("Payday");
    }

    [Fact]
    public void GetEventsForDate_OrdersByTitle()
    {
        var events = new List<CalendarEvent>
        {
            new() { Title = "Zebra", Recurrence = EventRecurrence.Annually, Month = 1, Day = 1 },
            new() { Title = "Apple", Recurrence = EventRecurrence.Annually, Month = 1, Day = 1 }
        };

        var result = CalendarEventService.GetEventsForDate(events, new DateTime(2025, 1, 1));

        result.Select(e => e.Title).Should().ContainInOrder("Apple", "Zebra");
    }

    [Fact]
    public void GetEventsForDate_NullSource_ReturnsEmpty()
    {
        CalendarEventService.GetEventsForDate(null, new DateTime(2025, 1, 1)).Should().BeEmpty();
    }

    [Fact]
    public void WeekOfMonthFor_ReturnsCorrectOccurrence()
    {
        CalendarEventService.WeekOfMonthFor(new DateTime(2025, 10, 16)).Should().Be(3); // 3rd Thursday
        CalendarEventService.WeekOfMonthFor(new DateTime(2025, 10, 1)).Should().Be(1);
        CalendarEventService.WeekOfMonthFor(new DateTime(2025, 10, 8)).Should().Be(2);
    }

    [Fact]
    public void CreateForDate_PopulatesAnchorFieldsFromDate()
    {
        var date = new DateTime(2025, 10, 16); // a Thursday, 3rd of the month

        var ev = CalendarEventService.CreateForDate(date, "Book club", "📚", "#59A14F", EventRecurrence.MonthlyByWeekday);

        ev.Title.Should().Be("Book club");
        ev.Emoji.Should().Be("📚");
        ev.ColorHex.Should().Be("#59A14F");
        ev.Recurrence.Should().Be(EventRecurrence.MonthlyByWeekday);
        ev.Year.Should().Be(2025);
        ev.Month.Should().Be(10);
        ev.Day.Should().Be(16);
        ev.Weekday.Should().Be(DayOfWeek.Thursday);
        ev.WeekOfMonth.Should().Be(3);

        // The created event should resolve to its own anchor date.
        ev.OccursOn(date).Should().BeTrue();
    }

    [Fact]
    public void CreateForDate_BlankEmoji_StoredAsNull()
    {
        var ev = CalendarEventService.CreateForDate(new DateTime(2025, 1, 1), "New Year", "   ", "#4E79A7", EventRecurrence.Annually);

        ev.Emoji.Should().BeNull();
    }

    [Fact]
    public void Palette_IsNonEmpty()
    {
        CalendarEventService.Palette.Should().NotBeEmpty();
        CalendarEventService.Palette.Should().OnlyContain(c => c.StartsWith("#"));
    }
}
