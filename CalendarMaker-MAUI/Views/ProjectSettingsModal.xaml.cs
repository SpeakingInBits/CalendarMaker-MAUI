using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Views;

public partial class ProjectSettingsModal : ContentPage
{
    private CalendarProject? _project;

    public event EventHandler? Cancelled;
    public event EventHandler? Applied;

    public ProjectSettingsModal(CalendarProject project)
    {
        InitializeComponent();
        _project = project;

        // Initialize margin sliders with current values (convert points to inches)
        TopMarginSlider.Value = _project.Margins.TopPt / 72.0;
        BottomMarginSlider.Value = _project.Margins.BottomPt / 72.0;
        LeftMarginSlider.Value = _project.Margins.LeftPt / 72.0;
        RightMarginSlider.Value = _project.Margins.RightPt / 72.0;

        // Initialize borderless calendar settings
        BorderlessCalendarCheckbox.IsChecked = _project.CoverSpec.BorderlessCalendar;
        CalendarPaddingSlider.Value = _project.CoverSpec.CalendarPaddingPt / 72.0;

        // Initialize calendar background settings
        UseCalendarBackgroundCheckbox.IsChecked = _project.CoverSpec.UseCalendarBackgroundOnBorderless;
        BackgroundColorEntry.Text = _project.Theme.BackgroundColor ?? "#FFFFFF";
        UpdateColorPreview(_project.Theme.BackgroundColor ?? "#FFFFFF");

        // Wire up events
        CancelBtn.Clicked += OnCancelClicked;
        ApplyBtn.Clicked += OnApplyClicked;
        ResetMarginsBtn.Clicked += OnResetMarginsClicked;
        ColorPickerTap.Tapped += OnColorPickerTapped;
    }

    private void OnBackgroundColorChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateColorPreview(e.NewTextValue);
    }

    private void UpdateColorPreview(string? colorHex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                ColorPreview.BackgroundColor = Color.FromArgb(colorHex);
            }
        }
        catch
        {
            // Invalid color, keep previous color
        }
    }

    private async void OnColorPickerTapped(object? sender, EventArgs e)
    {
        // Show color picker action sheet with common colors
        string? action = await DisplayActionSheet(
            "Choose Background Color",
            "Cancel",
            null,
            "White (#FFFFFF)",
            "Light Gray (#F5F5F5)",
            "Gray (#E0E0E0)",
            "Light Blue (#E3F2FD)",
            "Light Green (#E8F5E9)",
            "Light Yellow (#FFF9C4)",
            "Light Pink (#FCE4EC)",
            "Light Purple (#F3E5F5)",
            "Beige (#F5F5DC)",
            "Cream (#FFFDD0)",
            "Custom..."
        );

        string? newColor = action switch
        {
            "White (#FFFFFF)" => "#FFFFFF",
            "Light Gray (#F5F5F5)" => "#F5F5F5",
            "Gray (#E0E0E0)" => "#E0E0E0",
            "Light Blue (#E3F2FD)" => "#E3F2FD",
            "Light Green (#E8F5E9)" => "#E8F5E9",
            "Light Yellow (#FFF9C4)" => "#FFF9C4",
            "Light Pink (#FCE4EC)" => "#FCE4EC",
            "Light Purple (#F3E5F5)" => "#F3E5F5",
            "Beige (#F5F5DC)" => "#F5F5DC",
            "Cream (#FFFDD0)" => "#FFFDD0",
            "Custom..." => await PromptForCustomColor(),
            _ => null
        };

        if (newColor != null)
        {
            BackgroundColorEntry.Text = newColor;
            UpdateColorPreview(newColor);
        }
    }

    private async Task<string?> PromptForCustomColor()
    {
        string? result = await DisplayPromptAsync(
            "Custom Color",
            "Enter hex color code (e.g., #FF5733):",
            placeholder: "#FFFFFF",
            maxLength: 7,
            keyboard: Keyboard.Text);

        if (!string.IsNullOrWhiteSpace(result) && result.StartsWith("#") && (result.Length == 7 || result.Length == 9))
        {
            return result;
        }

        return null;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_project != null)
        {
            // Convert inches back to points and update project
            _project.Margins.TopPt = TopMarginSlider.Value * 72.0;
            _project.Margins.BottomPt = BottomMarginSlider.Value * 72.0;
            _project.Margins.LeftPt = LeftMarginSlider.Value * 72.0;
            _project.Margins.RightPt = RightMarginSlider.Value * 72.0;

            // Update borderless calendar settings
            _project.CoverSpec.BorderlessCalendar = BorderlessCalendarCheckbox.IsChecked;
            _project.CoverSpec.CalendarPaddingPt = CalendarPaddingSlider.Value * 72.0;

            // If borderless is enabled, set all margins to 0
            if (_project.CoverSpec.BorderlessCalendar)
            {
                _project.Margins.LeftPt = 0;
                _project.Margins.TopPt = 0;
                _project.Margins.RightPt = 0;
                _project.Margins.BottomPt = 0;
                _project.CoverSpec.BorderlessFrontCover = true;
                _project.CoverSpec.BorderlessBackCover = true;
            }
            else
            {
                _project.CoverSpec.BorderlessFrontCover = false;
                _project.CoverSpec.BorderlessBackCover = false;
            }

            // Update calendar background settings
            _project.CoverSpec.UseCalendarBackgroundOnBorderless = UseCalendarBackgroundCheckbox.IsChecked;
            if (!string.IsNullOrWhiteSpace(BackgroundColorEntry.Text))
            {
                _project.Theme.BackgroundColor = BackgroundColorEntry.Text.Trim();
            }
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void OnResetMarginsClicked(object? sender, EventArgs e)
    {
        // Reset to default: 0.2 inches = 14.4 points
        TopMarginSlider.Value = 0.2;
        BottomMarginSlider.Value = 0.2;
        LeftMarginSlider.Value = 0.2;
        RightMarginSlider.Value = 0.2;
    }
}
