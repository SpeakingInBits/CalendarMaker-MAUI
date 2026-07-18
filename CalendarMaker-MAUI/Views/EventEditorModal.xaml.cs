using System.Globalization;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;

namespace CalendarMaker_MAUI.Views;

/// <summary>
/// Modal for adding, viewing, and removing <see cref="CalendarEvent"/>s on a specific calendar day.
/// Mutates the passed project's <see cref="CalendarProject.Events"/> collection directly and raises
/// <see cref="EventsChanged"/> so the caller can persist and refresh the canvas.
/// </summary>
public partial class EventEditorModal : ContentPage
{
    private readonly CalendarProject _project;
    private readonly DateTime _date;
    private string _selectedColor = CalendarEventService.Palette[0];
    private readonly List<Button> _swatchButtons = new();

    /// <summary>Raised whenever an event is added or removed.</summary>
    public event EventHandler? EventsChanged;

    /// <summary>Raised when the user dismisses the modal.</summary>
    public event EventHandler? Closed;

    public EventEditorModal(CalendarProject project, DateTime date)
    {
        InitializeComponent();

        _project = project;
        _date = date.Date;

        HeaderLabel.Text = $"Events — {_date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture)}";

        BuildColorSwatches();
        BuildRecurrenceOptions();
        RefreshExistingEvents();

        AddButton.Clicked += OnAddClicked;
        CloseButton.Clicked += (_, __) => Closed?.Invoke(this, EventArgs.Empty);
    }

    private void BuildColorSwatches()
    {
        foreach (string hex in CalendarEventService.Palette)
        {
            var btn = new Button
            {
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                BackgroundColor = Color.FromArgb(hex),
                BorderColor = Colors.Gray,
                BorderWidth = 1
            };
            btn.Clicked += (_, __) => SelectColor(hex);
            _swatchButtons.Add(btn);
            ColorSwatches.Children.Add(btn);
        }

        SelectColor(_selectedColor);
    }

    private void SelectColor(string hex)
    {
        _selectedColor = hex;
        for (int i = 0; i < _swatchButtons.Count; i++)
        {
            bool selected = string.Equals(CalendarEventService.Palette[i], hex, StringComparison.OrdinalIgnoreCase);
            _swatchButtons[i].BorderColor = selected ? Colors.Black : Colors.Gray;
            _swatchButtons[i].BorderWidth = selected ? 3 : 1;
        }
    }

    private void BuildRecurrenceOptions()
    {
        // Order must match the switch in GetSelectedRecurrence.
        RecurrencePicker.ItemsSource = new List<string>
        {
            $"Only on {_date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}",
            $"Every year on {_date.ToString("MMM d", CultureInfo.InvariantCulture)}",
            $"Every {_date.DayOfWeek}",
            $"The {OrdinalWeek(_date)} {_date.DayOfWeek} of {_date.ToString("MMMM", CultureInfo.InvariantCulture)}"
        };
        RecurrencePicker.SelectedIndex = 0;
    }

    private EventRecurrence GetSelectedRecurrence() => RecurrencePicker.SelectedIndex switch
    {
        1 => EventRecurrence.Annually,
        2 => EventRecurrence.Weekly,
        3 => EventRecurrence.MonthlyByWeekday,
        _ => EventRecurrence.None
    };

    private void OnAddClicked(object? sender, EventArgs e)
    {
        string title = TitleEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            TitleEntry.Focus();
            return;
        }

        var newEvent = CalendarEventService.CreateForDate(
            _date,
            title,
            _selectedColor,
            GetSelectedRecurrence());

        _project.Events.Add(newEvent);
        EventsChanged?.Invoke(this, EventArgs.Empty);

        // Reset the form for the next entry.
        TitleEntry.Text = string.Empty;
        RefreshExistingEvents();
    }

    private void RefreshExistingEvents()
    {
        ExistingEventsContainer.Children.Clear();

        var events = CalendarEventService.GetEventsForDate(_project.Events, _date);
        NoEventsLabel.IsVisible = events.Count == 0;

        foreach (var ev in events)
        {
            ExistingEventsContainer.Children.Add(BuildEventRow(ev));
        }
    }

    private View BuildEventRow(CalendarEvent ev)
    {
        var swatch = new BoxView
        {
            WidthRequest = 16,
            HeightRequest = 16,
            CornerRadius = 8,
            Color = Color.FromArgb(ev.ColorHex),
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = ev.Title,
            VerticalOptions = LayoutOptions.Center
        };

        var recurrence = new Label
        {
            Text = DescribeRecurrence(ev),
            FontSize = 11,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };

        var deleteBtn = new Button
        {
            Text = "Delete",
            FontSize = 12,
            Padding = new Thickness(8, 2),
            HorizontalOptions = LayoutOptions.End
        };
        deleteBtn.Clicked += (_, __) =>
        {
            _project.Events.RemoveAll(x => x.Id == ev.Id);
            EventsChanged?.Invoke(this, EventArgs.Empty);
            RefreshExistingEvents();
        };

        var grid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        grid.Add(swatch, 0, 0);
        grid.Add(label, 1, 0);
        grid.Add(recurrence, 2, 0);
        grid.Add(deleteBtn, 3, 0);
        return grid;
    }

    private static string DescribeRecurrence(CalendarEvent ev) => ev.Recurrence switch
    {
        EventRecurrence.Annually => "Every year",
        EventRecurrence.Weekly => $"Every {ev.Weekday}",
        EventRecurrence.MonthlyByWeekday =>
            $"{OrdinalWeek(ev.WeekOfMonth)} {ev.Weekday} monthly",
        _ => "One time"
    };

    private static string OrdinalWeek(DateTime date) => OrdinalWeek(CalendarEventService.WeekOfMonthFor(date));

    private static string OrdinalWeek(int week) => week switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        4 => "4th",
        _ => "last"
    };
}
