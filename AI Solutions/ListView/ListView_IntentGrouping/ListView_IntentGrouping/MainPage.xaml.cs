using ListView_IntentGrouping.ViewModels;
using Syncfusion.Maui.ListView;

namespace ListView_IntentGrouping;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        IntentListView.ItemsLayout = DeviceInfo.Current.Idiom == DeviceIdiom.Phone
            ? new LinearLayout()
            : new GridLayout { SpanCount = 2 };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Items.Count == 0)
        {
            await _viewModel.LoadInitialItemsAsync();
        }
    }
}
