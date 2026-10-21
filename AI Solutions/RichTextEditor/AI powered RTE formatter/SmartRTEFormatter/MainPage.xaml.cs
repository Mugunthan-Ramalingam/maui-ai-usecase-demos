namespace SmartRTEFormatter;

/// <summary>
/// Displays the rich text editor and its AI formatting controls.
/// </summary>
public partial class MainPage : ContentPage
{
    /// <summary>
    /// Initializes the page and assigns its view model.
    /// </summary>
    /// <param name="viewModel">The view model that provides page state and commands.</param>
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
