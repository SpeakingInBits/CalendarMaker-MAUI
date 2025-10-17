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
    Task<IReadOnlyList<ImageAsset>> GetAllPhotosAsync(CalendarProject project);
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

        var sourceAsset = project.ImageAssets.FirstOrDefault(a => a.Id == assetId);
        if (sourceAsset == null) return;

        // Remove any existing photo in the target slot (not the source photo)
        if (role == "monthPhoto" && slotIndex.HasValue)
        {
            await RemovePhotoFromSlotAsync(project, monthIndex, slotIndex.Value, role);
        }
        else if (role == "coverPhoto" || role == "backCoverPhoto")
        {
            // Remove existing photo from THIS SPECIFIC SLOT on the cover
            var existingCover = project.ImageAssets.FirstOrDefault(a => 
                a.Role == role && 
                (a.SlotIndex ?? 0) == (slotIndex ?? 0));
            if (existingCover != null)
            {
                project.ImageAssets.Remove(existingCover);
            }
        }

        // Create a NEW asset instance that references the same file
        // This allows the same photo to be used in multiple places
        var newAsset = new ImageAsset
        {
            Id = Guid.NewGuid().ToString("N"), // New unique ID
            ProjectId = project.Id,
            Path = sourceAsset.Path, // Same file path - reuse the image file
            Role = role,
            MonthIndex = (role == "coverPhoto" || role == "backCoverPhoto") ? null : monthIndex,
            SlotIndex = slotIndex, // Keep the slot index for ALL roles (including covers!)
            PanX = 0,
            PanY = 0,
            Zoom = 1,
            Order = 0
        };

        // Add the new asset to the project
        project.ImageAssets.Add(newAsset);

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
            // Remove the asset instance from the project
            // The original "unassigned" photo remains available for reuse
            project.ImageAssets.Remove(asset);
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

    public async Task<IReadOnlyList<ImageAsset>> GetAllPhotosAsync(CalendarProject project)
    {
        // Return ALL assets - the modal will handle grouping and deduplication
        return await Task.FromResult(project.ImageAssets.ToList());
    }
}
