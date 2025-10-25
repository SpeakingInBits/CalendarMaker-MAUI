using System.ComponentModel;

namespace CalendarMaker_MAUI.Views;

public partial class NewProjectModal : ContentPage
{
    public string? SelectedPreset { get; private set; }
    public string? ProjectName { get; private set; }
    public int? Year { get; private set; }

    public event EventHandler? Cancelled;
    public event EventHandler? Created;

    public NewProjectModal()
    {
        InitializeComponent();

        // Defaults
        PresetPicker.SelectedIndex = 0;
        YearEntry.Text = DateTime.Now.Year.ToString();
        NameEntry.Text = "My Calendar";

        CancelBtn.Clicked += (_, __) => { Cancelled?.Invoke(this, EventArgs.Empty); };
        CreateBtn.Clicked += OnCreateClicked;
    }

    private void OnCreateClicked(object? sender, EventArgs e)
    {
        SelectedPreset = PresetPicker.SelectedIndex switch
        {
            0 => "5x7 Landscape 50/50 (Photo Left)",
            1 => "Letter Portrait 50/50 (Photo Top)",
            2 => "11x17 Portrait 60/40 (Photo Top)",
            3 => "13x19 Portrait 65/35 (Photo Top)",
            _ => "5x7 Landscape 50/50 (Photo Left)"
        };

        string? name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "My Calendar";
        }

        ProjectName = name;

        if (int.TryParse(YearEntry.Text, out int year))
        {
            Year = year;
        }
        else
        {
            Year = DateTime.Now.Year;
        }

        Created?.Invoke(this, EventArgs.Empty);
    }
}