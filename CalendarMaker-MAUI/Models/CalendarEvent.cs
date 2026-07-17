namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Describes how a <see cref="CalendarEvent"/> repeats across the calendar.
/// </summary>
public enum EventRecurrence
{
    /// <summary>
    /// Occurs once, on a single specific date (<see cref="CalendarEvent.Year"/>/<see cref="CalendarEvent.Month"/>/<see cref="CalendarEvent.Day"/>).
    /// </summary>
    None,

    /// <summary>
    /// Occurs every year on the same month and day (e.g. birthdays, anniversaries).
    /// </summary>
    Annually,

    /// <summary>
    /// Occurs every week on a given <see cref="CalendarEvent.Weekday"/>.
    /// </summary>
    Weekly,

    /// <summary>
    /// Occurs on the Nth (<see cref="CalendarEvent.WeekOfMonth"/>) <see cref="CalendarEvent.Weekday"/> of
    /// <see cref="CalendarEvent.Month"/> every year (e.g. "4th Thursday of November"). This keeps
    /// weekday-anchored holidays on the correct day as the calendar year changes.
    /// </summary>
    MonthlyByWeekday
}

/// <summary>
/// A user-defined calendar event that is drawn onto the day cells of a calendar grid.
/// </summary>
public sealed class CalendarEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this event.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the event title displayed on the day cell.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional emoji or short symbol used to draw attention to the event.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Gets or sets the accent color of the event as a hex string (e.g. "#4E79A7").
    /// </summary>
    public string ColorHex { get; set; } = "#4E79A7";

    /// <summary>
    /// Gets or sets how the event repeats.
    /// </summary>
    public EventRecurrence Recurrence { get; set; } = EventRecurrence.None;

    /// <summary>
    /// Gets or sets the anchor year. Only used when <see cref="Recurrence"/> is <see cref="EventRecurrence.None"/>.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the anchor month (1-12). Used by <see cref="EventRecurrence.None"/>,
    /// <see cref="EventRecurrence.Annually"/>, and <see cref="EventRecurrence.MonthlyByWeekday"/>.
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Gets or sets the anchor day of month (1-31). Used by <see cref="EventRecurrence.None"/> and
    /// <see cref="EventRecurrence.Annually"/>.
    /// </summary>
    public int Day { get; set; }

    /// <summary>
    /// Gets or sets the anchor weekday. Used by <see cref="EventRecurrence.Weekly"/> and
    /// <see cref="EventRecurrence.MonthlyByWeekday"/>.
    /// </summary>
    public DayOfWeek Weekday { get; set; }

    /// <summary>
    /// Gets or sets which occurrence of <see cref="Weekday"/> within the month the event falls on
    /// (1-4, or 5 for "last"). Only used by <see cref="EventRecurrence.MonthlyByWeekday"/>.
    /// </summary>
    public int WeekOfMonth { get; set; } = 1;

    /// <summary>
    /// Determines whether this event occurs on the specified date.
    /// </summary>
    /// <param name="date">The date to test.</param>
    /// <returns><c>true</c> if the event occurs on <paramref name="date"/>; otherwise <c>false</c>.</returns>
    public bool OccursOn(DateTime date) => Recurrence switch
    {
        EventRecurrence.None => date.Year == Year && date.Month == Month && date.Day == Day,
        EventRecurrence.Annually => date.Month == Month && date.Day == Day,
        EventRecurrence.Weekly => date.DayOfWeek == Weekday,
        EventRecurrence.MonthlyByWeekday => date.Month == Month
            && date.DayOfWeek == Weekday
            && MatchesWeekOfMonth(date),
        _ => false
    };

    private bool MatchesWeekOfMonth(DateTime date)
    {
        // Which occurrence of this weekday within the month is `date`? (1-based)
        int occurrence = (date.Day - 1) / 7 + 1;

        if (WeekOfMonth >= 5)
        {
            // "Last" occurrence: there is no same weekday 7 days later within this month.
            return date.AddDays(7).Month != date.Month;
        }

        return occurrence == WeekOfMonth;
    }
}
