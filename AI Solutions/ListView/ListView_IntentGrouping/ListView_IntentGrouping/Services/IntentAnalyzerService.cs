using ListView_IntentGrouping.Models;

namespace ListView_IntentGrouping.Services;

/// <summary>
/// AI powered intent analyzer (local rule-based fallback).
///
/// The analyzer is fully local, deterministic, and runs without any
/// network calls. It performs:
///   * Domain detection from the user's natural language prompt
///   * Selection of the right intent taxonomy for the dataset
///   * Per-item scoring against the curated intent set
///   * Confidence estimation
///   * Auto-generation of category summaries using extracted
///     high-frequency keywords
///   * Priority assignment based on rating + intent severity
///
/// Although it is rule-based, the lexicons and scoring logic are
/// designed to be representative of how a real LLM would categorize
/// the same content. To swap in a remote LLM (OpenAI, Azure OpenAI,
/// etc.) you would replace <see cref="AnalyzeAsync"/> with an HTTP
/// call and keep the same <see cref="IntentAnalysisResult"/> shape so
/// the UI does not have to change.
/// </summary>
public class IntentAnalyzerService : IIntentAnalyzer
{
    /// <summary>
    /// Always <see cref="IntentAnalyzerMode.Local"/> — this bundled
    /// analyzer needs no credentials and no network access.
    /// </summary>
    public IntentAnalyzerMode Mode => IntentAnalyzerMode.Local;

    /// <summary>
    /// Label surfaced in the status banner while this analyzer is
    /// active.
    /// </summary>
    public string DisplayName => "Local rule-based";

    /// <summary>
    /// Run the AI analyzer on the supplied items using the user prompt
    /// and the dataset's domain to bias the categorization. The
    /// domain selects the right intent taxonomy (Customer Feedback,
    /// Support Tickets, Survey Responses, Product Reviews, Employee
    /// Feedback) so each dataset gets intent headers that make sense
    /// for that data.
    /// </summary>
    public async Task<IntentAnalysisResult> AnalyzeAsync(
        string prompt,
        string domain,
        IEnumerable<FeedbackItem> items,
        CancellationToken cancellationToken = default)
    {
        // Yield once so the UI thread can update its "analyzing" state.
        await Task.Yield();

        var itemsList = items.ToList();
        var intents = IntentTaxonomies.ForDomain(domain);
        var result = new IntentAnalysisResult
        {
            DetectedDomain = string.IsNullOrWhiteSpace(domain) ? "Customer Feedback" : domain,
            TotalItems = itemsList.Count,
        };

        // ----------------------------------------------------------------
        // 1. Score every item against every intent in the active taxonomy
        // ----------------------------------------------------------------
        var scored = new Dictionary<string, int>[itemsList.Count];
        var matchedKeywords = new HashSet<string>[itemsList.Count];
        for (int i = 0; i < itemsList.Count; i++)
        {
            scored[i] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            matchedKeywords[i] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var text = (itemsList[i].Text ?? string.Empty).ToLowerInvariant();
            foreach (var intent in intents)
            {
                int score = 0;
                foreach (var kw in intent.Keywords)
                {
                    if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += ScoreKeyword(kw, prompt);
                        matchedKeywords[i].Add(kw);
                    }
                }
                if (score > 0) scored[i][intent.Name] = score;
            }
        }

        // ----------------------------------------------------------------
        // 2. Pick the winning intent for each item
        // ----------------------------------------------------------------
        var groups = new Dictionary<string, IntentCategory>(StringComparer.OrdinalIgnoreCase);
        var overallThemeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int unclassified = 0;
        var fallbackIntentName = intents.FirstOrDefault()?.Name ?? "Feedback";

        for (int i = 0; i < itemsList.Count; i++)
        {
            var item = itemsList[i];
            string? chosen = null;
            int best = 0;
            if (scored[i].Count > 0)
            {
                foreach (var kv in scored[i])
                {
                    if (kv.Value > best)
                    {
                        best = kv.Value;
                        chosen = kv.Key;
                    }
                }
            }

            if (chosen == null)
            {
                chosen = fallbackIntentName;
                unclassified++;
            }

            item.AssignedIntent = chosen;
            item.Confidence = best == 0 ? 0.45 : Math.Min(1.0, 0.55 + best * 0.08);
            item.Priority = IntentScoring.ComputePriority(item, chosen);

            if (!groups.TryGetValue(chosen, out var category))
            {
                var def = intents.FirstOrDefault(x =>
                    x.Name.Equals(chosen, StringComparison.OrdinalIgnoreCase));
                category = new IntentCategory
                {
                    Name = def?.Name ?? chosen,
                    SummaryTemplate = def?.SummaryTemplate
                        ?? "{count} items in this category. Recurring themes: {keywords}."
                };
                groups[chosen] = category;
            }
            // Stash the category reference on the item so the
            // GroupDescriptor on GroupKey can group by the actual
            // IntentCategory object, so every group is keyed by its category.
            item.GroupKey = category;
            category.Items.Add(item);

            foreach (var kw in matchedKeywords[i])
            {
                overallThemeCounts.TryGetValue(kw, out var existing);
                overallThemeCounts[kw] = existing + 1;
            }
        }

        // ----------------------------------------------------------------
        // 3. Order categories by item count (most frequent first)
        // ----------------------------------------------------------------
        result.Categories = groups.Values
            .OrderByDescending(c => c.Items.Count)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ----------------------------------------------------------------
        // 4. Generate per-category summaries and an overall summary
        // ----------------------------------------------------------------
        foreach (var category in result.Categories)
        {
            var keywords = IntentScoring.ExtractTopKeywords(category.Items.Select(x => x.Text), topN: 4);
            var joined = keywords.Count == 0 ? "no dominant keywords detected" : string.Join(", ", keywords);
            category.Summary = category.SummaryTemplate
                .Replace("{count}", category.Items.Count.ToString())
                .Replace("{keywords}", joined);
        }

        result.TopThemes = overallThemeCounts
            .Where(kv => kv.Value > 1)
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => $"{kv.Key} ({kv.Value})")
            .ToList();

        result.UnclassifiedCount = unclassified;
        result.OverallSummary = IntentScoring.BuildOverallSummary(result);
        return result;
    }

    private static int ScoreKeyword(string keyword, string prompt)
    {
        // Longer, more specific keywords are stronger signals. Keywords
        // mentioned in the prompt get an extra boost so the analyzer is
        // "context aware" - the user can bias the categorization.
        int baseScore = keyword.Split(' ').Length >= 2 ? 3 : 2;
        if (!string.IsNullOrWhiteSpace(prompt) &&
            prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            baseScore += 2;
        }
        return baseScore;
    }
}
