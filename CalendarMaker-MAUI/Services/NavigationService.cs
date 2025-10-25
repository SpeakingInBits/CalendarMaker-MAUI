namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Implementation of INavigationService that wraps .NET MAUI Shell navigation.
/// </summary>
public sealed class NavigationService : INavigationService
{
    /// <inheritdoc />
    public async Task NavigateToAsync(string route, Dictionary<string, object>? parameters = null)
    {
    if (Shell.Current == null)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationService: Cannot navigate - Shell.Current is null. Route: {route}");
  return;
      }

        if (parameters == null || parameters.Count == 0)
   {
            await Shell.Current.GoToAsync(route);
        }
        else
  {
            await Shell.Current.GoToAsync(route, parameters);
        }
    }

    /// <inheritdoc />
    public async Task GoBackAsync()
    {
        if (Shell.Current == null)
        {
            System.Diagnostics.Debug.WriteLine("NavigationService: Cannot go back - Shell.Current is null.");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    /// <inheritdoc />
    public async Task PushModalAsync(Page page, bool animated = true)
    {
        if (Shell.Current?.Navigation == null)
        {
 System.Diagnostics.Debug.WriteLine("NavigationService: Cannot push modal - Shell.Current.Navigation is null.");
            return;
   }

        await Shell.Current.Navigation.PushModalAsync(page, animated);
    }

/// <inheritdoc />
    public async Task PopModalAsync(bool animated = true)
    {
        if (Shell.Current?.Navigation == null)
        {
       System.Diagnostics.Debug.WriteLine("NavigationService: Cannot pop modal - Shell.Current.Navigation is null.");
   return;
        }

   await Shell.Current.Navigation.PopModalAsync(animated);
    }
}
