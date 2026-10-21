using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NutriLens.Helpers;
using NutriLens.Models;
using NutriLens.Services;

namespace NutriLens.ViewModels;

public partial class ReviewIngredientsViewModel : ObservableObject
{
    private static readonly Regex IngredientsHeadingPattern = new(
        @"(?im)^[ \t]*(?:#{1,6}[ \t]*)?(?:[-*•][ \t]*)?(?:\d+[.)][ \t]*)?(?:\*{1,2}[ \t]*)?ingredients?(?:[ \t]+(?:list|statement))?[ \t]*:?[ \t]*(?:\*{1,2})?[ \t]*",
        RegexOptions.Compiled);
    private static readonly Regex NutritionFactsHeadingPattern = new(
        @"(?im)^[ \t]*(?:#{1,6}[ \t]*)?(?:\*{1,2}[ \t]*)?nutrition[ \t]+facts(?:[ \t]*\([^)]*\))?[ \t]*:?[ \t]*(?:\*{1,2})?[ \t]*",
        RegexOptions.Compiled);
    private readonly IAzureOpenAIIngredientService analysisService;
    private readonly IUserPreferenceStore preferenceStore;
    private string? scannedImagePath;
    private string extractedLabelText = string.Empty;

    public ObservableCollection<IngredientItem> Ingredients { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isAddIngredientPopupVisible;

    [ObservableProperty]
    private string newIngredientName = string.Empty;

    [ObservableProperty]
    private string newIngredientDescription = string.Empty;

    [ObservableProperty]
    private string analyzingStatus = string.Empty;

    public int IngredientCount => Ingredients.Count;
    public bool HasIngredients => IngredientCount > 0;

    public string IngredientSummary => $"{IngredientCount} Components Found";
    public ImageSource? ScannedImage { get; private set; }
    public string BackIcon => MaterialIcons.ArrowBack;
    public string PersonIcon => MaterialIcons.Person;
    public string AddIcon => MaterialIcons.AddCircleOutline;
    public string CloseIcon => MaterialIcons.Close;
    public string CameraIcon => MaterialIcons.PhotoCamera;
    public string ForwardIcon => MaterialIcons.ArrowForward;

    public ReviewIngredientsViewModel()
        : this(
            Resolve<IAzureOpenAIIngredientService>() ?? new AzureOpenAIIngredientService(),
            Resolve<IUserPreferenceStore>() ?? new UserPreferenceStore())
    {
    }

    public ReviewIngredientsViewModel(
        IAzureOpenAIIngredientService analysisService,
        IUserPreferenceStore preferenceStore)
    {
        this.analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        this.preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));

        Ingredients.CollectionChanged += (_, _) => RefreshNumbers();
    }

    public void HydrateFromPendingReview()
    {
        var pending = AnalysisNavigationData.PendingReview;
        if (pending is null)
            return;
        extractedLabelText = pending.ExtractedText;

        scannedImagePath = pending.Image.FullPath;
        ScannedImage = CreateImageSource(pending.Image);
        OnPropertyChanged(nameof(ScannedImage));
        AnalysisNavigationData.PendingReview = null;

        Ingredients.Clear();

        foreach (var item in ParseIngredients(pending.ExtractedText))
            Ingredients.Add(item);

        RefreshNumbers();
    }
    private static ImageSource? CreateImageSource(FileResult image)
    {
        if (image is null)
            return null;

        if (!string.IsNullOrWhiteSpace(image.FullPath) &&
            File.Exists(image.FullPath))
        {
            return ImageSource.FromFile(image.FullPath);
        }

        return ImageSource.FromStream(async cancellationToken =>
        {
            var stream = await image.OpenReadAsync();
            return stream;
        });
    }
    private static IEnumerable<IngredientItem> ParseIngredients(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var ingredientLines = NormalizeExtractedText(text)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .SelectMany(SplitTopLevelIngredients)
            .ToList();

        foreach (var raw in ingredientLines)
        {
            var cleaned = Regex.Replace(raw, @"^\s*(?:\d+[.\)]|-|\*|•)\s*", "").Trim();

            if (string.IsNullOrWhiteSpace(cleaned) ||
                cleaned.Equals("Not visible", StringComparison.OrdinalIgnoreCase))
                continue;

            var (name, description) = SplitIngredient(cleaned);

            if (string.IsNullOrWhiteSpace(name))
                continue;

            yield return new IngredientItem
            {
                IngredientName = name.Trim(),
                IngredientDescription = description?.Trim() ?? string.Empty
            };

        }

        foreach (var raw in ExtractNutritionFacts(text)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = Regex.Replace(raw.Trim(), @"^(?:[-*•]\s*)+", "");
            if (string.IsNullOrWhiteSpace(cleaned) ||
                cleaned.Equals("Not visible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var (name, value) = SplitNutritionRow(cleaned);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            yield return new IngredientItem
            {
                IngredientName = name,
                IngredientDescription = value
            };

        }
    }

    private static (string Name, string Value) SplitNutritionRow(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
            return (line[..colonIndex].Trim(), line[(colonIndex + 1)..].Trim());

        var valueMatch = Regex.Match(
            line,
            @"\s(?=(?:<|>)?\d+(?:[.,]\d+)?\s*(?:mg|mcg|g|kcal|calories?|iu|%))",
            RegexOptions.IgnoreCase);

        if (!valueMatch.Success && Regex.IsMatch(line, @"^(?:serving size|servings per container|calories)\b", RegexOptions.IgnoreCase))
            valueMatch = Regex.Match(line, @"\s(?=(?:<|>)?\d)");

        return valueMatch.Success
            ? (line[..valueMatch.Index].Trim(), line[valueMatch.Index..].Trim())
            : (line.Trim(), string.Empty);
    }

    private static string NormalizeExtractedText(string text)
    {
        var ingredientsHeading = IngredientsHeadingPattern.Match(text);
        if (!ingredientsHeading.Success)
            ingredientsHeading = Regex.Match(text, @"\bingredients?\s*:\s*", RegexOptions.IgnoreCase);

        if (!ingredientsHeading.Success)
            return string.Empty;

        var contentStart = ingredientsHeading.Index + ingredientsHeading.Length;
        var nutritionHeading = NutritionFactsHeadingPattern.Match(text, contentStart);
        if (!nutritionHeading.Success)
            nutritionHeading = Regex.Match(text, @"\bnutrition\s+facts\s*:?");

        var contentEnd = nutritionHeading.Success && nutritionHeading.Index >= contentStart
            ? nutritionHeading.Index
            : text.Length;

        return text[contentStart..contentEnd].Trim();
    }

    private static IEnumerable<string> SplitTopLevelIngredients(string line)
    {
        var nestingDepth = 0;
        var start = 0;

        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] is '(' or '[')
                nestingDepth++;
            else if (line[index] is ')' or ']')
                nestingDepth = Math.Max(0, nestingDepth - 1);
            else if ((line[index] is ',' or ';') && nestingDepth == 0)
            {
                var ingredient = line[start..index].Trim();
                if (ingredient.Length > 0)
                    yield return ingredient;
                start = index + 1;
            }
        }

        var lastIngredient = line[start..].Trim();
        if (lastIngredient.Length > 0)
            yield return lastIngredient;
    }

    private static (string Name, string? Description) SplitIngredient(string line)
    {
        var parenMatch = Regex.Match(line, @"^(.*?)\s*\((.+)\)\s*$");
        if (parenMatch.Success)
            return (parenMatch.Groups[1].Value, parenMatch.Groups[2].Value);

        var sepMatch = Regex.Match(line, @"^(.*?)\s*(?:—|–|-|:)\s*(.+)$");
        if (sepMatch.Success && sepMatch.Groups[1].Value.Trim().Length > 0)
            return (sepMatch.Groups[1].Value, sepMatch.Groups[2].Value);

        return (line, null);
    }

    private void RefreshNumbers()
    {
        OnPropertyChanged(nameof(IngredientCount));
        OnPropertyChanged(nameof(IngredientSummary));
        OnPropertyChanged(nameof(HasIngredients));
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        Ingredients.Clear();
        await AppNavigator.PopAsync();
    }

    [RelayCommand]
    private async Task DeleteIngredientAsync(IngredientItem? item)
    {
        if (item is null)
            return;

        Ingredients.Remove(item);
        RefreshNumbers();
         
    }

    [RelayCommand]
    private async Task AddIngredientAsync()
    {
        var name = (NewIngredientName ?? string.Empty).Trim();
        var desc = (NewIngredientDescription ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            await AppNavigator.ShowAlertAsync("Validation", "Ingredient name cannot be empty.", "OK");
            return;
        }

        if (Ingredients.Any(x =>
                string.Equals(x.IngredientName?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            await AppNavigator.ShowAlertAsync("Duplicate", "This ingredient already exists.", "OK");
            return;
        }

        Ingredients.Add(new IngredientItem
        {
            IngredientName = name,
            IngredientDescription = desc
        });

        RefreshNumbers();

        NewIngredientName = string.Empty;
        NewIngredientDescription = string.Empty;
        IsAddIngredientPopupVisible = false;
    }

    [RelayCommand]
    private void ShowAddIngredientPopup()
    {
        NewIngredientName = string.Empty;
        NewIngredientDescription = string.Empty;
        IsAddIngredientPopupVisible = true;
    }

    [RelayCommand]
    private void CancelAddIngredient()
    {
        IsAddIngredientPopupVisible = false;
    }

    [RelayCommand]
    private async Task RetakeAsync()
    {  
        Ingredients.Clear();
        await AppNavigator.GoScanAsync();
    }

    [RelayCommand]
    private async Task OpenProfileAsync() => await AppNavigator.GoProfileAsync();

    [RelayCommand]
    private async Task AnalyzeNowAsync()
    {
        if (IsLoading)
            return;

        if (Ingredients.Count == 0)
        {
            await AppNavigator.ShowAlertAsync("Validation", "At least one ingredient is required.", "OK");
            return;
        }

        try
        {
            IsLoading = true;
            AnalyzingStatus = "Analyzing ingredients...";

            var prefs = preferenceStore.Load();
            if (prefs.IsEmpty)
                prefs = null;

            var combinedIngredients = string.Join('\n', Ingredients.Select(i =>
                string.IsNullOrWhiteSpace(i.IngredientDescription)
                    ? i.IngredientName
                    : $"{i.IngredientName} ({i.IngredientDescription})"));

            var result = await analysisService.AnalyzeAsync(combinedIngredients, prefs);

            if (result is null)
                throw new InvalidOperationException("The AI analysis returned no result.");

            await AppNavigator.GoAnalyzeIngredientsAsync(result, scannedImagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReviewIngredients] Analysis failed: {ex}");
            await AppNavigator.ShowAlertAsync("Analysis Error", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
            AnalyzingStatus = string.Empty;
        }
    }

    private static string ExtractNutritionFacts(string extractedText)
    {
        var heading = NutritionFactsHeadingPattern.Match(extractedText);
        if (!heading.Success)
            heading = Regex.Match(extractedText, @"\bnutrition\s+facts\s*:?\s*", RegexOptions.IgnoreCase);

        return !heading.Success
            ? string.Empty
            : extractedText[(heading.Index + heading.Length)..].Trim();
    }

    private static T? Resolve<T>() where T : class =>
        Application.Current?.Handler?.MauiContext?.Services.GetService<T>();
}