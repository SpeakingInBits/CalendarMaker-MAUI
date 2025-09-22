namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;

public interface ICalendarEngine
{
    // Returns a matrix of weeks -> days for a given month
    List<List<DateTime?>> BuildMonthGrid(int year, int month, DayOfWeek firstDayOfWeek);
}

public sealed class CalendarEngine : ICalendarEngine
{
    public List<List<DateTime?>> BuildMonthGrid(int year, int month, DayOfWeek firstDayOfWeek)
    {
        var firstOfMonth = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var weeks = new List<List<DateTime?>>();

        // Calculate the index of the first day in the week based on firstDayOfWeek
        int dayOffset = ((int)firstOfMonth.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        int currentDay = 1 - dayOffset;

        // Up to 6 weeks rows
        for (int w = 0; w < 6; w++)
        {
            var week = new List<DateTime?>(7);
            for (int d = 0; d < 7; d++)
            {
                if (currentDay >= 1 && currentDay <= daysInMonth)
                    week.Add(new DateTime(year, month, currentDay));
                else
                    week.Add(null);
                currentDay++;
            }
            weeks.Add(week);

            // Stop if the last week contains only next-month days
            if (currentDay > daysInMonth && week.All(x => x == null || x.Value.Month != month))
                break;
        }

        return weeks;
    }
}
