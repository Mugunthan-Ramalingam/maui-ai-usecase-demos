using Foundation;

namespace SmartRTEFormatter
{
    /// <summary>
    /// Provides the Mac Catalyst application entry point.
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
