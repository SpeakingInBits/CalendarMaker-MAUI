using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Pure helpers for resolving which <see cref="CalendarEvent"/>s fall on a given date and for
/// building new events anchored to a clicked date. Kept free of UI/rendering concerns so it can
/// be unit tested in isolation.
/// </summary>
public static class CalendarEventService
{
    /// <summary>
    /// A small, print-friendly palette users can pick event colors from.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette = new[]
    {
        "#4E79A7", // blue
        "#E15759", // red
        "#59A14F", // green
        "#F28E2B", // orange
        "#B07AA1", // purple
        "#EDC948", // yellow
        "#76B7B2", // teal
        "#FF9DA7"  // pink
    };

    /// <summary>
    /// Returns the events that occur on <paramref name="date"/>, ordered for stable rendering.
    /// </summary>
    /// <param name="events">The candidate events (typically <c>project.Events</c>).</param>
    /// <param name="date">The date to resolve.</param>
    public static IReadOnlyList<CalendarEvent> GetEventsForDate(IEnumerable<CalendarEvent>? events, DateTime date)
    {
        if (events is null)
        {
            return Array.Empty<CalendarEvent>();
        }

        return events
            .Where(e => e.OccursOn(date))
            .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Creates a new event anchored to <paramref name="date"/>, interpreting the anchor according to
    /// the requested <paramref name="recurrence"/>. All anchor fields are populated from the date so
    /// switching the recurrence later still resolves correctly.
    /// </summary>
    public static CalendarEvent CreateForDate(
        DateTime date,
        string title,
        string? emoji,
        string colorHex,
        EventRecurrence recurrence)
    {
        return new CalendarEvent
        {
            Title = title,
            Emoji = string.IsNullOrWhiteSpace(emoji) ? null : emoji,
            ColorHex = colorHex,
            Recurrence = recurrence,
            Year = date.Year,
            Month = date.Month,
            Day = date.Day,
            Weekday = date.DayOfWeek,
            WeekOfMonth = WeekOfMonthFor(date)
        };
    }

    /// <summary>
    /// Returns which occurrence (1-based) of its own weekday the given date is within its month.
    /// For example, 2025-10-16 (a Thursday) returns 3 because it is the third Thursday of October.
    /// </summary>
    public static int WeekOfMonthFor(DateTime date) => (date.Day - 1) / 7 + 1;
}
