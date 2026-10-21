using System.Collections.ObjectModel;

namespace ListView_IntentGrouping.Models;

/// <summary>
/// A category (intent) produced by the AI analyzer: its name, the items
/// assigned to it, and a generated summary.
/// </summary>
public class IntentCategory
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SummaryTemplate { get; set; } = string.Empty;

    public ObservableCollection<FeedbackItem> Items { get; } = new();
}
