using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;
using Moq;
using SkiaSharp;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Verifies that <see cref="CalendarRenderer"/> reports the day-cell rectangles used to hit-test
/// clicks for the calendar-events feature, and that rendering events does not throw.
/// </summary>
public class CalendarRendererDayCellsTests
{
    private static CalendarRenderer CreateRenderer() =>
        new(new CalendarEngine(), Mock.Of<IImageProcessor>());

    private static (SKBitmap bmp, SKCanvas canvas) CreateCanvas()
    {
        var bmp = new SKBitmap(600, 600);
        return (bmp, new SKCanvas(bmp));
    }

    [Fact]
    public void RenderCalendarGrid_PopulatesOneRectPerDayOfMonth()
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Sunday };
        var cells = new Dictionary<DateTime, SKRect>();
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            renderer.RenderCalendarGrid(canvas, new SKRect(0, 0, 600, 500), project, 2025, 1, cells);
        }

        cells.Should().HaveCount(31); // January has 31 days
        cells.Keys.Should().Contain(new DateTime(2025, 1, 1));
        cells.Keys.Should().Contain(new DateTime(2025, 1, 31));
        cells.Values.Should().OnlyContain(r => r.Width > 0 && r.Height > 0);
    }

    [Fact]
    public void RenderCalendarGrid_DayCellRectsStayWithinBounds()
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Monday };
        var cells = new Dictionary<DateTime, SKRect>();
        var bounds = new SKRect(10, 20, 590, 480);
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            renderer.RenderCalendarGrid(canvas, bounds, project, 2025, 2, cells);
        }

        cells.Should().HaveCount(28); // February 2025 (non-leap)
        cells.Values.Should().OnlyContain(r =>
            r.Left >= bounds.Left - 0.5f &&
            r.Right <= bounds.Right + 0.5f &&
            r.Bottom <= bounds.Bottom + 0.5f);
    }

    [Fact]
    public void RenderCalendarGrid_WithEvents_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Sunday };
        project.Events.Add(new CalendarEvent
        {
            Title = "Holiday 🎉",
            ColorHex = "#59A14F",
            Recurrence = EventRecurrence.Annually,
            Month = 1,
            Day = 1
        });
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            var act = () => renderer.RenderCalendarGrid(canvas, new SKRect(0, 0, 600, 500), project, 2025, 1);
            act.Should().NotThrow();
        }
    }

    [Theory]
    [InlineData("A really long multi word event title that must wrap onto several lines")]
    [InlineData("Supercalifragilisticexpialidociousunbreakablesingleword")] // no spaces -> hard break
    [InlineData("")] // empty title
    [InlineData("Mom's Birthday 🎂 party 🎉")] // emoji in title (font fallback path)
    [InlineData("🏈🎂🎉⭐❤️🎄")] // emoji-only title
    public void RenderCalendarGrid_WithLongOrAwkwardTitle_WrapsWithoutThrowing(string title)
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Sunday };
        project.Events.Add(new CalendarEvent
        {
            Title = title,
            ColorHex = "#4E79A7",
            Recurrence = EventRecurrence.Annually,
            Month = 1,
            Day = 15
        });
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            // Use a narrow grid so day cells are small and wrapping/hard-breaking is exercised.
            var act = () => renderer.RenderCalendarGrid(canvas, new SKRect(0, 0, 240, 260), project, 2025, 1);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void RenderCalendarGrid_ManyEventsOnOneDay_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Sunday };
        for (int i = 0; i < 10; i++)
        {
            project.Events.Add(new CalendarEvent
            {
                Title = $"Event number {i} with a fairly long title",
                ColorHex = "#E15759",
                Recurrence = EventRecurrence.None,
                Year = 2025,
                Month = 1,
                Day = 15
            });
        }
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            var act = () => renderer.RenderCalendarGrid(canvas, new SKRect(0, 0, 600, 500), project, 2025, 1);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void RenderCalendarGrid_NullDayCellBounds_IsAllowed()
    {
        var renderer = CreateRenderer();
        var project = new CalendarProject { FirstDayOfWeek = DayOfWeek.Sunday };
        var (bmp, canvas) = CreateCanvas();

        using (bmp)
        using (canvas)
        {
            var act = () => renderer.RenderCalendarGrid(canvas, new SKRect(0, 0, 600, 500), project, 2025, 3, dayCellBounds: null);
            act.Should().NotThrow();
        }
    }
}
