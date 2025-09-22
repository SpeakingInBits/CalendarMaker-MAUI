namespace CalendarMaker_MAUI.Models;

public sealed class ImageAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Role { get; set; } = "monthPhoto"; // or coverPhoto
    public int? MonthIndex { get; set; } // 0..11 for months, null for cover
}
