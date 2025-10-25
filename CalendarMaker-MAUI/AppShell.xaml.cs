namespace CalendarMaker_MAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register route for DesignerPage using type (Shell will instantiate the page)
        Routing.RegisterRoute("designer", typeof(Views.DesignerPage));
    }
}