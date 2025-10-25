using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of IFilePickerService that wraps .NET MAUI file picker and saver functionality.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    /// <inheritdoc />
    public async Task<FileResult?> PickFileAsync(PickOptions? options = null)
    {
        try
    {
 return await FilePicker.PickAsync(options);
        }
    catch (Exception ex)
     {
            System.Diagnostics.Debug.WriteLine($"FilePickerService: Error picking file - {ex.Message}");
      return null;
}
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FileResult>> PickMultipleFilesAsync(PickOptions? options = null)
    {
  try
  {
   var results = await FilePicker.PickMultipleAsync(options);
         return results ?? Enumerable.Empty<FileResult>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FilePickerService: Error picking multiple files - {ex.Message}");
    return Enumerable.Empty<FileResult>();
        }
    }

    /// <inheritdoc />
    public async Task<FileSaverResult> SaveFileAsync(string fileName, Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
 return await FileSaver.Default.SaveAsync(fileName, stream, cancellationToken);
        }
    catch (Exception ex)
        {
  System.Diagnostics.Debug.WriteLine($"FilePickerService: Error saving file - {ex.Message}");
          return new FileSaverResult(null, new Exception("Failed to save file", ex));
  }
    }
}
