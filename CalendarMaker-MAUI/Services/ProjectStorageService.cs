namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;

public interface IProjectStorageService
{
    Task<string> CreateProjectAsync(CalendarProject project);
    Task<IReadOnlyList<CalendarProject>> GetProjectsAsync();
    Task DeleteProjectAsync(string projectId);
    Task UpdateProjectAsync(CalendarProject project);

    string GetProjectDirectory(string projectId);
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
        string dir = Path.Combine(_root, project.Id);
        Directory.CreateDirectory(dir);
        await WriteProjectJsonAsync(dir, project);
        return project.Id;
    }

    public async Task<IReadOnlyList<CalendarProject>> GetProjectsAsync()
    {
        var result = new List<CalendarProject>();
        if (!Directory.Exists(_root))
        {
            return result;
        }

        foreach (string dir in Directory.EnumerateDirectories(_root))
        {
            string json = Path.Combine(dir, "project.json");
            if (File.Exists(json))
            {
                string text = await File.ReadAllTextAsync(json);
                var project = System.Text.Json.JsonSerializer.Deserialize<CalendarProject>(text);
                if (project != null)
                {
                    result.Add(project);
                }
            }
        }
        return result;
    }

    public Task DeleteProjectAsync(string projectId)
    {
        string dir = Path.Combine(_root, projectId);
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* swallow for now; could log */ }
        }
        return Task.CompletedTask;
    }

    public async Task UpdateProjectAsync(CalendarProject project)
    {
        string dir = GetProjectDirectory(project.Id);
        Directory.CreateDirectory(dir);
        project.UpdatedUtc = DateTime.UtcNow;
        await WriteProjectJsonAsync(dir, project);
    }

    public string GetProjectDirectory(string projectId)
    {
        return Path.Combine(_root, projectId);
    }

    private static async Task WriteProjectJsonAsync(string dir, CalendarProject project)
    {
        string jsonPath = Path.Combine(dir, "project.json");
        string json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(jsonPath, json);
    }
}