namespace CalendarMaker_MAUI.Models;

public enum LayoutPlacement
{
    PhotoTopCalendarBottom,
    PhotoBottomCalendarTop,
    PhotoLeftCalendarRight,
    PhotoRightCalendarLeft
}

public enum PhotoFillMode
{
    Cover,
    Contain
}

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public enum PhotoLayout
{
    Single,
    TwoVerticalSplit, // two photos side-by-side (vertical divider)
    Grid2x2,          // four photos (2x2 grid)
    TwoHorizontalStack, // 2 photos stacked vertically
    ThreeLeftStack,   // 3 photos: 2 stacked vertically on left, 1 on right
    ThreeRightStack   // 3 photos: 2 stacked vertically on right, 1 on left
}

public sealed class LayoutSpec
{
    public LayoutPlacement Placement { get; set; } = LayoutPlacement.PhotoTopCalendarBottom;
    // 0..1, portion of page dedicated to the first element in Placement order
    public double SplitRatio { get; set; } = 0.5;
    public PhotoFillMode PhotoFill { get; set; } = PhotoFillMode.Cover;
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;

    // How many photo slots and their arrangement inside the photo area
    public PhotoLayout PhotoLayout { get; set; } = PhotoLayout.Single;
}
