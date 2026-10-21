using Syncfusion.Maui.DataGrid;

namespace DataGrid_Duplicate_Sample;

public partial class MainPage : ContentPage
{
    private readonly ViewModel viewModel;

    public MainPage(ViewModel vm)
    {
        InitializeComponent();
        viewModel = vm;
        BindingContext = vm;
    }

    /// <summary>
    /// Fires after the user finishes editing a cell and the value has been
    /// written to the bound Customer. Triggers the ViewModel's live
    /// duplicate-detection flow for the affected row.
    /// </summary>
    private async void CustomerGrid_CurrentCellEndEdit(object? sender, DataGridCurrentCellEndEditEventArgs e)
    {
        if (e.OldValue is Customer edited)
        {
            await viewModel.OnCellCommittedAsync(edited, CancellationToken.None);
        }
    }
}
