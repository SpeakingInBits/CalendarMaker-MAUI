using CalendarMaker_MAUI.Services;
using Moq;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="AppUpdateCheck"/>, which offers to restart into a downloaded update.
/// </summary>
public class AppUpdateCheckTests
{
    private readonly Mock<IAppUpdater> _updater = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly AppUpdateCheck _check;

    public AppUpdateCheckTests()
    {
        _check = new AppUpdateCheck(_updater.Object, _dialogs.Object);
    }

    [Fact]
    public async Task RunAsync_NoUpdate_DoesNotAsk()
    {
        _updater.Setup(u => u.DownloadUpdateAsync()).ReturnsAsync((string?)null);

        await _check.RunAsync();

        _dialogs.Verify(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _updater.Verify(u => u.RestartToUpdate(), Times.Never);
    }

    [Fact]
    public async Task RunAsync_UpdateAndRestartNow_Restarts()
    {
        _updater.Setup(u => u.DownloadUpdateAsync()).ReturnsAsync("1.2.3");
        _dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.Is<string>(m => m.Contains("1.2.3")), "Restart now", "Later"))
            .ReturnsAsync(true);

        await _check.RunAsync();

        _updater.Verify(u => u.RestartToUpdate(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UpdateAndLater_DoesNotRestart()
    {
        _updater.Setup(u => u.DownloadUpdateAsync()).ReturnsAsync("1.2.3");
        _dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _check.RunAsync();

        _updater.Verify(u => u.RestartToUpdate(), Times.Never);
    }
}
