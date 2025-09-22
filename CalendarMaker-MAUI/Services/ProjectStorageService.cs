namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;

public interface IProjectStorageService
{
    Task<string> CreateProjectAsync(CalendarProject project);
    Task<IReadOnlyList<CalendarProject>> GetProjectsAsync();
}

public sealed class ProjectStorageService : IProjectStorageService
{
    private readonly string _root;

    public ProjectStorageService()
    {
        _root = Path.Combine(FileSystem.Current.AppDataDirectory, "Projects");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> CreateProjectAsync(CalendarProject project)
    {
        project.Id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(_root, project.Id);
        Directory.CreateDirectory(dir);

        var jsonPath = Path.Combine(dir, "project.json");
        var json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(jsonPath, json);
        return project.Id;
    }

    public async Task<IReadOnlyList<CalendarProject>> GetProjectsAsync()
    {
        var result = new List<CalendarProject>();
        if (!Directory.Exists(_root)) return result;
        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            var json = Path.Combine(dir, "project.json");
            if (File.Exists(json))
            {
                var text = await File.ReadAllTextAsync(json);
                var project = System.Text.Json.JsonSerializer.Deserialize<CalendarProject>(text);
                if (project != null) result.Add(project);
            }
        }
        return result;
    }
}
