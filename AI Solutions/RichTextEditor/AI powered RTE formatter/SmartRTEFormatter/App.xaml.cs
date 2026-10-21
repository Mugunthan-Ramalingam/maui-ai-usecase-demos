using Microsoft.Extensions.DependencyInjection;

namespace SmartRTEFormatter;

/// <summary>
/// Configures the application and creates its main Shell window.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes the application with its dependency service provider.
    /// </summary>
    /// <param name="serviceProvider">The provider used to resolve the application Shell.</param>
    public App(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        InitializeComponent();
    }

    /// <summary>
    /// Creates the main window containing the application Shell.
    /// </summary>
    /// <param name="activationState">The state used to activate the window, if available.</param>
    /// <returns>The main application window.</returns>
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(serviceProvider.GetRequiredService<AppShell>());
}