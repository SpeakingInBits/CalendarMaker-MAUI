namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Checks for a newer version when the app starts. A newer version downloads in the background, then the user can
/// restart into it now; otherwise it installs the next time the app opens.
/// </summary>
public sealed class AppUpdateCheck
{
    private readonly IAppUpdater _updater;
    private readonly IDialogService _dialogService;

    public AppUpdateCheck(IAppUpdater updater, IDialogService dialogService)
    {
        _updater = updater;
        _dialogService = dialogService;
    }

    public async Task RunAsync()
    {
        var version = await _updater.DownloadUpdateAsync();
        if (version is null)
            return;

        // Projects are only saved when the user saves them, so restarting now would lose unsaved changes.
        var restart = await _dialogService.ShowConfirmAsync(
            "Update ready",
            $"Version {version} is ready. It installs the next time you open the app, or restart now. Save your calendar before restarting.",
            "Restart now",
            "Later");

        if (restart)
            _updater.RestartToUpdate();
    }
}
