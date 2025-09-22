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

public sealed class LayoutSpec
{
    public LayoutPlacement Placement { get; set; } = LayoutPlacement.PhotoTopCalendarBottom;
    // 0..1, portion of page dedicated to the first element in Placement order
    public double SplitRatio { get; set; } = 0.5;
    public PhotoFillMode PhotoFill { get; set; } = PhotoFillMode.Cover;
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
}
