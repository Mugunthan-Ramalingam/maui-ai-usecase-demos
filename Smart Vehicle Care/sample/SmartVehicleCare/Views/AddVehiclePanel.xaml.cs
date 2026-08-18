using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class AddVehiclePanel : ContentView
{
    public AddVehiclePanel()
    {
        InitializeComponent();

        // Walk up the visual tree on load to extract AddVehicleViewModel from MainViewModel
        this.BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (this.BindingContext is MainViewModel mainVM)
            this.BindingContext = mainVM.AddVehicleViewModel;
    }

    private void OnNumericTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue == null) return;
        var cleaned = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (cleaned != e.NewTextValue)
            entry.Text = cleaned;
    }
}
