namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service for displaying dialogs and alerts to the user.
/// Abstracts platform-specific dialog functionality to improve testability.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Displays an alert dialog with a single button.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message to display.</param>
/// <param name="cancel">The text for the cancel/dismiss button. Defaults to "OK".</param>
    /// <returns>A task that completes when the alert is dismissed.</returns>
    Task ShowAlertAsync(string title, string message, string cancel = "OK");

    /// <summary>
    /// Displays a confirmation dialog with accept and cancel buttons.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <returns>True if the user accepted, false if they cancelled.</returns>
    Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Displays an action sheet with multiple options.
    /// </summary>
    /// <param name="title">The title of the action sheet.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <param name="destruction">Optional text for a destructive action button.</param>
    /// <param name="buttons">The available action buttons.</param>
    /// <returns>The text of the selected button, or null if cancelled.</returns>
    Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction = null, params string[] buttons);
}
