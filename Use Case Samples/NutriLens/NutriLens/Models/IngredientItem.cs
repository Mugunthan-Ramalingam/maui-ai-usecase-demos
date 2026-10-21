using CommunityToolkit.Mvvm.ComponentModel;

namespace NutriLens.Models;

public partial class IngredientItem : ObservableObject
{
    [ObservableProperty]
    private string ingredientName = string.Empty;

    [ObservableProperty]
    private string ingredientDescription = string.Empty;
}