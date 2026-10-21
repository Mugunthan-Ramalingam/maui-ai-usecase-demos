using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace SmartAITags;

/// <summary>
/// Drives AI tag generation using <see cref="IAzureTagService"/>. The VM owns
/// the user query, the result collection, and all transient state (busy,
/// error). The View only renders these via bindings — it never reaches into
/// Application.Current or mutates the VM's collections.
/// </summary>
public class TagViewModel : INotifyPropertyChanged
{
    private readonly IAzureTagService _aiService;

    private string? _userQuery;
    private bool _isBusy;
    private bool _hasSearched;
    private bool _hasTags;
    private string? _errorMessage;
    private CancellationTokenSource? _cts;

    public ObservableCollection<TagModel> Tags { get; }

    public string? UserQuery
    {
        get => _userQuery;
        set
        {
            if (_userQuery == value) return;
            _userQuery = value;
            OnPropertyChanged(nameof(UserQuery));
            OnPropertyChanged(nameof(CanGenerate));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(CanGenerate));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool HasSearched
    {
        get => _hasSearched;
        private set
        {
            if (_hasSearched == value) return;
            _hasSearched = value;
            OnPropertyChanged(nameof(HasSearched));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    /// <summary>Alias kept for XAML compatibility.</summary>
    public bool IsLoading => _isBusy;

    public bool HasTags
    {
        get => _hasTags;
        private set
        {
            if (_hasTags == value) return;
            _hasTags = value;
            OnPropertyChanged(nameof(HasTags));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool ShowEmptyState => !_isBusy && !_hasTags && !_hasSearched;
    public bool CanGenerate => !_isBusy && !string.IsNullOrWhiteSpace(_userQuery);

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public ICommand GenerateTagsCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand DismissErrorCommand { get; }

    /// <summary>
    /// Raised once per successful generation with the canonicalized tag
    /// names. The View listens for this to perform the per-chip stagger
    /// animation (a UI concern) by calling <see cref="AppendTag"/> on the
    /// VM between delays.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? TagsReady;

    public TagViewModel(IAzureTagService aiService)
    {
        // Service is supplied by DI so unit tests can substitute a fake
        // without touching production code. Mirrors the
        // DataGrid_Duplicate_Sample.ViewModel pattern.
        _aiService = aiService;

        Tags = new ObservableCollection<TagModel>();
        Tags.CollectionChanged += (_, _) => HasTags = Tags.Count > 0;

        GenerateTagsCommand = new Command(async () => await GenerateTagsAsync(UserQuery));
        ClearCommand = new Command(Clear);
        DismissErrorCommand = new Command(() => ErrorMessage = null);
    }

    /// <summary>
    /// Append a single tag to <see cref="Tags"/>. Invoked by the View
    /// between stagger delays when handling <see cref="TagsReady"/>.
    /// </summary>
    public void AppendTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        Tags.Add(new TagModel { Name = name.Trim() });
    }

    /// <summary>Called by the View; <paramref name="query"/> is the source of truth.</summary>
    public async Task GenerateTagsAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || _isBusy) return;

        // Authoritative — overwrite whatever the Entry last bound.
        UserQuery = query;

        // Hide stale chips while loading.
        Tags.Clear();
        ErrorMessage = null;

        IsBusy = true;

        // Cancel any in-flight request before starting a new one.
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            // ── Call Azure OpenAI ──
            var completion = await _aiService.GetCompletionAsync(query, token);
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(completion))
                return;

            // Parse + post-filter to the vocabulary so chips are valid keywords only.
            var tags = ParseAndFilterTags(completion);
            TagsReady?.Invoke(this, tags);
        }
        catch (OperationCanceledException)
        {
            // A newer search superseded this one — leave the UI clean.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI tag generation failed: {ex.Message}");
            ErrorMessage = $"AI request failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            HasSearched = true;
        }
    }

    public void Clear()
    {
        UserQuery = null;
        Tags.Clear();
        ErrorMessage = null;
        HasSearched = false;
    }

    /// <summary>
    /// Parses the AI completion and KEEPS only tags that exist (case-insensitive)
    /// in <see cref="AzureOpenAITagService.TagVocabulary"/>. Returns the canonical
    /// TitleCase form. Single source of truth for both prompt and post-filter.
    /// </summary>
    private static List<string> ParseAndFilterTags(string completion)
    {
        if (string.IsNullOrWhiteSpace(completion))
            return new List<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in completion
            .Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) &&
                        !s.Equals("Empty", StringComparison.OrdinalIgnoreCase) &&
                        !s.StartsWith("Here are", StringComparison.OrdinalIgnoreCase)))
        {
            var cleaned = raw
                .Trim('•', '-', '*', '.', ')', '[', ']', ',', ';', ':', '\t')
                .Trim();

            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length > 40)
                continue;

            var canonical = AzureOpenAITagService.TagVocabulary
                .FirstOrDefault(v => string.Equals(v, cleaned, StringComparison.OrdinalIgnoreCase));

            if (canonical != null && seen.Add(canonical))
                result.Add(canonical);

            if (result.Count >= 12) break;
        }

        return result;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
