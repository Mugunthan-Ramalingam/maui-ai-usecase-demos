using NutriLens.Helpers;
using NutriLens.ViewModels;

namespace NutriLens.Views;

public partial class TrendsPage : ContentPage
{
    public TrendsPage()
        : this(new TrendsViewModel())
    {
    }

    public TrendsPage(TrendsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is TrendsViewModel vm)
                await vm.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Trends] Load failed: {ex}");
            await DisplayAlertAsync("Trends", "Scan trends could not be loaded.", "OK");
        }
    }

    private async void OnHomeClicked(
        object? sender,
        EventArgs e)
    {
        await AppNavigator.RunNavigationSafelyAsync(
            this,
            AppNavigator.GoDashboardAsync,
            "Dashboard");
    }

    private async void OnHistoryClicked(
        object? sender,
        EventArgs e)
    {
        await AppNavigator.RunNavigationSafelyAsync(
            this,
            AppNavigator.GoHistoryAsync,
            "History");
    }

    private async void OnScanClicked(
        object? sender,
        TappedEventArgs e)
    {
        await AppNavigator.RunNavigationSafelyAsync(
            this,
            AppNavigator.GoScanAsync,
            "Scan");
    }

    private async void OnProfileClicked(
        object? sender,
        EventArgs e)
    {
        await AppNavigator.RunNavigationSafelyAsync(
            this,
            AppNavigator.GoProfileAsync,
            "Profile");
    }
}