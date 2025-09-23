namespace CalendarMaker_MAUI
{
    public partial class App : Application
    {
        public App(Views.ProjectsPage projectsPage)
        {
            InitializeComponent();

            // Use Shell again for modern navigation
            MainPage = new AppShell();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(MainPage);
        }
    }
}