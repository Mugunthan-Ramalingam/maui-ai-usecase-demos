using ObjCRuntime;
using UIKit;

namespace SmartRTEFormatter
{
    /// <summary>
    /// Starts the application on iOS.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Starts UIKit with the application's delegate.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the operating system.</param>
        static void Main(string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
