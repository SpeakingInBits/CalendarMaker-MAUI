namespace CalendarMaker_MAUI.Views;

public partial class NewProjectPage : ContentPage
{
    public NewProjectPage()
    {
        Content = new VerticalStackLayout
        {
            Padding = 16,
            Children =
            {
                new Label{ Text = "New Project (Coming Soon)", FontAttributes = FontAttributes.Bold },
                new Label{ Text = "This dialog will let you choose presets (5x7 Landscape 50/50, Letter Portrait 50/50), flip sides, set margins, and font/theme."}
            }
        };
    }
}
