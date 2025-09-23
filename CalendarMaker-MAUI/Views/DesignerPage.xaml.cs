using SkiaSharp.Views.Maui.Controls;

namespace CalendarMaker_MAUI.Views;

public partial class DesignerPage : ContentPage
{
    public DesignerPage()
    {
        InitializeComponent();

        var canvas = new SKCanvasView();
        canvas.PaintSurface += Canvas_PaintSurface;
        CanvasHost.Content = canvas;

        BackBtn.Clicked += async (_, __) => await Shell.Current.GoToAsync("..");
    }

    private void Canvas_PaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.White);

        using var paint = new SkiaSharp.SKPaint
        {
            Color = new SkiaSharp.SKColor(0x51, 0x2B, 0xD4),
            IsAntialias = true,
            StrokeWidth = 4,
            Style = SkiaSharp.SKPaintStyle.Stroke
        };

        // Placeholder: draw a simple border and a split line 50/50 vertically
        var rect = new SkiaSharp.SKRect(20, 20, e.Info.Width - 20, e.Info.Height - 20);
        canvas.DrawRect(rect, paint);
        canvas.DrawLine(rect.MidX, rect.Top, rect.MidX, rect.Bottom, paint);
    }
}
