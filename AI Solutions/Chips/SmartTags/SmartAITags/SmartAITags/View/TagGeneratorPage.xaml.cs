namespace SmartAITags;

/// <summary>
/// Tag generator page. Wires the Entry clear icon, the Generate Tags button,
/// the four-state result panel (empty / loading / chips / error) and the
/// chip-stagger animation. The View never reaches into
/// <see cref="Application.Current"/> or mutates the VM's collections
/// directly — it only calls <see cref="TagViewModel.AppendTag"/> between
/// visual delays.
/// </summary>
public partial class TagGeneratorPage : ContentPage
{
    private readonly TagViewModel _viewModel;
    private CancellationTokenSource? _staggerCts;

    public TagGeneratorPage(TagViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is TagViewModel vm)
        {
            vm.TagsReady -= OnTagsReady;
            vm.TagsReady += OnTagsReady;
        }
    }

    /// <summary>Hides the query-hint once the user starts typing.</summary>
    private void OnQueryTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;
        queryInputLayout.ShowHint = string.IsNullOrEmpty(text);
    }

    /// <summary>Enter / Return while editing → fire Generate.</summary>
    private void OnQueryCompleted(object? sender, EventArgs e) => RunAISearch();

    /// <summary>Clear (×) icon tapped → reset everything via the VM.</summary>
    private void OnClearClicked(object? sender, EventArgs e)
    {
        // Cancel any in-flight stagger so a previous run can't keep appending
        // chips after the user cleared.
        _staggerCts?.Cancel();
        _viewModel.Clear();
        // UserQuery is now null and Entry two-way bound → queryEditor.Text
        // becomes empty automatically. ShowHint follows TextChanged.
    }

    /// <summary>"Try again" affordance on the error banner.</summary>
    private void OnRetryClicked(object? sender, EventArgs e) => RunAISearch();

    private void RunAISearch()
    {
        if (_viewModel.IsBusy) return;
        var query = queryEditor.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query)) return;
        _ = _viewModel.GenerateTagsAsync(query);
    }

    /// <summary>
    /// Adds the AI-returned tags one-by-one with a 120 ms stagger so each
    /// chip visibly appears. <see cref="TagViewModel.IsBusy"/> is already
    /// false by the time this fires (the VM finished its HTTP call), so the
    /// loading indicator is gone and the chip panel is ready.
    /// </summary>
    private async void OnTagsReady(object? sender, IReadOnlyList<string> tags)
    {
        // A newer search may have started; cancel any in-flight stagger so
        // stale chips don't trickle in after the user moved on.
        _staggerCts?.Cancel();
        _staggerCts = new CancellationTokenSource();
        var token = _staggerCts.Token;

        try
        {
            foreach (var tag in tags)
            {
                token.ThrowIfCancellationRequested();
                _viewModel.AppendTag(tag);
                await Task.Delay(120, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Newer search superseded this stagger — silent no-op.
        }
    }
}
