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

        // Wire up events
        CancelBtn.Clicked += OnCancelClicked;
        ApplyBtn.Clicked += OnApplyClicked;
        ResetMarginsBtn.Clicked += OnResetMarginsClicked;
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
