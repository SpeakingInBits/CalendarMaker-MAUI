namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service for navigating between pages in the application.
/// Abstracts Shell navigation to improve testability and decouple from Shell dependencies.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to a route with optional parameters.
    /// </summary>
    /// <param name="route">The route to navigate to.</param>
    /// <param name="parameters">Optional navigation parameters as key-value pairs.</param>
    /// <returns>A task that completes when navigation is finished.</returns>
    Task NavigateToAsync(string route, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Navigates back in the navigation stack.
    /// </summary>
    /// <returns>A task that completes when navigation is finished.</returns>
    Task GoBackAsync();

    /// <summary>
    /// Pushes a modal page onto the navigation stack.
    /// </summary>
    /// <param name="page">The page to display as a modal.</param>
 /// <param name="animated">Whether to animate the transition.</param>
    /// <returns>A task that completes when the modal is displayed.</returns>
    Task PushModalAsync(Page page, bool animated = true);

    /// <summary>
    /// Pops the current modal page from the navigation stack.
    /// </summary>
    /// <param name="animated">Whether to animate the transition.</param>
    /// <returns>A task that completes when the modal is dismissed.</returns>
    Task PopModalAsync(bool animated = true);
}
