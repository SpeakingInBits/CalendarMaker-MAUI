namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;
using Microsoft.Maui.Storage;

public interface IAssetService
{
    Task<ImageAsset?> ImportProjectPhotoAsync(CalendarProject project, FileResult fileResult);
    Task AssignPhotoToSlotAsync(CalendarProject project, string assetId, int monthIndex, int? slotIndex = null, string role = "monthPhoto");
    Task RemovePhotoFromSlotAsync(CalendarProject project, int monthIndex, int slotIndex, string role = "monthPhoto");
    string GetImagesDirectory(string projectId);
    Task<IReadOnlyList<ImageAsset>> GetUnassignedPhotosAsync(CalendarProject project);
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

    public async Task<ImageAsset?> ImportProjectPhotoAsync(CalendarProject project, FileResult fileResult)
    {
        if (fileResult == null || project == null) return null;
        var imagesDir = GetImagesDirectory(project.Id);
        var ext = Path.GetExtension(fileResult.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".img";
        var fileName = $"photo_{Guid.NewGuid():N}{ext}";
        var destPath = Path.Combine(imagesDir, fileName);

        await using (var src = await fileResult.OpenReadAsync())
        await using (var dst = File.Create(destPath))
        {
            await src.CopyToAsync(dst);
        }

        // Create unassigned photo asset
        var asset = new ImageAsset
        {
            ProjectId = project.Id,
            Path = destPath,
            Role = "unassigned",
            MonthIndex = null,
            SlotIndex = null,
            PanX = 0,
            PanY = 0,
            Zoom = 1
        };
        project.ImageAssets.Add(asset);
        await _storage.UpdateProjectAsync(project);
        return asset;
    }

    public async Task AssignPhotoToSlotAsync(CalendarProject project, string assetId, int monthIndex, int? slotIndex = null, string role = "monthPhoto")
    {
        if (project == null) return;

        var asset = project.ImageAssets.FirstOrDefault(a => a.Id == assetId);
        if (asset == null) return;

        // Remove any existing photo in this slot
        if (role == "monthPhoto" && slotIndex.HasValue)
        {
            await RemovePhotoFromSlotAsync(project, monthIndex, slotIndex.Value, role);
        }
        else if (role == "coverPhoto")
        {
            var existingCover = project.ImageAssets.FirstOrDefault(a => a.Role == "coverPhoto");
            if (existingCover != null)
            {
                existingCover.Role = "unassigned";
                existingCover.MonthIndex = null;
                existingCover.SlotIndex = null;
            }
        }

        // Assign the photo to the slot
        asset.Role = role;
        asset.MonthIndex = role == "coverPhoto" ? null : monthIndex;
        asset.SlotIndex = role == "coverPhoto" ? null : slotIndex;
        asset.PanX = asset.PanY = 0;
        asset.Zoom = 1;

        await _storage.UpdateProjectAsync(project);
    }

    public async Task RemovePhotoFromSlotAsync(CalendarProject project, int monthIndex, int slotIndex, string role = "monthPhoto")
    {
        if (project == null) return;

        var asset = project.ImageAssets.FirstOrDefault(a => 
            a.Role == role && 
            a.MonthIndex == monthIndex && 
            (a.SlotIndex ?? 0) == slotIndex);

        if (asset != null)
        {
            asset.Role = "unassigned";
            asset.MonthIndex = null;
            asset.SlotIndex = null;
            asset.PanX = asset.PanY = 0;
            asset.Zoom = 1;
            await _storage.UpdateProjectAsync(project);
        }
    }

    public async Task<IReadOnlyList<ImageAsset>> GetUnassignedPhotosAsync(CalendarProject project)
    {
        return project.ImageAssets
            .Where(a => a.Role == "unassigned")
            .OrderBy(a => a.Id)
            .ToList();
    }
}
