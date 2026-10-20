using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class SettingsPage : ContentView
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is SettingsViewModel vm)
        {
            vm.OnDeleteVehicleConfirmationRequested = async vehicle =>
            {
                var page = Application.Current?.MainPage;
                return page != null && await page.DisplayAlert(
                    "Delete vehicle",
                    $"Delete {vehicle.MakeModel} and all of its service, fuel, and schedule data?",
                    "Delete",
                    "Cancel");
            };
        }
    }
}
