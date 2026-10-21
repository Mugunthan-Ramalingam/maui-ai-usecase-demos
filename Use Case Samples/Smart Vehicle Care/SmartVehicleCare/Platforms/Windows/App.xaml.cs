using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SmartVehicleCare.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            // Must be registered here (before InitializeComponent) on Windows because
            // MauiXamlInflator=SourceGen causes WinUI to scan Syncfusion assemblies during
            // InitializeComponent, which races against the later MauiProgram registration.
            // Use the same key as in MauiProgram.cs — Syncfusion ignores the second call.
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
