using CalendarMaker_MAUI.Services;
using CalendarMaker_MAUI.ViewModels;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace CalendarMaker_MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // DI registrations
        builder.Services.AddSingleton<ICalendarEngine, CalendarEngine>();
        builder.Services.AddSingleton<ITemplateService, TemplateService>();
        builder.Services.AddSingleton<ILayoutService, LayoutService>();
        builder.Services.AddSingleton<IProjectStorageService, ProjectStorageService>();
        builder.Services.AddSingleton<IAssetService, AssetService>();
        builder.Services.AddSingleton<IPdfExportService, PdfExportService>();
        
        // Rendering services (Phase 2)
        builder.Services.AddSingleton<ILayoutCalculator, LayoutCalculator>();
        builder.Services.AddSingleton<ICalendarRenderer, CalendarRenderer>();
        builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
     
        // Foundation services (Phase 1)
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IFilePickerService, FilePickerService>();

        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<Views.ProjectsPage>();
        builder.Services.AddTransient<DesignerViewModel>();
        builder.Services.AddTransient<Views.DesignerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        AppServices.Services = app.Services;
        return app;
    }
}