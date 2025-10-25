using System.Globalization;
using CalendarMaker_MAUI.Models;
using CalendarMaker_MAUI.Services;
using CalendarMaker_MAUI.ViewModels;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace CalendarMaker_MAUI.Views;

[QueryProperty(nameof(ProjectId), "projectId")]
public partial class DesignerPage : ContentPage
{
    private readonly DesignerViewModel _viewModel;
    private readonly ILayoutCalculator _layoutCalculator;
    private readonly ICalendarRenderer _calendarRenderer;
    private readonly SKCanvasView _canvas;

    // Rendering-specific state (kept in View for now)
    private float _pageScale = 1f;
    private float _pageOffsetX, _pageOffsetY;
    private const float DragStartThreshold = 3f;

    public string? ProjectId { get; set; }

    public DesignerPage(
        DesignerViewModel viewModel,
        ILayoutCalculator layoutCalculator,
        ICalendarRenderer calendarRenderer)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _layoutCalculator = layoutCalculator;
        _calendarRenderer = calendarRenderer;
        BindingContext = _viewModel;

        // Initialize canvas
        _canvas = new SKCanvasView { IgnorePixelScaling = false };
        _canvas.PaintSurface += Canvas_PaintSurface;
        _canvas.EnableTouchEvents = true;
        _canvas.Touch += OnCanvasTouch;

        if (CanvasHost != null)
        {
            CanvasHost.Content = _canvas;
        }

        // Subscribe to ViewModel property changes that require canvas refresh
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DesignerViewModel.PageIndex) ||
                e.PropertyName == nameof(DesignerViewModel.Project) ||
                e.PropertyName == nameof(DesignerViewModel.SplitRatio) ||
                e.PropertyName == nameof(DesignerViewModel.ZoomValue) ||
                e.PropertyName == nameof(DesignerViewModel.SelectedPhotoLayoutIndex) ||
                e.PropertyName == nameof(DesignerViewModel.BorderlessChecked))
            {
                _canvas.InvalidateSurface();
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(ProjectId))
        {
            await _viewModel.LoadProjectAsync(ProjectId);
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (!string.IsNullOrEmpty(ProjectId))
        {
            await _viewModel.LoadProjectAsync(ProjectId);
        }
    }

    #region Canvas Rendering

    private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        var project = _viewModel.Project;
        if (project == null)
        {
            return;
        }

        var (pageWpt, pageHpt) = PageSizes.GetPoints(project.PageSpec);
        if (pageWpt <= 0 || pageHpt <= 0)
        {
            pageWpt = 612;
            pageHpt = 792;
        }

        float scale = (float)Math.Min(e.Info.Width / (float)pageWpt, e.Info.Height / (float)pageHpt);
        float offsetX = (e.Info.Width - (float)pageWpt * scale) / 2f;
        float offsetY = (e.Info.Height - (float)pageHpt * scale) / 2f;

        _pageScale = scale;
        _pageOffsetX = offsetX;
        _pageOffsetY = offsetY;

        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);

        // Draw page border
        var pageRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        using var pageBorder = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f / scale
        };
        canvas.DrawRect(pageRect, pageBorder);

        // Calculate content rect
        SKRect contentRect = CalculateContentRect(project, pageWpt, pageHpt);

        // Draw content border if not borderless
        if (!IsBorderless(project))
        {
            using var contentBorder = new SKPaint
            {
                Color = SKColors.Silver,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f / scale
            };
            canvas.DrawRect(contentRect, contentBorder);
        }

        // Render page content
        RenderPageContent(canvas, project, contentRect);

        canvas.Restore();
    }

    private SKRect CalculateContentRect(CalendarProject project, double pageWpt, double pageHpt)
    {
        Margins m = project.Margins;
        SKRect contentRect;

        int pageIndex = _viewModel.PageIndex;

        if (pageIndex == -1 && project.CoverSpec.BorderlessFrontCover)
        {
            contentRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        }
        else if (pageIndex == 12 && project.CoverSpec.BorderlessBackCover)
        {
            contentRect = new SKRect(0, 0, (float)pageWpt, (float)pageHpt);
        }
        else
        {
            contentRect = new SKRect(
                (float)m.LeftPt,
                (float)m.TopPt,
                (float)pageWpt - (float)m.RightPt,
                (float)pageHpt - (float)m.BottomPt);
        }

        // Handle double-sided cover rendering
        if (project.EnableDoubleSided && (pageIndex == -1 || pageIndex == 12))
        {
            if (pageIndex == -1)
            {
                // Front cover - bottom half
                contentRect = new SKRect(
                    contentRect.Left,
                    contentRect.MidY + 2f,
                    contentRect.Right,
                    contentRect.Bottom);
            }
            else
            {
                // Back cover - top half
                contentRect = new SKRect(
                    contentRect.Left,
                    contentRect.Top,
                    contentRect.Right,
                    contentRect.MidY - 2f);
            }
        }

        return contentRect;
    }

    private bool IsBorderless(CalendarProject project)
    {
        int pageIndex = _viewModel.PageIndex;
        return (pageIndex == -1 && project.CoverSpec.BorderlessFrontCover) ||
               (pageIndex == 12 && project.CoverSpec.BorderlessBackCover);
    }

    private void RenderPageContent(SKCanvas canvas, CalendarProject project, SKRect contentRect)
    {
        int pageIndex = _viewModel.PageIndex;
        int activeSlotIndex = _viewModel.ActiveSlotIndex;
        List<SKRect> photoSlots;
        SKRect photoRect;

        if (pageIndex == -1) // Front cover
        {
            PhotoLayout layout = project.FrontCoverPhotoLayout;
            photoSlots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);
            photoRect = contentRect;

            _viewModel.UpdateCachedRenderingData(photoSlots, photoRect, contentRect);

            _calendarRenderer.RenderPhotoSlots(
                canvas,
                photoSlots,
                project.ImageAssets.ToList(),
                "coverPhoto",
                null,
                activeSlotIndex);
        }
        else if (pageIndex == -2) // Previous December
        {
            (photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, project.LayoutSpec);

            int decemberIndex = project.StartMonth == 1 ? 11 : 12 - project.StartMonth;
            PhotoLayout layout = project.MonthPhotoLayouts.TryGetValue(decemberIndex, out PhotoLayout perMonth)
                ? perMonth
                : project.LayoutSpec.PhotoLayout;

            photoSlots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);
            _viewModel.UpdateCachedRenderingData(photoSlots, photoRect, contentRect);

            _calendarRenderer.RenderPhotoSlots(
                canvas,
                photoSlots,
                project.ImageAssets.ToList(),
                "monthPhoto",
                _viewModel.PageIndex,
                activeSlotIndex);

            // Draw calendar for previous year
            _calendarRenderer.RenderCalendarGrid(canvas, calRect, project, project.Year - 1, 12);
        }
        else if (pageIndex == 12) // Back cover
        {
            PhotoLayout layout = project.BackCoverPhotoLayout;
            photoSlots = _layoutCalculator.ComputePhotoSlots(contentRect, layout);
            photoRect = contentRect;

            _viewModel.UpdateCachedRenderingData(photoSlots, photoRect, contentRect);

            _calendarRenderer.RenderPhotoSlots(
                canvas,
                photoSlots,
                project.ImageAssets.ToList(),
                "backCoverPhoto",
                null,
                activeSlotIndex);
        }
        else // Month page
        {
            (photoRect, SKRect calRect) = _layoutCalculator.ComputeSplit(contentRect, project.LayoutSpec);

            PhotoLayout layout = project.MonthPhotoLayouts.TryGetValue(pageIndex, out PhotoLayout perMonth)
                ? perMonth
                : project.LayoutSpec.PhotoLayout;

            photoSlots = _layoutCalculator.ComputePhotoSlots(photoRect, layout);
            _viewModel.UpdateCachedRenderingData(photoSlots, photoRect, contentRect);

            _calendarRenderer.RenderPhotoSlots(
                canvas,
                photoSlots,
                project.ImageAssets.ToList(),
                "monthPhoto",
                pageIndex,
                activeSlotIndex);

            int month = ((project.StartMonth - 1 + pageIndex) % 12) + 1;
            int year = project.Year + (project.StartMonth - 1 + pageIndex) / 12;
            _calendarRenderer.RenderCalendarGrid(canvas, calRect, project, year, month);
        }
    }

    #endregion

    #region Touch Handling

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        CalendarProject? project = _viewModel.Project;
        if (project == null)
        {
            return;
        }

        SKPoint loc = e.Location;
        float density = 1f;

        try
        {
            SKSize canvasSize = _canvas.CanvasSize;
            if (_canvas.Width > 0)
            {
                density = (float)(canvasSize.Width / _canvas.Width);
            }
        }
        catch { }

        SKPoint touchPx = new SKPoint(loc.X * density, loc.Y * density);
        SKPoint pagePt = new SKPoint(
            (touchPx.X - _pageOffsetX) / _pageScale,
            (touchPx.Y - _pageOffsetY) / _pageScale);

        int pageIndex = _viewModel.PageIndex;
        bool isCover = pageIndex == -1 || pageIndex == 12;
        SKRect hitRect = isCover ? _viewModel.LastContentRect : _viewModel.LastPhotoRect;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                HandleTouchPressed(pagePt, hitRect, project, isCover);
                break;

            case SKTouchAction.Moved:
                HandleTouchMoved(pagePt, project, isCover);
                e.Handled = _viewModel.IsDragging;
                return;

            case SKTouchAction.Released:
                HandleTouchReleased(pagePt, hitRect, project);
                e.Handled = _viewModel.IsDragging;
                if (_viewModel.IsDragging)
                {
                    _viewModel.IsDragging = false;
                }
                return;

            case SKTouchAction.Cancelled:
                _viewModel.IsPointerDown = false;
                _viewModel.IsDragging = false;
                break;
        }

        e.Handled = false;
    }

    private void HandleTouchPressed(SKPoint pagePt, SKRect hitRect, CalendarProject project, bool isCover)
    {
        _viewModel.IsPointerDown = true;

        int hitIdx = HitTestSlot(pagePt);
        if (hitIdx >= 0 && hitIdx != _viewModel.ActiveSlotIndex)
        {
            _viewModel.ActiveSlotIndex = hitIdx;
            _canvas.InvalidateSurface();
        }

        SKRect targetRect = GetCurrentTargetRect(hitRect);
        ImageAsset? asset = _viewModel.GetActiveAsset();

        _viewModel.PressedOnAsset = targetRect.Contains(pagePt) && asset != null && File.Exists(asset.Path);
        _viewModel.DragStartPagePoint = pagePt;

        if (_viewModel.PressedOnAsset && asset != null)
        {
            using SKBitmap? bmp = SKBitmap.Decode(asset.Path);
            if (bmp != null)
            {
                (float excessX, float excessY) = CalculateDragExcess(bmp, targetRect, asset);
                _viewModel.DragExcessX = excessX;
                _viewModel.DragExcessY = excessY;
                _viewModel.StartPanX = asset.PanX;
                _viewModel.StartPanY = asset.PanY;
                _viewModel.StartZoom = asset.Zoom;
            }
        }
    }

    private void HandleTouchMoved(SKPoint pagePt, CalendarProject project, bool isCover)
    {
        if (!_viewModel.IsPointerDown || !_viewModel.PressedOnAsset)
        {
            return;
        }

        ImageAsset? asset = _viewModel.GetActiveAsset();
        if (asset == null)
        {
            return;
        }

        float dx = pagePt.X - _viewModel.DragStartPagePoint.X;
        float dy = pagePt.Y - _viewModel.DragStartPagePoint.Y;

        if (!_viewModel.IsDragging)
        {
            if (Math.Abs(dx) > DragStartThreshold || Math.Abs(dy) > DragStartThreshold)
            {
                _viewModel.IsDragging = true;
            }
        }

        if (_viewModel.IsDragging)
        {
            double newPanX = _viewModel.StartPanX + (_viewModel.DragExcessX > 0 ? dx / _viewModel.DragExcessX : 0);
            double newPanY = _viewModel.StartPanY + (_viewModel.DragExcessY > 0 ? dy / _viewModel.DragExcessY : 0);
            asset.PanX = Math.Clamp(newPanX, -1, 1);
            asset.PanY = Math.Clamp(newPanY, -1, 1);
            _canvas.InvalidateSurface();
        }
    }

    private void HandleTouchReleased(SKPoint pagePt, SKRect hitRect, CalendarProject project)
    {
        _viewModel.IsPointerDown = false;

        if (_viewModel.IsDragging)
        {
            // Save project on drag end
            _viewModel.SaveProjectCommand?.Execute(null);
            return;
        }

        SKRect targetRect = GetCurrentTargetRect(hitRect);
        if (targetRect.Contains(pagePt))
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _viewModel.LastTapAt).TotalMilliseconds < 300 &&
                SKPoint.Distance(pagePt, _viewModel.LastTapPoint) < 10)
            {
                // Execute command using Task.Run to avoid async warning
                _ = Task.Run(async () => await ((AsyncRelayCommand)_viewModel.ShowPhotoSelectorCommand).ExecuteAsync(null));
            }

            _viewModel.LastTapAt = now;
            _viewModel.LastTapPoint = pagePt;
        }
    }

    private int HitTestSlot(SKPoint pt)
    {
        List<SKRect> slots = _viewModel.LastPhotoSlots;
        if (slots.Count == 0)
        {
            return -1;
        }

        return slots.FindIndex(r => r.Contains(pt));
    }

    private SKRect GetCurrentTargetRect(SKRect fallback)
    {
        List<SKRect> slots = _viewModel.LastPhotoSlots;
        int activeIndex = _viewModel.ActiveSlotIndex;

        return slots.Count > activeIndex ? slots[activeIndex] : fallback;
    }

    private (float excessX, float excessY) CalculateDragExcess(SKBitmap bmp, SKRect rect, ImageAsset asset)
    {
        float imgW = bmp.Width;
        float imgH = bmp.Height;
        float rectW = rect.Width;
        float rectH = rect.Height;
        float imgAspect = imgW / imgH;
        float rectAspect = rectW / rectH;

        float baseScale = imgAspect > rectAspect ? rectH / imgH : rectW / imgW;
        float scale = baseScale * (float)Math.Clamp(asset.Zoom <= 0 ? 1 : asset.Zoom, 0.5, 3.0);
        float targetW = imgW * scale;
        float targetH = imgH * scale;

        return (
            Math.Max(0, (targetW - rectW) / 2f),
            Math.Max(0, (targetH - rectH) / 2f)
        );
    }

    #endregion

#if WINDOWS
    private void OnCanvasKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        ImageAsset? asset = _viewModel.GetActiveAsset();
        if (asset == null)
        {
            return;
        }

        double step = 0.05;
        bool handled = false;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                asset.PanX = Math.Clamp(asset.PanX - step, -1, 1);
                handled = true;
                break;
            case Windows.System.VirtualKey.Right:
                asset.PanX = Math.Clamp(asset.PanX + step, -1, 1);
                handled = true;
                break;
            case Windows.System.VirtualKey.Up:
                asset.PanY = Math.Clamp(asset.PanY - step, -1, 1);
                handled = true;
                break;
            case Windows.System.VirtualKey.Down:
                asset.PanY = Math.Clamp(asset.PanY + step, -1, 1);
                handled = true;
                break;
        }

        if (handled)
        {
            _viewModel.SaveProjectCommand?.Execute(null);
            _canvas.InvalidateSurface();
            e.Handled = true;
        }
    }
#endif
}
