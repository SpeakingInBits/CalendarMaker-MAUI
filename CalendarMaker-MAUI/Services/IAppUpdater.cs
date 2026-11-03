namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Finds and downloads a newer version of the app. Only the Windows build has one (Platforms/Windows/AppUpdater.cs),
/// since it's the only platform released so far.
/// </summary>
public interface IAppUpdater
{
    /// <summary>
    /// Downloads the newest release if it's newer than this one and returns its version, or null when the app is up to date,
    /// wasn't installed from a release (a debug build), or the check failed (offline).
    /// </summary>
    Task<string?> DownloadUpdateAsync();

    /// <summary>Closes the app, installs the downloaded update and opens the new version.</summary>
    void RestartToUpdate();
}
