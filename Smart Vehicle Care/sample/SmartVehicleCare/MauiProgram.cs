using Microsoft.Extensions.Logging;
using SmartVehicleCare.Views;
using SmartVehicleCare.ViewModels;
using SmartVehicleCare.Services;
using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Licensing;

namespace SmartVehicleCare
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JAaF5cX2pCd0x1WmFZfVhgc19EZVZSQGYuP1ZhSXxVdk1jXX9ZcnFWQ2BdU0N9XEY=");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                    // TODO: Add Inter-Regular.ttf and Inter-SemiBold.ttf to Resources/Fonts for PDS typography compliance
                });

            // Register services as singletons so they can be injected in future
            builder.Services.AddSingleton<VehicleDataService>(_ => VehicleDataService.Instance);

            // Register pages and ViewModels
            builder.Services.AddSingleton<SplashPage>();
            builder.Services.AddSingleton<WelcomePage>();
            builder.Services.AddSingleton<WelcomeViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<AIAssistPage>();
            builder.Services.AddTransient<AIAssistViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
