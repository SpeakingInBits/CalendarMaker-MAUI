using Microsoft.Extensions.Logging;
using CalendarMaker_MAUI.Services;
using CalendarMaker_MAUI.ViewModels;
using CommunityToolkit.Maui;

namespace CalendarMaker_MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
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
            builder.Services.AddSingleton<IRenderService, RenderService>();
            builder.Services.AddSingleton<IPdfExportService, PdfExportService>();

            builder.Services.AddTransient<ProjectsViewModel>();
            builder.Services.AddTransient<Views.ProjectsPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
