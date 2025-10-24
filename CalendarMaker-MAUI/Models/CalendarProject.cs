namespace CalendarMaker_MAUI.Models;

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
    /// Gets or sets the per-month photo layout overrides, where the key is the month index (0-11) relative to StartMonth.
    /// </summary>
    public Dictionary<int, PhotoLayout> MonthPhotoLayouts { get; set; } = new();
    
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
        _ => PageSpec.Size.ToString()
    };
}
