namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;
using Microsoft.Maui.Storage;

public interface IAssetService
{
    Task<ImageAsset?> ImportMonthPhotoAsync(CalendarProject project, int monthIndex, FileResult fileResult);
    string GetImagesDirectory(string projectId);
}

public sealed class AssetService : IAssetService
{
    private readonly IProjectStorageService _storage;

    public AssetService(IProjectStorageService storage)
    {
        _storage = storage;
    }

    public string GetImagesDirectory(string projectId)
    {
        var dir = Path.Combine(_storage.GetProjectDirectory(projectId), "Assets", "Images");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<ImageAsset?> ImportMonthPhotoAsync(CalendarProject project, int monthIndex, FileResult fileResult)
    {
        if (fileResult == null || project == null) return null;
        var imagesDir = GetImagesDirectory(project.Id);
        var ext = Path.GetExtension(fileResult.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".img";
        var fileName = $"month_{monthIndex + 1}_{Guid.NewGuid():N}{ext}";
        var destPath = Path.Combine(imagesDir, fileName);

        await using (var src = await fileResult.OpenReadAsync())
        await using (var dst = File.Create(destPath))
        {
            await src.CopyToAsync(dst);
        }

        // Do not remove other month photos; multi-slot months are supported now
        var asset = new ImageAsset
        {
            ProjectId = project.Id,
            Path = destPath,
            Role = "monthPhoto",
            MonthIndex = monthIndex
        };
        project.ImageAssets.Add(asset);
        await _storage.UpdateProjectAsync(project);
        return asset;
    }
}
