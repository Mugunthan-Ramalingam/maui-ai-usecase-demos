using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;

namespace SmartRTEFormatter;

/// <summary>
/// Configures and creates the .NET MAUI application.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates the application and registers its services and pages.
    /// </summary>
    /// <returns>The configured MAUI application.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IAIFormattingService, AzureOpenAIFormattingService>();
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AppShell>();

        return builder.Build();
    }
}
