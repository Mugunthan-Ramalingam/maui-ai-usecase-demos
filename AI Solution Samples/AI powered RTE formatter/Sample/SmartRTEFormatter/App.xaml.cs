using SmartRTEFormatter.Services;

namespace SmartRTEFormatter
{
    public partial class App : Application
    {
        private readonly IAIFormattingService formattingService;

        public App(IAIFormattingService formattingService)
        {
            this.formattingService = formattingService;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(new MainPage(formattingService)));
        }
    }
}