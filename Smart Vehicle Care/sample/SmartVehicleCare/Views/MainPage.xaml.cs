using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
    }

    private MainViewModel VM => (MainViewModel)BindingContext;

    // Re-check API key prompt every time the page appears (e.g., after Welcome redirect)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = VM.CheckApiKeyPromptAsync();
    }

    private void OnNavDashboardTapped(object sender, TappedEventArgs e)
        => VM.CurrentSection = NavSection.Dashboard;

    private void OnNavVehicleCenterTapped(object sender, TappedEventArgs e)
        => VM.CurrentSection = NavSection.VehicleCenter;

    private void OnNavAIAssistTapped(object sender, TappedEventArgs e)
        => VM.CurrentSection = NavSection.AIAssist;

    private void OnNavServiceCenterTapped(object sender, TappedEventArgs e)
        => VM.CurrentSection = NavSection.ServiceCenter;

    private void OnNavSettingsTapped(object sender, TappedEventArgs e)
        => VM.CurrentSection = NavSection.Settings;

    private async void OnAddVehicleClicked(object sender, EventArgs e)
        => await DisplayAlertAsync("Add Vehicle", "Vehicle management coming soon.", "OK");

    private async void OnOilChangeChipTapped(object sender, TappedEventArgs e)
        => await DisplayAlertAsync("Oil Change", "Next oil change due on Jun 27.\nEstimated cost: \u20b91,200.", "OK");

    private async void OnLastFuelChipTapped(object sender, TappedEventArgs e)
        => await DisplayAlertAsync("Last Fuel Log", "Last refuel: 3 days ago.\nAmount: 30 L \u00b7 \u20b92,850.", "OK");

    private async void OnAddActionChipTapped(object sender, TappedEventArgs e)
        => await DisplayAlertAsync("Add Action", "Log a new action: fuel or service.", "OK");

    private void OnDrawerBackdropTapped(object sender, TappedEventArgs e)
        => VM.IsAddVehicleDrawerVisible = false;

    private void OnBottomSheetBackdropTapped(object sender, TappedEventArgs e)
        => VM.IsAddVehicleBottomSheetVisible = false;
}
