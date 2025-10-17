namespace CalendarMaker_MAUI.Models;

public sealed class CalendarProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Project";
    public int Year { get; set; } = DateTime.Now.Year;
    public int StartMonth { get; set; } = 1; // 1..12
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    public string TemplateKey { get; set; } = "PhotoMonthlyClassic";

    public PageSpec PageSpec { get; set; } = new();
    public Margins Margins { get; set; } = new();
    public ThemeSpec Theme { get; set; } = new();
    public LayoutSpec LayoutSpec { get; set; } = new();
    public CoverSpec CoverSpec { get; set; } = new();

    public List<ImageAsset> ImageAssets { get; set; } = new();

    // Optional per-month photo layout override (0..11 month index relative to StartMonth)
    public Dictionary<int, PhotoLayout> MonthPhotoLayouts { get; set; } = new();
    
    // Photo layouts for covers
    public PhotoLayout FrontCoverPhotoLayout { get; set; } = PhotoLayout.Single;
    public PhotoLayout BackCoverPhotoLayout { get; set; } = PhotoLayout.Single;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    // Computed property for display
    public string PageSizeDisplay => PageSpec.Size switch
    {
        PageSize.FiveBySeven => "5x7",
        PageSize.Letter => "Letter",
        PageSize.Tabloid_11x17 => "11x17",
        PageSize.SuperB_13x19 => "13x19",
        _ => PageSpec.Size.ToString()
    };
}
