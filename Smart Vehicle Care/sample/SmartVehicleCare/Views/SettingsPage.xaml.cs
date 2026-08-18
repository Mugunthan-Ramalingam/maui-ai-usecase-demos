namespace SmartVehicleCare.Views;

public partial class SettingsPage : ContentView
{
    public SettingsPage()
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
}
