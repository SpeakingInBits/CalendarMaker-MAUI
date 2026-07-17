using CalendarMaker_MAUI.Models;
using FluentAssertions;

namespace CalendarMaker.Tests.Models;

/// <summary>
/// Tests for computed members on <see cref="CalendarProject"/>.
/// </summary>
public class CalendarProjectTests
{
    private static CalendarProject WithSize(PageSize size) => new()
    {
        PageSpec = new PageSpec { Size = size }
    };

    [Theory]
    [InlineData(PageSize.FiveBySeven, "5x7")]
    [InlineData(PageSize.Letter, "Letter")]
    [InlineData(PageSize.Tabloid_11x17, "11x17")]
    [InlineData(PageSize.SuperB_13x19, "13x19")]
    public void PageSizeDisplay_KnownSizes_ReturnFriendlyLabels(PageSize size, string expected)
    {
        WithSize(size).PageSizeDisplay.Should().Be(expected);
    }

    [Fact]
    public void PageSizeDisplay_UnmappedSize_FallsBackToEnumName()
    {
        // A4 has no explicit label and should fall back to the enum name.
        WithSize(PageSize.A4).PageSizeDisplay.Should().Be("A4");
    }

    [Fact]
    public void NewProject_HasInitializedCollectionsAndDefaults()
    {
        var project = new CalendarProject();

        project.ImageAssets.Should().NotBeNull().And.BeEmpty();
        project.MonthPhotoLayouts.Should().NotBeNull().And.BeEmpty();
        project.PageSpec.Should().NotBeNull();
        project.LayoutSpec.Should().NotBeNull();
        project.Id.Should().NotBeNullOrWhiteSpace();
    }
}
