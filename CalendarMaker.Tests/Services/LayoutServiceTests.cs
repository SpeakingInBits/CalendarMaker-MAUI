using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="LayoutService.ApplyDefaultPreset"/>, which chooses a sensible
/// default placement and split ratio for a page size + orientation.
/// </summary>
public class LayoutServiceTests
{
    private readonly LayoutService _service = new();

    private static CalendarProject ProjectWith(PageSize size, PageOrientation orientation) => new()
    {
        PageSpec = new PageSpec { Size = size, Orientation = orientation },
        LayoutSpec = new LayoutSpec()
    };

    [Fact]
    public void ApplyDefaultPreset_FiveBySevenLandscape_UsesPhotoLeft()
    {
        var project = ProjectWith(PageSize.FiveBySeven, PageOrientation.Landscape);

        _service.ApplyDefaultPreset(project);

        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoLeftCalendarRight);
        project.LayoutSpec.SplitRatio.Should().Be(0.5);
    }

    [Fact]
    public void ApplyDefaultPreset_LetterPortrait_UsesPhotoTopHalfSplit()
    {
        var project = ProjectWith(PageSize.Letter, PageOrientation.Portrait);

        _service.ApplyDefaultPreset(project);

        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoTopCalendarBottom);
        project.LayoutSpec.SplitRatio.Should().Be(0.5);
    }

    [Fact]
    public void ApplyDefaultPreset_TabloidPortrait_UsesSixtyFortySplit()
    {
        var project = ProjectWith(PageSize.Tabloid_11x17, PageOrientation.Portrait);

        _service.ApplyDefaultPreset(project);

        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoTopCalendarBottom);
        project.LayoutSpec.SplitRatio.Should().Be(0.6);
    }

    [Fact]
    public void ApplyDefaultPreset_SuperBPortrait_UsesSixtyFiveSplit()
    {
        var project = ProjectWith(PageSize.SuperB_13x19, PageOrientation.Portrait);

        _service.ApplyDefaultPreset(project);

        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoTopCalendarBottom);
        project.LayoutSpec.SplitRatio.Should().Be(0.65);
    }

    [Fact]
    public void ApplyDefaultPreset_UnmatchedCombination_FallsBackToPhotoTopHalfSplit()
    {
        // A4 portrait has no explicit rule, so it should hit the sensible fallback.
        var project = ProjectWith(PageSize.A4, PageOrientation.Portrait);

        _service.ApplyDefaultPreset(project);

        project.LayoutSpec.Placement.Should().Be(LayoutPlacement.PhotoTopCalendarBottom);
        project.LayoutSpec.SplitRatio.Should().Be(0.5);
    }
}
