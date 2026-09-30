namespace CalendarMaker_MAUI.Models;

using System.Text.Json.Serialization;
using CalendarMaker_MAUI.Services;

/// <summary>
/// Represents a calendar creation project with all its configuration settings, assets, and metadata.
/// </summary>
public sealed class CalendarProject
{
    /// <summary>
    /// Gets or sets the unique identifier for this calendar project.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the display name of the calendar project.
    /// </summary>
    public string Name { get; set; } = "New Project";

    /// <summary>
    /// Gets or sets the year for which this calendar is being created.
    /// </summary>
    public int Year { get; set; } = DateTime.Now.Year;

    /// <summary>
    /// Gets or sets the starting month of the calendar. Valid values are 1 through 12, where 1 is January.
    /// </summary>
    public int StartMonth { get; set; } = 1; // 1..12

    /// <summary>
    /// The largest number of month pages a single calendar project may span.
    /// </summary>
    public const int MaxMonthCount = 24;

    private int _monthCount = 12;

    /// <summary>
    /// Gets or sets the number of month pages in the calendar, starting at <see cref="StartMonth"/> of
    /// <see cref="Year"/>. Clamped to 1 through <see cref="MaxMonthCount"/>. Defaults to 12 so projects
    /// saved before custom date ranges existed keep their one-year span.
    /// </summary>
    public int MonthCount
    {
        get => _monthCount;
        set => _monthCount = Math.Clamp(value, 1, MaxMonthCount);
    }

    /// <summary>
    /// Gets the first day of the first month in the calendar.
    /// </summary>
    [JsonIgnore]
    public DateTime StartDate => new(Year, StartMonth, 1);

    /// <summary>
    /// Gets the first day of the last month in the calendar.
    /// </summary>
    [JsonIgnore]
    public DateTime EndDate => StartDate.AddMonths(MonthCount - 1);

    /// <summary>
    /// Gets the designer page index of the back cover, which follows the last month page.
    /// </summary>
    [JsonIgnore]
    public int BackCoverPageIndex => MonthCount;

    /// <summary>
    /// Gets a value indicating whether the date range fits the fixed 12-month double-sided layout.
    /// </summary>
    [JsonIgnore]
    public bool SupportsDoubleSided => MonthCount == 12;

    /// <summary>
    /// Gets a year label for the calendar's date range, e.g. "2026" or "2026-2027".
    /// </summary>
    [JsonIgnore]
    public string YearRangeDisplay => EndDate.Year == Year ? $"{Year}" : $"{Year}-{EndDate.Year}";

    /// <summary>
    /// Maps a 0-based month page index (relative to the start month) to the first day of that month.
    /// </summary>
    /// <param name="monthIndex">The month page index, where 0 is the start month.</param>
    public DateTime GetMonthDate(int monthIndex) => StartDate.AddMonths(monthIndex);

    /// <summary>
    /// Counts the months in an inclusive month range, e.g. October 2026 through December 2027 is 15.
    /// Returns zero or less when the end precedes the start.
    /// </summary>
    public static int CountMonths(int startYear, int startMonth, int endYear, int endMonth)
        => ((endYear - startYear) * 12) + (endMonth - startMonth) + 1;

    /// <summary>
    /// Sets the calendar to span from the start month through the end month, inclusive.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The end precedes the start, or the range spans more than <see cref="MaxMonthCount"/> months.
    /// </exception>
    public void SetDateRange(int startYear, int startMonth, int endYear, int endMonth)
    {
        int count = CountMonths(startYear, startMonth, endYear, endMonth);
        if (count < 1 || count > MaxMonthCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endMonth),
                $"The date range must span 1 to {MaxMonthCount} months, but spans {count}.");
        }

        Year = startYear;
        StartMonth = startMonth;
        MonthCount = count;
    }

    /// <summary>
    /// Gets or sets the first day of the week for calendar display purposes.
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Gets or sets the template key that determines the calendar's design template.
    /// </summary>
    public string TemplateKey { get; set; } = TemplateService.DefaultTemplateKey;

    /// <summary>
    /// Gets or sets the page specifications including size and orientation.
    /// </summary>
    public PageSpec PageSpec { get; set; } = new();

    /// <summary>
    /// Gets or sets the margin specifications for the calendar pages.
    /// </summary>
    public Margins Margins { get; set; } = new();

    /// <summary>
    /// Gets or sets the theme specifications including fonts and colors.
    /// </summary>
    public ThemeSpec Theme { get; set; } = new();

    /// <summary>
    /// Gets or sets the layout specifications that control photo and calendar placement.
    /// </summary>
    public LayoutSpec LayoutSpec { get; set; } = new();

    /// <summary>
    /// Gets or sets the cover specifications for the calendar.
    /// </summary>
    public CoverSpec CoverSpec { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of image assets associated with this project.
    /// </summary>
    public List<ImageAsset> ImageAssets { get; set; } = new();

    /// <summary>
    /// Gets or sets the per-month photo layout overrides, where the key is the month index (0 to MonthCount - 1) relative to StartMonth.
    /// </summary>
    public Dictionary<int, PhotoLayout> MonthPhotoLayouts { get; set; } = new();

    /// <summary>
    /// Gets or sets the user-defined calendar events drawn onto the day cells.
    /// </summary>
    public List<CalendarEvent> Events { get; set; } = new();

    /// <summary>
    /// Gets or sets the photo layout for the front cover.
    /// </summary>
    public PhotoLayout FrontCoverPhotoLayout { get; set; } = PhotoLayout.Single;

    /// <summary>
    /// Gets or sets the photo layout for the back cover.
    /// </summary>
    public PhotoLayout BackCoverPhotoLayout { get; set; } = PhotoLayout.Single;

    /// <summary>
    /// Gets or sets a value indicating whether the calendar should be formatted as double-sided, including previous month's December.
    /// </summary>
    public bool EnableDoubleSided { get; set; } = false;

    /// <summary>
    /// Gets or sets the UTC timestamp when this project was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp when this project was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a human-readable display string for the current page size.
    /// </summary>
    public string PageSizeDisplay => PageSpec.Size switch
    {
        PageSize.FiveBySeven => "5x7",
        PageSize.Letter => "Letter",
        PageSize.Tabloid_11x17 => "11x17",
        PageSize.SuperB_13x19 => "13x19",
        PageSize.Square_12x12 => "12x12",
        _ => PageSpec.Size.ToString()
    };
}