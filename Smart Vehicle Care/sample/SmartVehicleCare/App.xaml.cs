using Microsoft.Extensions.DependencyInjection;
using SmartVehicleCare.Services;

namespace SmartVehicleCare
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Clear any persisted key on every launch so the popup appears fresh each run
            SecureStorage.Remove(AzureOpenAIService.SecureStorageKey);
            AzureOpenAIService.SetApiKey(null);
            // Deployment name is not sensitive — keep it across launches
            AzureOpenAIService.LoadDeploymentName();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new AppShell());
    }
}