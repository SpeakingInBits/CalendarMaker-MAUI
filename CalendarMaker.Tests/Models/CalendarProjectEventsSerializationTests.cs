using System.Text.Json;
using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Events are persisted as part of the project's JSON (see ProjectStorageService). These tests
/// lock in that they round-trip correctly and that projects saved before events existed still load.
/// </summary>
public class CalendarProjectEventsSerializationTests
{
    [Fact]
    public void Events_RoundTripThroughJson_PreserveAllFields()
    {
        var project = new CalendarProject { Id = "p1", Name = "Test", Year = 2025 };
        project.Events.Add(new CalendarEvent
        {
            Id = "e1",
            Title = "Mom's Birthday",
            Emoji = "🎂",
            ColorHex = "#E15759",
            Recurrence = EventRecurrence.Annually,
            Month = 4,
            Day = 12
        });
        project.Events.Add(new CalendarEvent
        {
            Id = "e2",
            Title = "Thanksgiving",
            Emoji = "🦃",
            ColorHex = "#F28E2B",
            Recurrence = EventRecurrence.MonthlyByWeekday,
            Month = 11,
            Weekday = DayOfWeek.Thursday,
            WeekOfMonth = 4
        });

        string json = JsonSerializer.Serialize(project);
        var restored = JsonSerializer.Deserialize<CalendarProject>(json);

        restored.Should().NotBeNull();
        restored!.Events.Should().HaveCount(2);
        restored.Events.Should().BeEquivalentTo(project.Events);
    }

    [Fact]
    public void Deserialize_ProjectJsonWithoutEvents_YieldsEmptyEventList()
    {
        // A project saved before the Events field existed has no "Events" property.
        const string legacyJson = """{ "Id": "old", "Name": "Legacy", "Year": 2024 }""";

        var restored = JsonSerializer.Deserialize<CalendarProject>(legacyJson);

        restored.Should().NotBeNull();
        restored!.Events.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Recurrence_SerializesAsInteger_AndSurvivesRoundTrip()
    {
        var ev = new CalendarEvent { Recurrence = EventRecurrence.Weekly, Weekday = DayOfWeek.Friday };

        string json = JsonSerializer.Serialize(ev);
        var restored = JsonSerializer.Deserialize<CalendarEvent>(json);

        restored!.Recurrence.Should().Be(EventRecurrence.Weekly);
        restored.Weekday.Should().Be(DayOfWeek.Friday);
    }
}
