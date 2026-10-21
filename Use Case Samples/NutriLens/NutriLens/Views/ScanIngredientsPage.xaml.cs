using NutriLens.ViewModels;

namespace NutriLens.Views;

public partial class ScanIngredientsPage : ContentPage
{
    public ScanIngredientsPage(ScanIngredientsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    } 
}