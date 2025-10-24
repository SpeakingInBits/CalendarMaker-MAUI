namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Represents the margins for a calendar page, stored in points (1 pt = 1/72 inch).
/// </summary>
public sealed class Margins
{
    /// <summary>
    /// Gets or sets the left margin in points (1 pt = 1/72 inch).
    /// Default value is 14.4 points (0.2 inches).
    /// </summary>
    public double LeftPt { get; set; } = 14.4; // 0.2 in

    /// <summary>
    /// Gets or sets the top margin in points (1 pt = 1/72 inch).
    /// Default value is 14.4 points (0.2 inches).
    /// </summary>
    public double TopPt { get; set; } = 14.4;

    /// <summary>
    /// Gets or sets the right margin in points (1 pt = 1/72 inch).
    /// Default value is 14.4 points (0.2 inches).
    /// </summary>
    public double RightPt { get; set; } = 14.4;

    /// <summary>
    /// Gets or sets the bottom margin in points (1 pt = 1/72 inch).
    /// Default value is 14.4 points (0.2 inches).
    /// </summary>
    public double BottomPt { get; set; } = 14.4;
}
