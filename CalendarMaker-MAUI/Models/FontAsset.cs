namespace CalendarMaker_MAUI.Models;

/// <summary>
/// Represents a font asset associated with a calendar project.
/// </summary>
public sealed class FontAsset
{
    /// <summary>
    /// Gets or sets the unique identifier for this font asset.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the identifier of the project this font asset belongs to.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path to the font file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font family name.
    /// </summary>
    public string FamilyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font style (e.g., Regular, Bold, Italic).
    /// </summary>
    public string Style { get; set; } = string.Empty;
}
