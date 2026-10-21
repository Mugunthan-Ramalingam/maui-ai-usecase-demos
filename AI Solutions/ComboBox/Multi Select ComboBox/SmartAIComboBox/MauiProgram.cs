using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;

namespace SmartAIComboBox;

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

        // AI service + ViewModel + Page are resolved through DI so the VM is
        // unit-testable without a real HttpClient.
        builder.Services.AddSingleton<IFruitSelectionAIService, FruitSelectionAzureAIService>();
        builder.Services.AddSingleton<FruitSelectionViewModel>();
        builder.Services.AddSingleton<SmartAIComboBoxPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
