using Microsoft.Extensions.DependencyInjection;

namespace SmartRTEFormatter;

/// <summary>
/// Defines the application's Shell navigation and resolves its home page through dependency injection.
/// </summary>
public partial class AppShell : Shell
{
    /// <summary>
    /// Initializes the Shell and configures the home page content.
    /// </summary>
    /// <param name="serviceProvider">The provider used to resolve the home page.</param>
    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        MainShellContent.ContentTemplate =
            new DataTemplate(() => serviceProvider.GetRequiredService<MainPage>());
    }
}
