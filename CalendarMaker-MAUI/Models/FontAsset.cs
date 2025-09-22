namespace CalendarMaker_MAUI.Models;

public sealed class FontAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty; // Regular/Bold/Italic
}
