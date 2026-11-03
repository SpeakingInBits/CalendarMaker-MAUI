using System.Diagnostics;
using CalendarMaker_MAUI.Services;
using Velopack;
using Velopack.Sources;

namespace CalendarMaker_MAUI.WinUI;

/// <summary>
/// Updates the app from the project's GitHub releases with Velopack. The Release workflow publishes each platform's
/// package and release list (<c>releases.&lt;channel&gt;.json</c>) there, and the app only looks at its own channel.
/// </summary>
public sealed class AppUpdater : IAppUpdater
{
    // "latest/download" always serves the newest release's files. Asking GitHub's API instead would be limited to
    // 60 requests an hour per IP address.
    private const string ReleaseFiles = "https://github.com/SpeakingInBits/CalendarMaker-MAUI/releases/latest/download/";

    // CM_UPDATE_SOURCE, a folder or URL holding packaged releases, tries out an update before it's published.
    private readonly UpdateManager _manager = Environment.GetEnvironmentVariable("CM_UPDATE_SOURCE") is { Length: > 0 } source
        ? new(source)
        : new(new SimpleWebSource(ReleaseFiles));
    private UpdateInfo? _update;

    public async Task<string?> DownloadUpdateAsync()
    {
        if (!_manager.IsInstalled)
            return null;

        try
        {
            _update = await _manager.CheckForUpdatesAsync();
            if (_update is null)
                return null;

            await _manager.DownloadUpdatesAsync(_update);
            return _update.TargetFullRelease.Version.ToString();
        }
        catch (Exception ex)
        {
            // Not worth bothering the user with; the app checks again next time it opens.
            Trace.WriteLine($"Update check failed: {ex}");
            _update = null;
            return null;
        }
    }

    public void RestartToUpdate()
    {
        if (_update is not null)
            _manager.ApplyUpdatesAndRestart(_update.TargetFullRelease);
    }
}
