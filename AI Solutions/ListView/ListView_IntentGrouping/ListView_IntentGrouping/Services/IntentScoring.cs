using System.Text.RegularExpressions;
using ListView_IntentGrouping.Models;

namespace ListView_IntentGrouping.Services;

/// <summary>
/// Scoring and summary helpers shared by the local and Azure OpenAI
/// analyzers, so both produce priorities and summaries the same way.
/// </summary>
internal static class IntentScoring
{
    private static readonly Regex UrgentWords = new(
        @"\b(urgent|asap|immediately|critical|emergency|blocked)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int ComputePriority(FeedbackItem item, string intentName)
    {
        bool criticalIntent = intentName is "Account Management"
            or "Security" or "Billing" or "Performance"
            or "Login Issues" or "Payment Issues" or "Technical Issues"
            or "Negative Feedback" or "Performance & Bugs"
            or "Dissatisfied" or "Customer Support";
        int score = 0;
        if (item.Rating <= 2) score += 2;
        if (item.Rating == 3) score += 1;
        if (criticalIntent && item.Rating <= 2) score += 2;
        if (UrgentWords.IsMatch(item.Text))
        {
            score += 1;
        }
        return Math.Clamp(score, 1, 5);
    }

    public static List<string> ExtractTopKeywords(IEnumerable<string> texts, int topN)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in texts)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var tokens = Regex.Split(raw.ToLowerInvariant(), @"[^a-z0-9]+");
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (token.Length < 4) continue;
                if (IntentTaxonomies.StopWords.Contains(token)) continue;
                if (!char.IsLetter(token[0])) continue;
                counts.TryGetValue(token, out var c);
                counts[token] = c + 1;
            }
        }
        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .Select(kv => kv.Key)
            .ToList();
    }

    public static string BuildOverallSummary(IntentAnalysisResult result)
    {
        var dominant = result.Categories.FirstOrDefault();
        if (dominant == null)
        {
            return $"No clear intent patterns were detected in the supplied {result.DetectedDomain}.";
        }
        var second = result.Categories.Skip(1).FirstOrDefault();
        var phrase = second == null
            ? $"The dataset is dominated by a single intent: {dominant.Name}."
            : $"The two strongest intents are {dominant.Name} ({dominant.Items.Count} items) and {second.Name} ({second.Items.Count} items).";
        var themes = result.TopThemes.Count == 0
            ? string.Empty
            : $" Most frequent terms: {string.Join(", ", result.TopThemes)}.";
        return $"{result.DetectedDomain} were grouped into {result.Categories.Count} intent categories covering {result.TotalItems} items. {phrase}{themes}";
    }
}
