namespace CalendarMaker_MAUI.Models;

public sealed class ImageAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Role { get; set; } = "monthPhoto"; // or coverPhoto
    public int? MonthIndex { get; set; } // 0..11 for months, null for cover

    // Optional index for multi-photo months (0..n-1), null or 0 means first slot
    public int? SlotIndex { get; set; }

    // Normalized pan offsets (-1..1). 0 = centered.
    public double PanX { get; set; } = 0;
    public double PanY { get; set; } = 0;

    // Zoom multiplier (0.5..3). 1 = fit cover baseline
    public double Zoom { get; set; } = 1.0;

    // Optional display order for month galleries
    public int Order { get; set; } = 0;
}
