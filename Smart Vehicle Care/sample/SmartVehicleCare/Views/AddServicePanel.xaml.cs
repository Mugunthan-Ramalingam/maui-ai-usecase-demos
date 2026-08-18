using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class AddServicePanel : ContentView
{
    public AddServicePanel()
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

    private void OnServiceDateFieldTapped(object sender, TappedEventArgs e)
        => ServiceDatePicker.IsOpen = true;

    private void OnServiceDateOkClicked(object sender, EventArgs e)
    {
        if (ServiceDatePicker.SelectedDate.HasValue && BindingContext is AddServiceViewModel vm)
            vm.ServiceDate = ServiceDatePicker.SelectedDate.Value;
    }
}
