namespace CalendarMaker_MAUI
{
    public partial class App : Application
    {
        public App(Views.ProjectsPage projectsPage)
        {
            InitializeComponent();

            // Use a NavigationPage to support PushAsync from ProjectsPage
            MainPage = new NavigationPage(projectsPage);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(MainPage);
        }
    }
}