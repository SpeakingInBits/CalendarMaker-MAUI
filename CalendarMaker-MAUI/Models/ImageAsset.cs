namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Represents an image asset used in a calendar project, including its location, role, and display properties.
/// </summary>
public sealed class ImageAsset
{
    /// <summary>
    /// Gets or sets the unique identifier for this image asset.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the identifier of the project this image asset belongs to.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path to the image.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the image in the calendar. Valid values are "monthPhoto" or "coverPhoto".
    /// </summary>
    public string Role { get; set; } = "monthPhoto";

    /// <summary>
    /// Gets or sets the month index for this image (0-11 for months, null for cover).
    /// </summary>
    public int? MonthIndex { get; set; }

    /// <summary>
    /// Gets or sets the optional index for multi-photo months (0..n-1). A value of null or 0 indicates the first slot.
    /// </summary>
    public int? SlotIndex { get; set; }

    /// <summary>
    /// Gets or sets the normalized horizontal pan offset (-1 to 1). A value of 0 indicates centered.
    /// </summary>
    public double PanX { get; set; } = 0;

    /// <summary>
    /// Gets or sets the normalized vertical pan offset (-1 to 1). A value of 0 indicates centered.
    /// </summary>
    public double PanY { get; set; } = 0;

    /// <summary>
    /// Gets or sets the zoom multiplier (0.5 to 3). A value of 1 indicates fit cover baseline.
    /// </summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the optional display order for month galleries.
    /// </summary>
    public int Order { get; set; } = 0;
}