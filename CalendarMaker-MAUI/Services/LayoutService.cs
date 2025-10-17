namespace CalendarMaker_MAUI.Services;

using CalendarMaker_MAUI.Models;

public interface ILayoutService
{
    void ApplyDefaultPreset(CalendarProject project);
}

public sealed class LayoutService : ILayoutService
{
    public void ApplyDefaultPreset(CalendarProject project)
    {
        // Apply defaults based on page size and orientation
        if (project.PageSpec.Size == Models.PageSize.FiveBySeven && project.PageSpec.Orientation == Models.PageOrientation.Landscape)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoLeftCalendarRight;
            project.LayoutSpec.SplitRatio = 0.5;
        }
        else if (project.PageSpec.Size == Models.PageSize.Letter && project.PageSpec.Orientation == Models.PageOrientation.Portrait)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.5;
        }
        else if (project.PageSpec.Size == Models.PageSize.Tabloid_11x17 && project.PageSpec.Orientation == Models.PageOrientation.Portrait)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.6;
        }
        else if (project.PageSpec.Size == Models.PageSize.Tabloid_11x17 && project.PageSpec.Orientation == Models.PageOrientation.Landscape)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoLeftCalendarRight;
            project.LayoutSpec.SplitRatio = 0.6;
        }
        else if (project.PageSpec.Size == Models.PageSize.SuperB_13x19 && project.PageSpec.Orientation == Models.PageOrientation.Portrait)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.65;
        }
        else if (project.PageSpec.Size == Models.PageSize.SuperB_13x19 && project.PageSpec.Orientation == Models.PageOrientation.Landscape)
        {
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoLeftCalendarRight;
            project.LayoutSpec.SplitRatio = 0.65;
        }
        else
        {
            // Sensible fallback
            project.LayoutSpec.Placement = Models.LayoutPlacement.PhotoTopCalendarBottom;
            project.LayoutSpec.SplitRatio = 0.5;
        }
    }
}
