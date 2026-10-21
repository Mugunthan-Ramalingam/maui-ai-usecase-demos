namespace SmartAITags;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Page + ViewModel + service are resolved through DI so the VM is
        // unit-testable without a real HttpClient. AppShell.xaml is
        // intentionally not used: TagGeneratorPage is the only page.
        var services = IPlatformApplication.Current!.Services;
        return new Window(services.GetRequiredService<TagGeneratorPage>());
    }
}
