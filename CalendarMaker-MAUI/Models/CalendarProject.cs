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

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
