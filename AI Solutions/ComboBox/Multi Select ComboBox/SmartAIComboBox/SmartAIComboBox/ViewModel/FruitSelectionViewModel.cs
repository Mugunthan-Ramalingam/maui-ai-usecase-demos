using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace SmartAIComboBox;

/// <summary>
/// Drives the AI-powered fruit-selection sample. Owns the fruit list, the
/// current selection, the user query and all transient state. The View only
/// renders these via bindings — it never reaches into Application.Current or
/// mutates the VM's collections.
/// </summary>
public class FruitSelectionViewModel : INotifyPropertyChanged
{
    private readonly IFruitSelectionAIService _aiService;

    private string? _userQuery;
    private bool _isBusy;
    private bool _hasSearched;
    private string? _errorMessage;

    public ObservableCollection<FruitModel> Fruits { get; }
    public ObservableCollection<FruitModel> SelectedItems { get; } = new();

    public string? UserQuery
    {
        get => _userQuery;
        set { if (_userQuery == value) return; _userQuery = value; OnPropertyChanged(nameof(UserQuery)); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(CanClear));
            OnPropertyChanged(nameof(ComboHint));
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
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(CanClear));
        }
    }

    public bool IsIdle => !_isBusy && !_hasSearched;
    public bool CanClear => _hasSearched && !_isBusy;
    public string ComboHint => _isBusy ? "Loading..." : "Select fruits";

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

    public ICommand AskAICommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand DismissErrorCommand { get; }
    public ICommand ToggleSelectionCommand { get; }

    /// <summary>
    /// Raised when the AI result is ready to be applied to <see cref="Fruits"/> /
    /// <see cref="SelectedItems"/>. The View listens for this to open the
    /// dropdown — selection logic itself lives in
    /// <see cref="ApplyAISelection"/>.
    /// </summary>
    public event EventHandler? AISelectionApplied;

    public FruitSelectionViewModel(IFruitSelectionAIService aiService)
    {
        _aiService = aiService;
        Fruits = new ObservableCollection<FruitModel>(BuildFruitList());

        AskAICommand = new Command(async () => await AskAIAsync(UserQuery));
        ClearCommand = new Command(Clear);
        DismissErrorCommand = new Command(() => ErrorMessage = null);
        ToggleSelectionCommand = new Command<FruitModel>(ToggleSelection);
    }

    public void Clear()
    {
        UserQuery = null;
        foreach (var f in Fruits) f.IsSelected = false;
        SelectedItems.Clear();
        ErrorMessage = null;
        HasSearched = false;
    }

    /// <summary>
    /// Toggles the <see cref="FruitModel.IsSelected"/> flag of a single
    /// fruit and keeps <see cref="SelectedItems"/> in sync. Invoked by the
    /// checkbox in the dropdown row.
    /// </summary>
    public void ToggleSelection(FruitModel? fruit)
    {
        if (fruit is null) return;
        SetSelection(fruit, !fruit.IsSelected);
    }

    public void SetSelection(FruitModel? fruit, bool isSelected)
    {
        if (fruit is null) return;
        fruit.IsSelected = isSelected;
        if (isSelected)
        {
            if (!SelectedItems.Contains(fruit))
                SelectedItems.Add(fruit);
        }
        else
        {
            if (SelectedItems.Contains(fruit))
                SelectedItems.Remove(fruit);
        }
    }

    /// <summary>
    /// Resets every fruit to unchecked and then selects the ones whose
    /// names appear in <paramref name="matchedNames"/>. Owns the selection
    /// state — the View is only notified so it can open the dropdown.
    /// </summary>
    public void ApplyAISelection(IEnumerable<string> matchedNames)
    {
        var nameSet = (matchedNames ?? Enumerable.Empty<string>())
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Reset every checkbox first so a previous run doesn't carry over.
        foreach (var f in Fruits) f.IsSelected = false;
        SelectedItems.Clear();

        // Check + add only the AI matches.
        foreach (var item in Fruits)
        {
            if (item.Name is not null && nameSet.Contains(item.Name))
            {
                item.IsSelected = true;
                SelectedItems.Add(item);
            }
        }

        AISelectionApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 50 fruits. Each carries a rich nutrition/health tag string so the AI
    /// can map ANY natural-language request (including "healthy fruit",
    /// "fruits for immunity", "fruits for glowing skin", etc.) to real fruits.
    /// </summary>
    private static List<FruitModel> BuildFruitList()
    {
        var fruits = new[]
        {
            ("Apple",                120, "Fiber Kids Sweet Healthy Heart Immunity Digestive Skin"),
            ("Banana",                60, "Smoothie Kids Sweet Energy Fiber Healthy Heart Potassium Bone Digestive"),
            ("Mango",                150, "Smoothie Kids Sweet Tropical Healthy Immunity Skin Eye"),
            ("Orange",                80, "VitaminC Water Citrus Kids Healthy Immunity Heart Skin"),
            ("Pomegranate",          200, "Fiber Antioxidant VitaminC Healthy Heart Immunity Skin Iron"),
            ("Papaya",               100, "Water Digestive Tropical Smoothie Healthy Immunity Skin Eye Heart"),
            ("Pineapple",            120, "VitaminC Water Tropical Smoothie Healthy Immunity Digestive"),
            ("Watermelon",            60, "Water Kids Sweet Tropical Healthy Hydrating LowCalorie Heart"),
            ("Muskmelon",             80, "Water Sweet Kids Healthy Hydrating Immunity VitaminC"),
            ("Strawberry",           250, "VitaminC Berry Smoothie Antioxidant Healthy Skin Immunity Heart"),
            ("Blueberry",            400, "Berry Antioxidant Fiber VitaminC Healthy Brain Skin Heart Immunity"),
            ("Raspberry",            350, "Berry Antioxidant Fiber VitaminC Healthy Skin Heart WeightLoss"),
            ("Blackberry",           300, "Berry Antioxidant Fiber Healthy Skin Immunity Brain"),
            ("Grapes",               120, "Kids Sweet Water Healthy Heart Immunity Antioxidant"),
            ("Guava",                 80, "VitaminC Fiber Kids Sweet Healthy Immunity WeightLoss Eye Skin"),
            ("Kiwi",                 250, "VitaminC Fiber Citrus Antioxidant Healthy Immunity Skin Heart Digestive"),
            ("Pear",                 150, "Fiber Sweet Kids Healthy Digestive Heart WeightLoss"),
            ("Peach",                200, "Sweet Fiber Kids Healthy Skin Immunity LowCalorie"),
            ("Plum",                 180, "Sweet Fiber Kids Antioxidant Healthy Digestive Immunity"),
            ("Cherry",               300, "Antioxidant Sweet Kids Healthy Heart Immunity Sleep Brain"),
            ("Lemon",                100, "VitaminC Citrus LowSugar Healthy Immunity Detox Digestive"),
            ("Lime",                 100, "VitaminC Citrus LowSugar Healthy Immunity Detox Digestive"),
            ("Cranberry",            350, "Antioxidant Berry VitaminC Healthy Immunity Digestive Heart"),
            ("Apricot",              250, "Fiber Sweet Antioxidant Healthy Eye Skin Heart Iron"),
            ("Fig",                  300, "Fiber Sweet Antioxidant Digestive Healthy Bone Iron"),
            ("Dates",                200, "Energy Fiber Sweet Kids Healthy Iron Bone Heart"),
            ("Dragon Fruit",         250, "Fiber Antioxidant Water Tropical Healthy Immunity Heart Digestive Skin"),
            ("Passion Fruit",        300, "VitaminC Fiber Citrus Tropical Healthy Immunity Heart Sleep"),
            ("Avocado",              200, "Fiber HealthyFat LowSugar Healthy Heart Brain Skin Eye Potassium"),
            ("Coconut",              100, "Energy Water Tropical Healthy Heart Immunity Skin Hydrating"),
            ("Sweet Lime (Mosambi)",  120, "VitaminC Water Citrus Healthy Immunity Detox Digestive"),
            ("Lychee",               200, "VitaminC Sweet Tropical Kids Healthy Immunity Heart Antioxidant"),
            ("Jamun (Black Plum)",   150, "Antioxidant LowSugar Fiber Healthy Diabetic WeightLoss Immunity"),
            ("Custard Apple (Sitaphal)", 200, "Sweet Kids Energy Fiber Healthy Bone Eye Immunity Heart"),
            ("Jackfruit",            150, "Energy Fiber Tropical Sweet Healthy Immunity Heart Iron"),
            ("Sapodilla (Chikoo)",    120, "Energy Sweet Kids Fiber Healthy Bone Eye Immunity"),
            ("Star Fruit",           200, "VitaminC Water LowSugar Tropical Healthy WeightLoss Heart Immunity"),
            ("Mulberry",             250, "Antioxidant Berry Fiber Healthy Iron Immunity Heart Skin"),
            ("Amla (Indian Gooseberry)", 80, "VitaminC Antioxidant LowSugar Fiber Healthy Immunity Skin Hair Detox Diabetic"),
            ("Banana (Ripe) (Smoothie)", 60, "Smoothie Kids Energy Sweet Healthy Heart Potassium Bone"),
            ("Mango (Ripe) (Smoothie)", 150, "Smoothie Sweet Tropical Kids Healthy Immunity Skin Eye"),
            ("Papaya (Smoothie)",    100, "Smoothie Water Digestive Tropical Healthy Skin Eye Immunity"),
            ("Pineapple (Smoothie)", 120, "Smoothie VitaminC Water Tropical Healthy Immunity Digestive"),
            ("Strawberry (Smoothie)", 250, "Smoothie VitaminC Berry Antioxidant Healthy Skin Heart Immunity"),
            ("Orange (Smoothie)",     80, "Smoothie VitaminC Water Citrus Healthy Immunity Heart Skin"),
            ("Pear (Ripe)",          150, "Fiber Sweet Kids Healthy Digestive Heart WeightLoss"),
            ("Cantaloupe",           100, "Water Sweet Kids VitaminC Healthy Hydrating Immunity Eye Skin"),
            ("Honeydew Melon",       120, "Water Sweet Kids Healthy Hydrating Immunity LowCalorie"),
            ("Tangerine",            150, "VitaminC Citrus Water Sweet Healthy Immunity Skin Heart"),
            ("Nectarine",            200, "Fiber Sweet Kids Antioxidant Healthy Skin Heart Immunity"),
        };

        var list = new List<FruitModel>();
        foreach (var (name, price, tags) in fruits)
        {
            list.Add(new FruitModel
            {
                Name = name.Trim(),
                Price = price,
                Category = "Fruit",
                Diet = tags,
                IsSelected = false
            });
        }
        return list;
    }

    /// <summary>Called by the View; <paramref name="query"/> is the source of truth.</summary>
    public async Task AskAIAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || _isBusy) return;

        // Authoritative — overwrite whatever the Entry last bound.
        UserQuery = query;
        ErrorMessage = null;

        IsBusy = true;
        using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            string menu = string.Join("\n",
                Fruits.Select(f => $"{f.Name} | {f.Diet}"));

            string prompt =
                "From the menu below, choose the fruits that best match the user's natural-language request.\n\n" +
                "Rules:\n" +
                "- Return ONLY the exact fruit Names from the menu, one per line.\n" +
                "- Do NOT include tags, numbers, dashes, bullets, explanations, or any other text.\n" +
                "- Do NOT include headings like 'Here are the items'.\n" +
                "- The tags after each fruit describe its nutrition/health profile. Use BOTH the tags AND your general knowledge of fruits to interpret ANY natural-language request — including vague ones like 'healthy fruit', 'fruits good for health', 'nutritious fruits', 'fruits for weight loss', 'fruits for glowing skin', 'fruits for immunity', 'fruits for energy', 'fruits to eat during fever', etc.\n" +
                "- Tag meanings:\n" +
                "    Healthy, Immunity, Heart, Skin, Eye, Brain, Bone, Hair, Sleep, Detox, Digestive, Diabetic, WeightLoss, LowCalorie, Hydrating, Iron, Potassium, Energy, Fiber, VitaminC, Antioxidant, Water, Smoothie, Kids, Sweet, Citrus, Tropical, Berry, LowSugar, HealthyFat\n" +
                "- Intent examples (NOT exhaustive — generalize to similar requests):\n" +
                "    'healthy fruits' / 'fruits good for health' → fruits tagged Healthy\n" +
                "    'water-rich fruits' / 'hydrating fruits'   → fruits tagged Water or Hydrating\n" +
                "    'vitamin c rich fruits'                     → fruits tagged VitaminC\n" +
                "    'high-fiber fruits'                         → fruits tagged Fiber\n" +
                "    'fruits for smoothies'                       → fruits tagged Smoothie\n" +
                "    'kid-friendly fruits'                        → fruits tagged Kids\n" +
                "    'citrus fruits'                              → fruits tagged Citrus\n" +
                "    'tropical fruits'                            → fruits tagged Tropical\n" +
                "    'berry fruits'                               → fruits tagged Berry\n" +
                "    'low sugar fruits' / 'diabetic fruits'       → fruits tagged LowSugar or Diabetic\n" +
                "    'sweet fruits'                               → fruits tagged Sweet\n" +
                "    'antioxidant rich fruits'                    → fruits tagged Antioxidant\n" +
                "    'energy giving fruits'                       → fruits tagged Energy\n" +
                "    'fruits for digestion'                       → fruits tagged Digestive\n" +
                "    'fruits for immunity'                       → fruits tagged Immunity or VitaminC\n" +
                "    'fruits for glowing skin'                   → fruits tagged Skin or VitaminC\n" +
                "    'fruits for heart health'                  → fruits tagged Heart\n" +
                "    'fruits for eyes / vision'                  → fruits tagged Eye\n" +
                "    'fruits for weight loss'                    → fruits tagged WeightLoss or LowCalorie or Fiber\n" +
                "    'fruits rich in iron'                       → fruits tagged Iron\n" +
                "    'fruits rich in potassium'                  → fruits tagged Potassium\n" +
                "    'fruits for brain / memory'                → fruits tagged Brain\n" +
                "    'fruits for bones'                          → fruits tagged Bone\n" +
                "    'fruits to eat at night' / 'fruits for sleep' → fruits tagged Sleep\n" +
                "- NEVER return 'Empty' for a reasonable natural-language request about fruits. Always pick the closest matching fruits from the menu.\n" +
                "- If multiple constraints are given, return fruits satisfying ALL of them.\n" +
                "- Return at most 10 fruits.\n" +
                "- If ABSOLUTELY no fruit matches (e.g. user asks for a non-fruit), return exactly: Empty\n\n" +
                "Menu (Name | Tags):\n" + menu + "\n\n" +
                "User request: " + query + "\n\n" +
                "Matching fruits (one per line, exact names from the menu):";

            var completion = await _aiService.GetCompletionAsync(prompt, requestCts.Token)
                .WaitAsync(requestCts.Token);
            requestCts.Token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(completion))
                return;

            // Parse — tolerant to bullets/dashes/headers.
            var matches = completion
                .Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s) &&
                            !s.Equals("Empty", StringComparison.OrdinalIgnoreCase) &&
                            !s.StartsWith("Here are", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Trim('•', '-', '*', '.', ')'))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            ApplyAISelection(matches);
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
        {
            ErrorMessage = "AI request timed out after 30 seconds. Please try again.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "AI request was canceled. Please try again.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI selection failed: {ex.Message}");
            ErrorMessage = $"AI request failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            HasSearched = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
