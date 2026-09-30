using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Views;

public partial class ProjectSettingsModal : ContentPage
{
    private const int MinYear = 1900;
    private const int MaxYear = 2100;

    private CalendarProject? _project;

    // Length of the date range in months, kept so moving the start date shifts the end date with it.
    private int _monthCount;
    private bool _syncingDateRange;

    public event EventHandler? Cancelled;
    public event EventHandler? Applied;

    public ProjectSettingsModal(CalendarProject project)
    {
        InitializeComponent();
        _project = project;

        // Initialize page size picker
        PageSizePicker.ItemsSource = new List<string>
        {
            "5×7 inches",
            "Letter (8.5×11 inches)",
            "Tabloid/Ledger (11×17 inches)",
            "Super B (13×19 inches)",
            "12x12 inches (square)"
        };

        // Set current page size
        PageSizePicker.SelectedIndex = _project.PageSpec.Size switch
        {
            PageSize.FiveBySeven => 0,
            PageSize.Letter => 1,
            PageSize.Tabloid_11x17 => 2,
            PageSize.SuperB_13x19 => 3,
            PageSize.Square_12x12 => 4,
            _ => 1 // Default to Letter
        };

        // Initialize orientation picker
        OrientationPicker.ItemsSource = new List<string>
        {
            "Portrait",
            "Landscape"
        };

        OrientationPicker.SelectedIndex = _project.PageSpec.Orientation == PageOrientation.Portrait ? 0 : 1;

        // Initialize margin sliders with current values (convert points to inches)
        TopMarginSlider.Value = _project.Margins.TopPt / 72.0;
        BottomMarginSlider.Value = _project.Margins.BottomPt / 72.0;
        LeftMarginSlider.Value = _project.Margins.LeftPt / 72.0;
        RightMarginSlider.Value = _project.Margins.RightPt / 72.0;

        // Initialize borderless calendar settings
        BorderlessCalendarCheckbox.IsChecked = _project.CoverSpec.BorderlessCalendar;
        CalendarTopPaddingSlider.Value = _project.CoverSpec.CalendarTopPaddingPt / 72.0;
        CalendarSidePaddingSlider.Value = _project.CoverSpec.CalendarSidePaddingPt / 72.0;
        CalendarBottomPaddingSlider.Value = _project.CoverSpec.CalendarBottomPaddingPt / 72.0;

        // Initialize calendar date range
        var monthNames = new List<string>
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };
        StartMonthPicker.ItemsSource = monthNames;
        EndMonthPicker.ItemsSource = monthNames;

        _monthCount = _project.MonthCount;
        _syncingDateRange = true;
        YearEntry.Text = _project.Year.ToString();
        StartMonthPicker.SelectedIndex = _project.StartMonth - 1;
        EndYearEntry.Text = _project.EndDate.Year.ToString();
        EndMonthPicker.SelectedIndex = _project.EndDate.Month - 1;
        _syncingDateRange = false;
        UpdateDateRangeSummary();

        // Populate day of week names
        FirstDayOfWeekPicker.ItemsSource = new List<string>
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };
        FirstDayOfWeekPicker.SelectedIndex = (int)_project.FirstDayOfWeek;

        DoubleSidedCheckbox.IsChecked = _project.EnableDoubleSided;

        // Initialize calendar background settings
        UseCalendarBackgroundCheckbox.IsChecked = _project.CoverSpec.UseCalendarBackgroundOnBorderless;
        BackgroundColorEntry.Text = _project.Theme.BackgroundColor ?? "#FFFFFF";
        UpdateColorPreview(_project.Theme.BackgroundColor ?? "#FFFFFF");

        // Wire up events
        CancelBtn.Clicked += OnCancelClicked;
        ApplyBtn.Clicked += OnApplyClicked;
        ResetMarginsBtn.Clicked += OnResetMarginsClicked;
        ColorPickerTap.Tapped += OnColorPickerTapped;

        // Handle picker changes
        PageSizePicker.SelectedIndexChanged += OnPageSizeChanged;
        OrientationPicker.SelectedIndexChanged += OnOrientationChanged;

        // Keep the date range consistent as the user edits it
        StartMonthPicker.SelectedIndexChanged += OnStartDateChanged;
        YearEntry.TextChanged += OnStartDateChanged;
        EndMonthPicker.SelectedIndexChanged += OnEndDateChanged;
        EndYearEntry.TextChanged += OnEndDateChanged;
    }

    private void OnStartDateChanged(object? sender, EventArgs e)
    {
        if (_syncingDateRange)
        {
            return;
        }

        // Move the end date along with the start date so the calendar keeps its length.
        if (TryGetMonth(YearEntry, StartMonthPicker, out DateTime start))
        {
            DateTime end = start.AddMonths(_monthCount - 1);
            _syncingDateRange = true;
            EndYearEntry.Text = end.Year.ToString();
            EndMonthPicker.SelectedIndex = end.Month - 1;
            _syncingDateRange = false;
        }

        UpdateDateRangeSummary();
    }

    private void OnEndDateChanged(object? sender, EventArgs e)
    {
        if (_syncingDateRange)
        {
            return;
        }

        if (TryGetDateRange(out _, out _, out int count, out _))
        {
            _monthCount = count;
        }

        UpdateDateRangeSummary();
    }

    private void UpdateDateRangeSummary()
    {
        if (TryGetDateRange(out DateTime start, out DateTime end, out int count, out string? error))
        {
            DateRangeSummaryLabel.Text = $"{count} {(count == 1 ? "month" : "months")}: " +
                $"{start:MMMM yyyy} through {end:MMMM yyyy}";
            DateRangeSummaryLabel.TextColor = Colors.Gray;
        }
        else
        {
            DateRangeSummaryLabel.Text = error;
            DateRangeSummaryLabel.TextColor = Colors.Red;
        }
    }

    private static bool TryGetMonth(Entry yearEntry, Picker monthPicker, out DateTime month)
    {
        month = default;
        if (!int.TryParse(yearEntry.Text, out int year) || year < MinYear || year > MaxYear ||
            monthPicker.SelectedIndex < 0)
        {
            return false;
        }

        month = new DateTime(year, monthPicker.SelectedIndex + 1, 1);
        return true;
    }

    /// <summary>
    /// Reads and validates the start and end dates entered by the user.
    /// </summary>
    private bool TryGetDateRange(out DateTime start, out DateTime end, out int count, out string? error)
    {
        count = 0;
        end = default;

        if (!TryGetMonth(YearEntry, StartMonthPicker, out start))
        {
            error = $"Enter a start year between {MinYear} and {MaxYear}.";
            return false;
        }

        if (!TryGetMonth(EndYearEntry, EndMonthPicker, out end))
        {
            error = $"Enter an end year between {MinYear} and {MaxYear}.";
            return false;
        }

        count = CalendarProject.CountMonths(start.Year, start.Month, end.Year, end.Month);
        if (count < 1)
        {
            error = "The end date must be on or after the start date.";
            return false;
        }

        if (count > CalendarProject.MaxMonthCount)
        {
            error = $"A calendar can span at most {CalendarProject.MaxMonthCount} months (this range is {count}).";
            return false;
        }

        error = null;
        return true;
    }

    private void OnBackgroundColorChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateColorPreview(e.NewTextValue);
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        // Show current page dimensions
        if (_project != null && PageSizePicker.SelectedIndex >= 0)
        {
            UpdatePageDimensionsDisplay();
        }
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        // Show updated page dimensions with new orientation
        if (_project != null && OrientationPicker.SelectedIndex >= 0)
        {
            UpdatePageDimensionsDisplay();
        }
    }

    private void UpdatePageDimensionsDisplay()
    {
        // This could show a label with current dimensions, but for now we'll just note it
        // The actual application happens in OnApplyClicked
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

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_project != null)
        {
            // Validate the date range before changing anything so an invalid range leaves the project untouched
            if (!TryGetDateRange(out DateTime start, out DateTime end, out int count, out string? error))
            {
                await DisplayAlert("Invalid Date Range", error, "OK");
                return;
            }

            if (DoubleSidedCheckbox.IsChecked && count != 12)
            {
                await DisplayAlert(
                    "Invalid Date Range",
                    $"Double-sided calendars must span exactly 12 months, but this range is {count}. " +
                    "Adjust the dates or turn off Double-Sided Calendar.",
                    "OK");
                return;
            }

            // Update page size and orientation
            _project.PageSpec.Size = PageSizePicker.SelectedIndex switch
            {
                0 => PageSize.FiveBySeven,
                2 => PageSize.Tabloid_11x17,
                3 => PageSize.SuperB_13x19,
                4 => PageSize.Square_12x12,
                _ => PageSize.Letter
            };

            _project.PageSpec.Orientation = OrientationPicker.SelectedIndex == 0
                ? PageOrientation.Portrait
                : PageOrientation.Landscape;

            // Convert inches back to points and update project
            _project.Margins.TopPt = TopMarginSlider.Value * 72.0;
            _project.Margins.BottomPt = BottomMarginSlider.Value * 72.0;
            _project.Margins.LeftPt = LeftMarginSlider.Value * 72.0;
            _project.Margins.RightPt = RightMarginSlider.Value * 72.0;

            // Update borderless calendar settings
            _project.CoverSpec.BorderlessCalendar = BorderlessCalendarCheckbox.IsChecked;
            _project.CoverSpec.CalendarTopPaddingPt = CalendarTopPaddingSlider.Value * 72.0;
            _project.CoverSpec.CalendarSidePaddingPt = CalendarSidePaddingSlider.Value * 72.0;
            _project.CoverSpec.CalendarBottomPaddingPt = CalendarBottomPaddingSlider.Value * 72.0;

            // Margins are intentionally left untouched here: the renderers ignore them while
            // borderless mode is active, so the user's margin values survive toggling it off.
            _project.CoverSpec.BorderlessFrontCover = _project.CoverSpec.BorderlessCalendar;
            _project.CoverSpec.BorderlessBackCover = _project.CoverSpec.BorderlessCalendar;

            // Update calendar settings
            _project.SetDateRange(start.Year, start.Month, end.Year, end.Month);
            _project.FirstDayOfWeek = (DayOfWeek)FirstDayOfWeekPicker.SelectedIndex;
            _project.EnableDoubleSided = DoubleSidedCheckbox.IsChecked;

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
