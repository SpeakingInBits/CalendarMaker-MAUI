using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service for file picking and saving operations.
/// Abstracts platform-specific file operations to improve testability.
/// </summary>
public interface IFilePickerService
{
 /// <summary>
    /// Prompts the user to pick a single file.
  /// </summary>
    /// <param name="options">Options for file picking (e.g., file types, title).</param>
    /// <returns>The selected file result, or null if cancelled.</returns>
    Task<FileResult?> PickFileAsync(PickOptions? options = null);

    /// <summary>
    /// Prompts the user to pick multiple files.
    /// </summary>
    /// <param name="options">Options for file picking (e.g., file types, title).</param>
    /// <returns>A collection of selected file results, or empty if cancelled.</returns>
    Task<IEnumerable<FileResult>> PickMultipleFilesAsync(PickOptions? options = null);

    /// <summary>
    /// Prompts the user to save a file with the given content.
    /// </summary>
    /// <param name="fileName">The suggested file name.</param>
    /// <param name="stream">The stream containing the file content.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the save operation.</returns>
    Task<FileSaverResult> SaveFileAsync(string fileName, Stream stream, CancellationToken cancellationToken = default);
}
