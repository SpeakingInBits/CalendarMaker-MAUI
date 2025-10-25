namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of IDialogService that wraps .NET MAUI's DisplayAlert functionality.
/// Requires a reference to the current page or application to display dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
/// <summary>
    /// Gets the current page from the application's main page.
    /// </summary>
    private Page? CurrentPage => Application.Current?.MainPage;

    /// <inheritdoc />
    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        var page = CurrentPage;
        if (page == null)
        {
   // Fallback: log or throw - for now we'll silently fail
     System.Diagnostics.Debug.WriteLine($"DialogService: Cannot show alert - no current page. Title: {title}, Message: {message}");
            return;
        }

  await page.DisplayAlert(title, message, cancel);
    }

  /// <inheritdoc />
    public async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
    {
   var page = CurrentPage;
      if (page == null)
        {
            System.Diagnostics.Debug.WriteLine($"DialogService: Cannot show confirmation - no current page. Title: {title}");
            return false;
        }

        return await page.DisplayAlert(title, message, accept, cancel);
    }

    /// <inheritdoc />
    public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction = null, params string[] buttons)
 {
        var page = CurrentPage;
     if (page == null)
        {
       System.Diagnostics.Debug.WriteLine($"DialogService: Cannot show action sheet - no current page. Title: {title}");
         return null;
  }

      return await page.DisplayActionSheet(title, cancel, destruction, buttons);
    }
}
