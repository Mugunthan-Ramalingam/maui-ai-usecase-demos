using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class AddFuelPanel : ContentView
{
    public AddFuelPanel()
    {
        InitializeComponent();
    }

    private void OnNumericTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue == null) return;
        var cleaned = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (cleaned != e.NewTextValue)
            entry.Text = cleaned;
    }

    private void OnFuelDateFieldTapped(object sender, TappedEventArgs e)
        => FuelDatePicker.IsOpen = true;

    private void OnFuelDateOkClicked(object sender, EventArgs e)
    {
        if (FuelDatePicker.SelectedDate.HasValue && BindingContext is AddFuelViewModel vm)
            vm.FuelDate = FuelDatePicker.SelectedDate.Value;
    }
}
