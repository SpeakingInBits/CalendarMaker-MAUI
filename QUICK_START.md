# Quick Start Guide - Begin Refactoring

This guide will help you get started with the refactoring process immediately.

---

## ?? Step 1: Set Up Test Project (30 minutes)

### Create the Test Project

```bash
# Navigate to solution directory
cd E:\Code\Projects\CalendarMaker-MAUI

# Create test project
dotnet new xunit -n CalendarMaker.Tests

# Add reference to main project
cd CalendarMaker.Tests
dotnet add reference ../CalendarMaker-MAUI/CalendarMaker-MAUI.csproj

# Add necessary packages
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package coverlet.collector
dotnet add package Microsoft.NET.Test.Sdk
```

### Add to Solution
```bash
# Add test project to solution
dotnet sln ../CalendarMaker-MAUI.sln add CalendarMaker.Tests/CalendarMaker.Tests.csproj
```

### Verify Setup
```bash
# Run tests (should show 0 tests)
dotnet test
```

---

## ?? Step 2: Write Your First Tests (1 hour)

### Test CalendarEngine (Easiest Starting Point)

**Create**: `CalendarMaker.Tests/Services/CalendarEngineTests.cs`

```csharp
using CalendarMaker_MAUI.Services;
using FluentAssertions;
using Xunit;

namespace CalendarMaker.Tests.Services;

public class CalendarEngineTests
{
    private readonly CalendarEngine _sut; // System Under Test

    public CalendarEngineTests()
    {
        _sut = new CalendarEngine();
    }

  [Fact]
    public void BuildMonthGrid_January2024_ShouldReturn5Weeks()
    {
        // Arrange
        int year = 2024;
        int month = 1; // January
    var firstDayOfWeek = DayOfWeek.Sunday;

        // Act
        var result = _sut.BuildMonthGrid(year, month, firstDayOfWeek);

        // Assert
   result.Should().HaveCount(5); // January 2024 has 5 weeks
        result[0].Should().HaveCount(7); // Each week has 7 days
    }

    [Fact]
    public void BuildMonthGrid_January2024_FirstDayShouldBeMonday()
    {
 // Arrange
 int year = 2024;
        int month = 1;
        var firstDayOfWeek = DayOfWeek.Sunday;

        // Act
        var result = _sut.BuildMonthGrid(year, month, firstDayOfWeek);

        // Assert
        var firstJanuary = result[0].First(d => d.HasValue && d.Value.Day == 1);
        firstJanuary.Should().NotBeNull();
        firstJanuary.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void BuildMonthGrid_February2024_ShouldBe29Days()
    {
        // Arrange (2024 is a leap year)
   int year = 2024;
        int month = 2;
      var firstDayOfWeek = DayOfWeek.Sunday;

  // Act
        var result = _sut.BuildMonthGrid(year, month, firstDayOfWeek);

        // Assert
        var allDays = result.SelectMany(week => week.Where(d => d.HasValue && d.Value.Month == 2));
   allDays.Should().HaveCount(29);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Saturday)]
    public void BuildMonthGrid_DifferentFirstDayOfWeek_ShouldAdjustGrid(DayOfWeek firstDay)
{
        // Arrange
        int year = 2024;
 int month = 1;

  // Act
        var result = _sut.BuildMonthGrid(year, month, firstDay);

  // Assert
        result.Should().NotBeEmpty();
        // The grid should always have 7 columns (days of week)
      result.All(week => week.Count == 7).Should().BeTrue();
    }

    [Fact]
    public void BuildMonthGrid_EmptyCells_ShouldBeNull()
    {
    // Arrange
        int year = 2024;
      int month = 1;
        var firstDayOfWeek = DayOfWeek.Sunday;

      // Act
        var result = _sut.BuildMonthGrid(year, month, firstDayOfWeek);

        // Assert
        // First week should have null values for days before month starts
        var firstWeek = result[0];
  firstWeek.Should().Contain(d => d == null);
    }
}
```

### Run the Tests
```bash
dotnet test CalendarMaker.Tests
```

Expected output:
```
Passed! - 5 tests passed in 0.5s
```

---

## ?? Step 3: Extract First Service - ILayoutCalculator (2-3 hours)

### 3.1 Create the Interface

**Create**: `CalendarMaker-MAUI/Services/ILayoutCalculator.cs`

```csharp
using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

/// <summary>
/// Service responsible for calculating photo slot positions and layout splits.
/// </summary>
public interface ILayoutCalculator
{
    /// <summary>
    /// Computes photo slot rectangles for a given area and layout type.
    /// </summary>
    List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout);

    /// <summary>
    /// Computes the split between photo and calendar sections.
    /// </summary>
    (SKRect photoRect, SKRect calendarRect) ComputeSplit(SKRect area, LayoutSpec spec);
}
```

### 3.2 Create the Implementation

**Create**: `CalendarMaker-MAUI/Services/LayoutCalculator.cs`

```csharp
using SkiaSharp;
using CalendarMaker_MAUI.Models;

namespace CalendarMaker_MAUI.Services;

public sealed class LayoutCalculator : ILayoutCalculator
{
    private const float SlotGap = 4f;

    public List<SKRect> ComputePhotoSlots(SKRect area, PhotoLayout layout)
    {
      return layout switch
        {
     PhotoLayout.TwoVerticalSplit => ComputeTwoVerticalSplit(area),
     PhotoLayout.Grid2x2 => ComputeGrid2x2(area),
            PhotoLayout.TwoHorizontalStack => ComputeTwoHorizontalStack(area),
            PhotoLayout.ThreeLeftStack => ComputeThreeLeftStack(area),
        PhotoLayout.ThreeRightStack => ComputeThreeRightStack(area),
            _ => new List<SKRect> { area }
        };
    }

    public (SKRect photoRect, SKRect calendarRect) ComputeSplit(SKRect area, LayoutSpec spec)
    {
        float ratio = (float)Math.Clamp(spec.SplitRatio, 0.1, 0.9);

   return spec.Placement switch
        {
            LayoutPlacement.PhotoLeftCalendarRight => (
    new SKRect(area.Left, area.Top, area.Left + area.Width * ratio, area.Bottom),
          new SKRect(area.Left + area.Width * ratio, area.Top, area.Right, area.Bottom)
    ),
            LayoutPlacement.PhotoRightCalendarLeft => (
      new SKRect(area.Left + area.Width * (1 - ratio), area.Top, area.Right, area.Bottom),
  new SKRect(area.Left, area.Top, area.Left + area.Width * (1 - ratio), area.Bottom)
            ),
            LayoutPlacement.PhotoTopCalendarBottom => (
            new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * ratio),
   new SKRect(area.Left, area.Top + area.Height * ratio, area.Right, area.Bottom)
   ),
            LayoutPlacement.PhotoBottomCalendarTop => (
      new SKRect(area.Left, area.Top + area.Height * (1 - ratio), area.Right, area.Bottom),
          new SKRect(area.Left, area.Top, area.Right, area.Top + area.Height * (1 - ratio))
  ),
          _ => (area, area)
 };
    }

    private List<SKRect> ComputeTwoVerticalSplit(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        return new List<SKRect>
        {
 new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom),
 new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Bottom)
        };
    }

    private List<SKRect> ComputeGrid2x2(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
        return new List<SKRect>
        {
   new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Top + halfH),
new SKRect(area.Left, area.Top + halfH + SlotGap, area.Left + halfW, area.Bottom),
          new SKRect(area.Left + halfW + SlotGap, area.Top + halfH + SlotGap, area.Right, area.Bottom)
      };
    }

    private List<SKRect> ComputeTwoHorizontalStack(SKRect area)
{
        float halfH = (area.Height - SlotGap) / 2f;
        return new List<SKRect>
    {
   new SKRect(area.Left, area.Top, area.Right, area.Top + halfH),
         new SKRect(area.Left, area.Top + halfH + SlotGap, area.Right, area.Bottom)
        };
    }

  private List<SKRect> ComputeThreeLeftStack(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
     return new List<SKRect>
        {
          new SKRect(area.Left, area.Top, area.Left + halfW, area.Top + halfH),
            new SKRect(area.Left, area.Top + halfH + SlotGap, area.Left + halfW, area.Bottom),
            new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Bottom)
        };
    }

    private List<SKRect> ComputeThreeRightStack(SKRect area)
    {
        float halfW = (area.Width - SlotGap) / 2f;
        float halfH = (area.Height - SlotGap) / 2f;
        return new List<SKRect>
        {
        new SKRect(area.Left, area.Top, area.Left + halfW, area.Bottom),
      new SKRect(area.Left + halfW + SlotGap, area.Top, area.Right, area.Top + halfH),
       new SKRect(area.Left + halfW + SlotGap, area.Top + halfH + SlotGap, area.Right, area.Bottom)
        };
    }
}
```

### 3.3 Register in DI

**Update**: `CalendarMaker-MAUI/MauiProgram.cs`

```csharp
// Add this line in ConfigureServices:
builder.Services.AddSingleton<ILayoutCalculator, LayoutCalculator>();
```

### 3.4 Write Tests for LayoutCalculator

**Create**: `CalendarMaker.Tests/Services/LayoutCalculatorTests.cs`

```csharp
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace CalendarMaker.Tests.Services;

public class LayoutCalculatorTests
{
    private readonly LayoutCalculator _sut;

    public LayoutCalculatorTests()
    {
        _sut = new LayoutCalculator();
    }

    [Fact]
  public void ComputePhotoSlots_SingleLayout_ReturnsOneSlot()
    {
      // Arrange
  var area = new SKRect(0, 0, 100, 100);
        var layout = PhotoLayout.Single;

        // Act
      var slots = _sut.ComputePhotoSlots(area, layout);

    // Assert
        slots.Should().HaveCount(1);
  slots[0].Should().Be(area);
    }

    [Fact]
    public void ComputePhotoSlots_TwoVerticalSplit_ReturnsTwoEqualSlots()
    {
        // Arrange
        var area = new SKRect(0, 0, 100, 100);
   var layout = PhotoLayout.TwoVerticalSplit;

        // Act
        var slots = _sut.ComputePhotoSlots(area, layout);

        // Assert
   slots.Should().HaveCount(2);
        // First slot should be left half
   slots[0].Left.Should().Be(0);
        slots[0].Width.Should().BeApproximately(48, 1); // (100 - 4) / 2
      
// Second slot should be right half
        slots[1].Right.Should().Be(100);
        slots[1].Width.Should().BeApproximately(48, 1);
    }

    [Fact]
    public void ComputePhotoSlots_Grid2x2_ReturnsFourSlots()
    {
        // Arrange
        var area = new SKRect(0, 0, 100, 100);
  var layout = PhotoLayout.Grid2x2;

      // Act
        var slots = _sut.ComputePhotoSlots(area, layout);

    // Assert
slots.Should().HaveCount(4);
   // All slots should have equal width and height
        var expectedWidth = (100 - 4) / 2f;
        var expectedHeight = (100 - 4) / 2f;
        
        slots.All(s => Math.Abs(s.Width - expectedWidth) < 1).Should().BeTrue();
        slots.All(s => Math.Abs(s.Height - expectedHeight) < 1).Should().BeTrue();
    }

    [Fact]
    public void ComputeSplit_PhotoLeft_CalendarRight_ShouldSplitCorrectly()
    {
        // Arrange
  var area = new SKRect(0, 0, 100, 100);
     var spec = new LayoutSpec
        {
     Placement = LayoutPlacement.PhotoLeftCalendarRight,
    SplitRatio = 0.6 // 60% photo, 40% calendar
        };

     // Act
        var (photoRect, calRect) = _sut.ComputeSplit(area, spec);

        // Assert
      photoRect.Width.Should().BeApproximately(60, 0.1f);
        calRect.Width.Should().BeApproximately(40, 0.1f);
        photoRect.Left.Should().Be(0);
        calRect.Right.Should().Be(100);
    }

    [Theory]
    [InlineData(0.5)] // 50/50 split
    [InlineData(0.3)] // 30/70 split
    [InlineData(0.7)] // 70/30 split
    public void ComputeSplit_VariousRatios_ShouldRespectRatio(double ratio)
    {
        // Arrange
        var area = new SKRect(0, 0, 100, 100);
        var spec = new LayoutSpec
        {
            Placement = LayoutPlacement.PhotoLeftCalendarRight,
   SplitRatio = ratio
   };

      // Act
        var (photoRect, calRect) = _sut.ComputeSplit(area, spec);

        // Assert
 var expectedPhotoWidth = 100 * ratio;
     photoRect.Width.Should().BeApproximately((float)expectedPhotoWidth, 0.1f);
  }
}
```

### 3.5 Run Tests
```bash
dotnet test CalendarMaker.Tests
```

You should now have 10+ passing tests! ?

---

## ?? Step 4: Refactor Existing Code to Use LayoutCalculator (1 hour)

### 4.1 Update PdfExportService

**Find and replace** in `RenderAndExport.cs`:

```csharp
// Add constructor parameter:
private readonly ILayoutCalculator _layoutCalculator;

public PdfExportService(ILayoutCalculator layoutCalculator)
{
    _layoutCalculator = layoutCalculator;
}

// Replace ComputePhotoSlots calls:
// OLD:
var slots = ComputePhotoSlots(photoRect, layout);

// NEW:
var slots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);

// Replace ComputeSplit calls:
// OLD:
(SKRect photoRect, SKRect calRect) = ComputeSplit(contentRect, project.LayoutSpec);

// NEW:
(SKRect photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, project.LayoutSpec);

// DELETE the old ComputePhotoSlots and ComputeSplit methods entirely
```

### 4.2 Update DesignerPage

```csharp
// Add constructor parameter:
private readonly ILayoutCalculator _layoutCalculator;

public DesignerPage(
    ICalendarEngine engine,
    IProjectStorageService storage,
    IAssetService assets,
    IPdfExportService pdf,
    ILayoutCalculator layoutCalculator) // NEW
{
    // ... existing code
    _layoutCalculator = layoutCalculator;
}

// Replace method calls (same as above)
// DELETE the old methods
```

---

## ? Step 5: Verify Everything Works (15 minutes)

### Run All Tests
```bash
dotnet test CalendarMaker.Tests --verbosity detailed
```

### Build the App
```bash
dotnet build CalendarMaker-MAUI
```

### Run the App
```bash
dotnet run --project CalendarMaker-MAUI
```

Test these features:
- [ ] Open existing project
- [ ] Navigate between pages
- [ ] Assign photos to slots
- [ ] Change photo layouts
- [ ] Export a single page
- [ ] Export full year

---

## ?? What You've Accomplished

? **Set up unit testing infrastructure**
? **Written 10+ unit tests**
? **Extracted first service (ILayoutCalculator)**
? **Removed ~100 lines of duplicate code**
? **Improved testability**
? **Maintained 100% backward compatibility**

---

## ?? Next Steps

Now that you have momentum, continue with:

1. **Task 1.1**: Create IDialogService (see TASK_TRACKING.md)
2. **Task 2.1**: Create ICalendarRenderer interface
3. **Task 3.1**: Create DesignerViewModel

Refer to `REFACTORING_PLAN.md` for detailed guidance on each task.

---

## ?? Tips

- **Test first**: Write tests before refactoring when possible
- **Small steps**: Make one change at a time
- **Commit often**: Commit after each working change
- **Run tests frequently**: Run `dotnet test` after every change
- **Ask for help**: Reference the detailed plans when stuck

---

## ?? Troubleshooting

### Tests won't run
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
dotnet test
```

### Can't reference MAUI project
- Ensure the test project targets the same .NET version
- Check that the reference path is correct

### SkiaSharp not found in tests
```bash
dotnet add CalendarMaker.Tests package SkiaSharp
dotnet add CalendarMaker.Tests package SkiaSharp.Views.Maui.Controls
```

---

Good luck! ??
