namespace ListView_IntentGrouping.Services;

/// <summary>
/// The grouping mode the user requested in the input field.
/// The view-model picks one of these per Group button click
/// and rebuilds the visible groups accordingly.
/// </summary>
public enum GroupingKind
{
    /// <summary>
    /// No grouping requested (initial flat list, after Reset).
    /// </summary>
    None,

    /// <summary>
    /// Group by rating value (1 Star, 2 Stars, ... 5 Stars).
    /// </summary>
    Rating,

    /// <summary>
    /// Group by the assigned priority bucket (High / Medium /
    /// Low), derived from the AI-analyzer-stamped
    /// <c>FeedbackItem.Priority</c> field.
    /// </summary>
    Priority,

    /// <summary>
    /// Group by the AI-detected *intent* the analyzer assigned
    /// to each ticket (e.g. Billing, Performance, User
    /// Experience). The user-facing prompt is "theme" - the
    /// intent IS the theme - so the resolver treats "theme",
    /// "themes", "intent", "intents", "topic", "topics",
    /// "category", and "categories" as triggers, and the
    /// header is rendered as "Intent - &lt;name&gt;".
    /// </summary>
    Intent,

    /// <summary>
    /// Group by the full <c>FeedbackItem.SenderName</c>.
    /// </summary>
    Sender,

    /// <summary>
    /// Group by *Customer Type* — the segment the customer
    /// belongs to (Enterprise, Premium, Standard, Trial).
    /// </summary>
    CustomerType,

    /// <summary>
    /// Group by *Product / Service* the ticket is about
    /// (Mobile App, Web Portal, API, Billing Platform,
    /// Reporting Dashboard).
    /// </summary>
    ProductService,

    /// <summary>
    /// Group by the *Business Impact* of the ticket (Revenue,
    /// Security, Compliance, Productivity, Minimal).
    /// </summary>
    BusinessImpact,

    /// <summary>
    /// Group by the *Trend / Emerging Issue* pattern the
    /// ticket reveals (Recurring, New, Spike in Volume,
    /// Seasonal).
    /// </summary>
    TrendEmerging,

    /// <summary>
    /// Group by the ticket's MonthYear.
    /// </summary>
    Month,
}

/// <summary>
/// Pure-rule prompt parser: turns a free-text input like
/// "group by ratings" or "group by month" into a
/// <see cref="GroupingKind"/>. No network calls; runs entirely
/// locally so the demo works even without
/// .
/// </summary>
public static class GroupingPromptResolver
{
    /// <summary>
    /// Resolve a user-entered prompt into a grouping kind.
    /// Recognises prompts for the sample scenario: ratings /
    /// priority / theme-as-intent / sender / customer type /
    /// product or service / business impact / trend or
    /// emerging issue / month. Empty / unknown prompts fall
    /// back to <see cref="GroupingKind.None"/>.
    /// </summary>
    public static GroupingKind Resolve(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return GroupingKind.None;
        }
        var p = prompt.Trim().ToLowerInvariant();

        // Resolve without needing the literal "group by" prefix.
        // Strip any leading "group by" / "by" / "according to"
        // noise so "ratings", "group by ratings" and "show me
        // ratings" all resolve to Rating in one pass.
        var core = StripGroupingPrefix(p);

        if (ContainsAny(core, "priority", "priorities", "severity", "urgency", "urgent"))
        {
            return GroupingKind.Priority;
        }
        // Order matters: Product/Service and Customer Type
        // contain the substring "customer", so the plain
        // "customer" prompt must be matched by Intent/Sender
        // BEFORE these specific category checks fire. The
        // "customer type" / "product" / "business impact" /
        // "trend" prompts all need exact-noun matching to win.
        if (ContainsAny(core, "intent", "intents", "theme", "themes", "topic", "topics",
                       "category", "categories"))
        {
            return GroupingKind.Intent;
        }
        if (ContainsAny(core, "sender", "senders", "person", "people",
                       "requester", "requesters"))
        {
            return GroupingKind.Sender;
        }
        if (ContainsAny(core, "customer type", "customer types", "customer segment",
                       "customer segments", "customer tier", "customer tiers",
                       "enterprise", "premium customer", "premium customers",
                       "standard customer", "standard customers",
                       "trial user", "trial users"))
        {
            return GroupingKind.CustomerType;
        }
        if (ContainsAny(core, "product", "products", "service", "services",
                       "mobile app", "web portal", "billing platform",
                       "reporting dashboard", "api"))
        {
            return GroupingKind.ProductService;
        }
        if (ContainsAny(core, "business impact", "impact", "revenue impact",
                       "security impact", "compliance impact",
                       "productivity impact", "minimal impact"))
        {
            return GroupingKind.BusinessImpact;
        }
        if (ContainsAny(core, "trend", "trends", "emerging", "emerging issue",
                       "emerging issues", "recurring", "recurring issue",
                       "recurring issues", "new issue", "new issues",
                       "spike", "volume spike", "seasonal",
                       "seasonal request", "seasonal requests"))
        {
            return GroupingKind.TrendEmerging;
        }
        if (ContainsAny(core, "rating", "ratings", "star", "stars"))
        {
            return GroupingKind.Rating;
        }
        if (ContainsAny(core, "month", "months", "month wise", "monthly", "date", "dates"))
        {
            return GroupingKind.Month;
        }
        return GroupingKind.None;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Strips leading "group by" / "by" / "according to" noise
    /// so the resolver is forgiving about whether the user
    /// typed a bare keyword ("ratings") or a fuller phrase
    /// ("group by ratings", "by ratings", ...).
    /// </summary>
    private static string StripGroupingPrefix(string text)
    {
        foreach (var prefix in new[] { "group by", "according to", "sort by", "filter by" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(prefix.Length).TrimStart();
                break;
            }
        }
        // A lone leading "by" ("by ratings", "by month").
        if (text.StartsWith("by ", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(3).TrimStart();
        }
        return text;
    }
}