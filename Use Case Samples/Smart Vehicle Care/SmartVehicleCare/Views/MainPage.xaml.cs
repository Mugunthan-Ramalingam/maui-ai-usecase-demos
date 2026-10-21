using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        SizeChanged += OnSizeChanged;

        // Wire quick-action alert events (view owns the dialogs, MVVM-compliant)
        viewModel.OnShowOilChangeAlert += () => _ = DisplayAlert(
            "Oil Change", "Next oil change scheduled on Jun 27.\nEstimated cost: ₹1,200.", "OK");
        viewModel.OnShowLastFuelAlert += () => _ = DisplayAlert(
            "Last Fuel Log", "Last refuel: 3 days ago.\nAmount: 30 L · ₹2,850.", "OK");
        viewModel.OnShowAddActionAlert += () => _ = DisplayAlert(
            "Add Action", "Log a new action such as fuel or service.", "OK");
    }

    private MainViewModel VM => (MainViewModel)BindingContext;

    // Re-check API key prompt every time the page appears (e.g., after Welcome redirect)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCompactDesktopState();
        VM.ResetToDashboard();
        _ = VM.CheckApiKeyPromptAsync();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        UpdateCompactDesktopState();
    }

    private void UpdateCompactDesktopState()
    {
        VM.IsCompactDesktop = DeviceInfo.Platform == DevicePlatform.WinUI && Width > 0 && Width < 700;
    }

}
