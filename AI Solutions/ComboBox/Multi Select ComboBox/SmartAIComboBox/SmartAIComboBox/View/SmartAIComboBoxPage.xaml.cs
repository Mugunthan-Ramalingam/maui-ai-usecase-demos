namespace SmartAIComboBox;

/// <summary>
/// AI MultiSelect ComboBox page. Wires the search affordance, the search
/// trigger (magnifier tap / Enter key), the clear icon and the dropdown's
/// checkbox state to <see cref="FruitSelectionViewModel"/>. The View never
/// reaches into <see cref="Application.Current"/> or mutates the VM's
/// collections directly.
/// </summary>
public partial class SmartAIComboBoxPage : ContentPage
{
    private readonly FruitSelectionViewModel _viewModel;

    public SmartAIComboBoxPage(FruitSelectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is FruitSelectionViewModel vm)
        {
            vm.AISelectionApplied -= OnAISelectionApplied;
            vm.AISelectionApplied += OnAISelectionApplied;
        }
    }

    /// <summary>Hides the query-hint once the user starts typing.</summary>
    private void OnQueryTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;
        queryInputLayout.ShowHint = string.IsNullOrEmpty(text);
    }

    /// <summary>Enter key while editing the query → fire search.</summary>
    private void OnQueryCompleted(object? sender, EventArgs e) => RunAISearch();

    /// <summary>Search magnifier icon tapped → fire search.</summary>
    private void OnSearchClicked(object? sender, EventArgs e) => RunAISearch();

    /// <summary>Clear (×) button tapped → reset input + combo selections.</summary>
    private void OnClearClicked(object? sender, EventArgs e)
    {
        _viewModel.Clear();
        // UserQuery is now null and Entry two-way bound → queryEntry.Text
        // becomes empty automatically. ShowHint follows TextChanged.
        comboBox.IsDropDownOpen = false;
    }

    private void RunAISearch()
    {
        if (_viewModel.IsBusy) return;
        var query = queryEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query)) return;
        _ = _viewModel.AskAIAsync(query);
    }

    /// <summary>
    /// Opens the dropdown so the (checkbox) ItemTemplate becomes visible
    /// after the VM applied the AI selection. All selection state lives on
    /// the VM.
    /// </summary>
    private void OnAISelectionApplied(object? sender, EventArgs e)
        => comboBox.IsDropDownOpen = true;

    /// <summary>
    /// Fires when the user manually toggles a CheckBox rendered inside a
    /// dropdown row. Delegates the selection update to the VM so the
    /// token display stays in sync.
    /// </summary>
    private void OnDropDownItemCheckBoxChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox cb && cb.BindingContext is FruitModel item)
            _viewModel.SetSelection(item, e.Value);
    }
}
