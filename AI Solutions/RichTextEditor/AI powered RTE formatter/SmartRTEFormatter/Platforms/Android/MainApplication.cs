using Android.App;
using Android.Runtime;

namespace SmartRTEFormatter
{
    /// <summary>
    /// Initializes the MAUI application on Android.
    /// </summary>
    [Application]
    public class MainApplication : MauiApplication
    {
        /// <summary>
        /// Creates the Android application wrapper.
        /// </summary>
        /// <param name="handle">The native Android application handle.</param>
        /// <param name="ownership">Specifies whether this instance owns the native handle.</param>
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
