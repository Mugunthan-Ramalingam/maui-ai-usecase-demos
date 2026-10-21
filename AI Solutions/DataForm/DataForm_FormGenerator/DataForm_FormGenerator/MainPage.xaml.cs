namespace DataForm_FormGenerator;

public partial class MainPage : ContentPage
{
    public MainPage(EducationFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
