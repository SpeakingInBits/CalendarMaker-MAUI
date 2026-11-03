namespace CalendarMaker_MAUI;

public partial class App : Application
{
    public App(Views.ProjectsPage projectsPage)
    {
        InitializeComponent();
        // Root page will be created in CreateWindow
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Initialize the app using Shell and return a Window with the root page
        var shell = new AppShell();
        var window = new Window(shell);
        return window;
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Only registered on platforms that are released with updates (Windows).
        if (AppServices.Services.GetService<Services.AppUpdateCheck>() is { } updateCheck)
            await updateCheck.RunAsync();
    }
}