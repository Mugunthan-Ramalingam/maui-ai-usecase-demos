using Microsoft.Extensions.Logging;
using SmartRTEFormatter.Services;
using Syncfusion.Maui.Core.Hosting;

namespace SmartRTEFormatter
{
    public static class MauiProgram
    {
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

            return builder.Build();
        }
    }
}
