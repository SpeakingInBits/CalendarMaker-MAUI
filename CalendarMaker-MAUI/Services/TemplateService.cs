namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;

public interface ITemplateService
{
    IEnumerable<string> GetTemplateKeys();
    TemplateDescriptor GetTemplate(string key);
}

public sealed class TemplateDescriptor
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class TemplateService : ITemplateService
{
    private readonly Dictionary<string, TemplateDescriptor> _templates = new();

    public TemplateService()
    {
        // Register default templates (dev-authored)
        Register(new TemplateDescriptor
        {
            Key = "PhotoMonthlyClassic",
            Name = "Photo Monthly Classic",
            Description = "Photo + monthly grid with configurable split"
        });
        Register(new TemplateDescriptor
        {
            Key = "PhotoCover",
            Name = "Photo Cover",
            Description = "Full photo or 2x3 grid with customizable title/subtitle"
        });
    }

    public IEnumerable<string> GetTemplateKeys() => _templates.Keys;

    public TemplateDescriptor GetTemplate(string key) => _templates[key];

    private void Register(TemplateDescriptor descriptor)
    {
        _templates[descriptor.Key] = descriptor;
    }
}
