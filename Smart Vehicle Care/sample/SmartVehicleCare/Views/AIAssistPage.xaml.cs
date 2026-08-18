namespace SmartVehicleCare.Views;

public partial class AIAssistPage : ContentView
{
    public AIAssistPage()
    {
        InitializeComponent();

    }

    // ViewModel lives on the inner RootGrid, not on this ContentView
    private ViewModels.AIAssistViewModel? VM =>
        RootGrid?.BindingContext as ViewModels.AIAssistViewModel;

    // Clear chat every time this view becomes visible (navigation in from any tab)
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(IsVisible) && IsVisible)
            VM?.ClearChat();
    }

}

