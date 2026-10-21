using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ListView_IntentGrouping.Models;
using ListView_IntentGrouping.Services;

namespace ListView_IntentGrouping.ViewModels;

public class MainPageViewModel : INotifyPropertyChanged
{
    private readonly IIntentAnalyzer _analyzer;
    private string _prompt = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private GroupingKind _currentGrouping = GroupingKind.None;
    private string _analyzerLabel = string.Empty;
    private string _credentialBanner = string.Empty;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Pristine working snapshot of every ticket. The view-model
    /// keeps this list so the Group button can rebuild the visible
    /// groupings repeatedly without needing to clone the dataset
    /// on every click.
    /// </summary>
    private readonly List<FeedbackItem> _sourceTickets = new();

    public MainPageViewModel(IIntentAnalyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _analyzerLabel = string.IsNullOrWhiteSpace(_analyzer.DisplayName)
            ? "AI analyzer"
            : _analyzer.DisplayName;
        GroupCommand = new Command(async () => await GroupAsync(), () => !IsBusy);
        CancelCommand = new Command(() => _cts?.Cancel(), () => IsBusy);
    }

    /// <summary>
    /// Visible ticket list, in display order, sharing references
    /// with the internal working snapshot. The SfListView binds
    /// directly to this collection.
    /// </summary>
    public ObservableCollection<FeedbackItem> Items { get; } = new();

    /// <summary>
    /// Distinct group headers currently in scope, in display
    /// order. The page's header template drives the per-group
    /// title, icon, accent colour, summary, and item count from
    /// this list's <see cref="GroupHeaderInfo"/> records.
    /// </summary>
    public ObservableCollection<GroupHeaderInfo> GroupHeaders { get; } = new();

    /// <summary>
    /// The user's prompt. Bound to the input field on MainPage.
    /// </summary>
    public string Prompt
    {
        get => _prompt;
        set { if (_prompt != value) { _prompt = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Free-form status / hint line shown below the input row.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Friendly label of the analyzer currently driving the
    /// grouping decisions (e.g. "Azure OpenAI (gpt-4o)" or
    /// "Local rule-based"). Surfaced in the status banner so the
    /// user always sees which engine produced the grouping.
    /// </summary>
    public string AnalyzerLabel
    {
        get => _analyzerLabel;
        private set { if (_analyzerLabel != value) { _analyzerLabel = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Small banner that surfaces the *currently active*
    /// Azure OpenAI credentials (host + key fingerprint +
    /// deployment). Set on startup and refreshed on every
    /// Group click so the user can always verify the key the
    /// app is sending matches the one in the Azure portal.
    /// </summary>
    public string CredentialBanner
    {
        get => _credentialBanner;
        private set { if (_credentialBanner != value) { _credentialBanner = value; OnPropertyChanged(); } }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotBusy));
            GroupCommand.ChangeCanExecute();
            CancelCommand.ChangeCanExecute();
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Current grouping mode. The list's grouping behavior binds to
    /// this to install or remove its <c>GroupDescriptor</c>, and the
    /// empty-state text follows it.
    /// </summary>
    public GroupingKind CurrentGrouping
    {
        get => _currentGrouping;
        private set
        {
            if (_currentGrouping == value) return;
            _currentGrouping = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGrouped));
            OnPropertyChanged(nameof(IsUngrouped));
            OnPropertyChanged(nameof(GroupTitle));
            OnPropertyChanged(nameof(EmptyStateHint));
        }
    }

    /// <summary>True when the current view has a group descriptor.</summary>
    public bool IsGrouped => _currentGrouping != GroupingKind.None;

    /// <summary>True when the current view is flat (initial / after Reset).</summary>
    public bool IsUngrouped => _currentGrouping == GroupingKind.None;

    /// <summary>
    /// Friendly title reflecting the active grouping mode for the
    /// summary / status card placeholder text.
    /// </summary>
    public string GroupTitle => _currentGrouping switch
    {
        GroupingKind.Rating         => "Grouped by Rating",
        GroupingKind.Priority       => "Grouped by Priority",
        GroupingKind.Intent         => "Grouped by Intent",
        GroupingKind.Sender         => "Grouped by Sender",
        GroupingKind.CustomerType   => "Grouped by Customer Type",
        GroupingKind.ProductService => "Grouped by Product / Service",
        GroupingKind.BusinessImpact => "Grouped by Business Impact",
        GroupingKind.TrendEmerging  => "Grouped by Trend / Emerging Issues",
        GroupingKind.Month          => "Grouped by Month",
        _                           => "All Tickets",
    };

    /// <summary>
    /// Hint shown in the empty-state when nothing matches the
    /// current grouping. Drawn from the active mode.
    /// </summary>
    public string EmptyStateHint => _currentGrouping switch
    {
        GroupingKind.Rating         => "No ratings to group by yet.",
        GroupingKind.Priority       => "No priorities to group by yet.",
        GroupingKind.Intent         => "No intents to group by yet.",
        GroupingKind.Sender         => "No senders to group by yet.",
        GroupingKind.CustomerType   => "No customer types to group by yet.",
        GroupingKind.ProductService => "No products or services to group by yet.",
        GroupingKind.BusinessImpact => "No business impacts to group by yet.",
        GroupingKind.TrendEmerging  => "No trends or emerging issues to group by yet.",
        GroupingKind.Month          => "No tickets this month yet.",
        _                           => "Type a prompt above (for example: \"ratings\") and tap Group."
    };

    public Command GroupCommand { get; }

    /// <summary>
    /// Cancels the Azure call that is currently in flight.
    /// </summary>
    public Command CancelCommand { get; }

    /// <summary>
    /// First-time entry point. Loads the unified ticket list into
    /// <see cref="Items"/> so the SfListView renders a flat list
    /// below the input row. Runs the injected AI analyzer once so
    /// every ticket gets its intent classification, confidence
    /// score and re-computed priority before the user clicks
    /// "Group".
    /// </summary>
    public async Task LoadInitialItemsAsync()
    {
        using var cts = BeginOperation();
        try
        {
            IsBusy = true;

            _sourceTickets.Clear();
            foreach (var src in FeedbackDataset.Tickets)
            {
                _sourceTickets.Add(Clone(src));
            }

            // Run the AI once on initial load so its work is
            // visible immediately in the status banner.
            await RunAnalyzerAsync(string.Empty, cts.Token);

            ReloadVisibleItems(GroupingKind.None);

            Prompt = string.Empty;
            StatusMessage = $"Loaded {_sourceTickets.Count} tickets via {_analyzer.DisplayName} - " +
                            "type a prompt such as \"ratings\" or \"theme\" and tap Group.";
        }
        catch (IntentAnalysisException ex)
        {
            // The analyzer is informational on initial load; do
            // not block the flat list because of a transient AI
            // failure. Surface the message in the status banner
            // so the user can fix the credential issue. Also
            // append the active credential fingerprint so the
            // user can confirm which key the running app is
            // actually using.
            ReloadVisibleItems(GroupingKind.None);
            StatusMessage = $"Loaded {_sourceTickets.Count} tickets: {ex.Message} " +
                            $"[Active: {AzureBaseService.ActiveCredentialSummary}]";
        }
        catch (OperationCanceledException)
        {
            ReloadVisibleItems(GroupingKind.None);
            StatusMessage = $"Loaded {_sourceTickets.Count} tickets (analysis cancelled).";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    /// <summary>
    /// The "Group" button handler. Resolves the user's prompt
    /// into a <see cref="GroupingKind"/>, runs the AI analyzer
    /// (so it does the grouping work) and rebuilds the visible
    /// groups accordingly. The list's grouping behavior reacts to
    /// <see cref="CurrentGrouping"/> through its binding.
    /// </summary>
    public async Task GroupAsync()
    {
        if (IsBusy) return;
        using var cts = BeginOperation();
        try
        {
            IsBusy = true;
            StatusMessage = $"Asking {_analyzer.DisplayName} to group the tickets...";

            if (_sourceTickets.Count == 0)
            {
                // First click before items loaded — fall back to
                // the initial load path so the user always sees
                // a list.
                await LoadInitialItemsAsync();
                return;
            }

            var kind = GroupingPromptResolver.Resolve(Prompt);

            // The AI always runs first so the grouping is driven
            // by the analyzer's results (intent label,
            // confidence, re-computed priority). The requested
            // <see cref="GroupingKind"/> then projects those
            // results into the right buckets below.
            await RunAnalyzerAsync(Prompt, cts.Token);

            // Show the live credential fingerprint after every
            // successful Group click. When the next click returns
            // 401, the Status banner will show the same fingerprint
            // so the user can match it against the key in the
            // Azure portal.
            CredentialBanner = AzureBaseService.GetStartupBanner();

            ReloadVisibleItems(kind);

            StatusMessage = kind switch
            {
                GroupingKind.Rating         => $"Grouped {_sourceTickets.Count} tickets by rating into {GroupHeaders.Count} bucket(s).",
                GroupingKind.Priority       => $"Grouped {_sourceTickets.Count} tickets by priority into {GroupHeaders.Count} bucket(s).",
                GroupingKind.Intent         => $"Grouped {_sourceTickets.Count} tickets by intent into {GroupHeaders.Count} bucket(s).",
                GroupingKind.Sender         => $"Grouped {_sourceTickets.Count} tickets by sender into {GroupHeaders.Count} bucket(s).",
                GroupingKind.CustomerType   => $"Grouped {_sourceTickets.Count} tickets by customer type into {GroupHeaders.Count} bucket(s).",
                GroupingKind.ProductService => $"Grouped {_sourceTickets.Count} tickets by product / service into {GroupHeaders.Count} bucket(s).",
                GroupingKind.BusinessImpact => $"Grouped {_sourceTickets.Count} tickets by business impact into {GroupHeaders.Count} bucket(s).",
                GroupingKind.TrendEmerging  => $"Grouped {_sourceTickets.Count} tickets by trend / emerging issues into {GroupHeaders.Count} bucket(s).",
                GroupingKind.Month          => $"Grouped {_sourceTickets.Count} tickets across {GroupHeaders.Count} month(s).",
                _                           => $"Showing {_sourceTickets.Count} tickets in a flat list (analyzed by {_analyzer.DisplayName}). Try \"ratings\", \"priority\", \"theme\", \"sender\", \"customer type\", \"product\", \"business impact\", \"trend\", or \"month\"."
            };
        }
        catch (IntentAnalysisException ex)
        {
            // Don't blow up the page if the AI call failed —
            // fall back to a flat list and surface the failure in
            // the status banner. The caller's tasks (bucketing)
            // can still proceed locally with the data already on
            // screen so the user gets a usable view.
            ReloadVisibleItems(GroupingKind.None);
            StatusMessage = $"AI grouping failed: {ex.Message} " +
                            $"[Active: {AzureBaseService.ActiveCredentialSummary}]";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Grouping cancelled.";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private CancellationTokenSource BeginOperation()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        return cts;
    }

    private void EndOperation(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_cts, cts))
        {
            _cts = null;
        }
        IsBusy = false;
    }

    /// <summary>
    /// Run the injected <see cref="IIntentAnalyzer"/> on the
    /// working ticket list. The analyzer mutates each
    /// <see cref="FeedbackItem.AssignedIntent"/>,
    /// <see cref="FeedbackItem.Confidence"/> and re-computes
    /// <see cref="FeedbackItem.Priority"/>.
    /// </summary>
    private async Task RunAnalyzerAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            await _analyzer.AnalyzeAsync(
                prompt ?? string.Empty,
                FeedbackDataset.Domain,
                _sourceTickets,
                cancellationToken).ConfigureAwait(true);
            AnalyzerLabel = _analyzer.DisplayName;
        }
        catch (Exception ex) when (ex is IntentAnalysisException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Convert any unexpected analyzer failure into the
            // canonical IntentAnalysisException so the rest of
            // the view-model keeps a uniform catch site.
            throw new IntentAnalysisException(
                $"{_analyzer.DisplayName} call failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Build the visible Items collection for the requested
    /// grouping kind, assigning each item's
    /// <see cref="FeedbackItem.GroupKey"/> to a
    /// <see cref="GroupHeaderInfo"/> reference.
    /// </summary>
    private void ReloadVisibleItems(GroupingKind kind)
    {
        ResetLists();

        if (kind == GroupingKind.None)
        {
            // Flat list: every source ticket in display order,
            // with no group key.
            foreach (var item in _sourceTickets)
            {
                item.GroupKey = null;
                Items.Add(item);
            }
            CurrentGrouping = GroupingKind.None;
            return;
        }

        // Trend / Emerging Issues uses the dataset's newest
        // MonthYear to tag "New Issues" vs everything else. Refresh
        // it here so the tagging is always current even after the
        // user groups, edits, and re-groups.
        if (kind == GroupingKind.TrendEmerging)
        {
            RefreshNewestMonthYearSort();
        }

        switch (kind)
        {
            case GroupingKind.Rating:
                BuildRatingGroups();
                break;
            case GroupingKind.Priority:
                BuildPriorityGroups();
                break;
            case GroupingKind.Intent:
                BuildIntentGroups();
                break;
            case GroupingKind.Sender:
                BuildSenderGroups();
                break;
            case GroupingKind.CustomerType:
                BuildCustomerTypeGroups();
                break;
            case GroupingKind.ProductService:
                BuildProductServiceGroups();
                break;
            case GroupingKind.BusinessImpact:
                BuildBusinessImpactGroups();
                break;
            case GroupingKind.TrendEmerging:
                BuildTrendEmergingGroups();
                break;
            case GroupingKind.Month:
                BuildMonthGroups();
                break;
        }

        // Flip CurrentGrouping exactly once at the end of the
        // rebuild so the list refreshes its GroupDescriptor once.
        CurrentGrouping = kind;
    }

    private void BuildRatingGroups()
    {
        // 5 Stars -> 1 Star (most positive first).
        var buckets = Enumerable.Range(1, 5)
            .Select(r => GroupHeaderInfo.FromBucket(
                title: GroupHeaderInfo.GetRatingBucketTitle(r),
                itemCount: 0))
            .ToDictionary(h => RatingFromTitle(h.Title));

        foreach (var item in _sourceTickets)
        {
            int rating = Math.Clamp(item.Rating, 1, 5);
            var header = buckets[rating];
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        foreach (var header in buckets.Values
                     .OrderByDescending(h => RatingFromTitle(h.Title)))
        {
            if (header.ItemCount > 0)
            {
                GroupHeaders.Add(header);
            }
        }
    }

    private static int RatingFromTitle(string title)
    {
        // "5 Stars" / "1 Star" -> int. Used so we can order buckets
        // from most positive (5) to least positive (1) without
        // touching the header string itself.
        foreach (var ch in title)
        {
            if (char.IsDigit(ch))
            {
                if (int.TryParse(ch.ToString(), out int rating))
                {
                    return rating;
                }
            }
        }
        return 0;
    }

    private void BuildIntentGroups()
    {
        var groups = new Dictionary<string, GroupHeaderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _sourceTickets)
        {
            var key = string.IsNullOrWhiteSpace(item.AssignedIntent)
                ? "Unclassified"
                : item.AssignedIntent;
            if (!groups.TryGetValue(key, out var header))
            {
                header = GroupHeaderInfo.FromBucket($"Intent - {key}", 0);
                groups[key] = header;
            }
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        // Most frequent themes first, then alphabetical for
        // buckets with the same count.
        foreach (var header in groups.Values
                     .OrderByDescending(h => h.ItemCount)
                     .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase))
        {
            GroupHeaders.Add(header);
        }
    }

    private void BuildSenderGroups()
    {
        var groups = new Dictionary<string, GroupHeaderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _sourceTickets)
        {
            var key = string.IsNullOrWhiteSpace(item.SenderName) ? "Unknown" : item.SenderName;
            if (!groups.TryGetValue(key, out var header))
            {
                header = GroupHeaderInfo.FromBucket(key, 0);
                groups[key] = header;
            }
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        foreach (var header in groups.Values.OrderBy(h => h.Title, StringComparer.OrdinalIgnoreCase))
        {
            GroupHeaders.Add(header);
        }
    }

    private void BuildCustomerTypeGroups()
    {
        var buckets = new (string Title, string[] Keywords)[]
        {
            ("Customer Type - Enterprise Customers",
                new[] { "enterprise", "enterprise sla", "enterprise plan", "corporate", "sso", "saml",
                        "contract", "msa", "single sign-on", "procurement", "vendor", "mfa", "audit",
                        "compliance", "iso", "soc 2", "soc2", "on-prem", "integration",
                        "workspace", "seat", "seats" }),
            ("Customer Type - Premium Customers",
                new[] { "premium", "premium plan", "priority support", "dedicated", "concierge",
                        "premium tier", "white-glove", "annual plan", "professional" }),
            ("Customer Type - Standard Customers",
                new[] { "standard", "standard plan", "monthly plan", "free trial ended",
                        "downgrade", "auto-renew", "subscription", "personal plan",
                        "individual", "small team", "starter" }),
            ("Customer Type - Trial Users",
                new[] { "trial", "trial user", "free trial", "trial expired", "trial ending",
                        "14-day", "30-day", "exploring", "evaluating", "evaluation",
                        "prospect", "demo account" }),
        };

        BuildKeywordBuckets(buckets, _sourceTickets);
    }

    // -----------------------------------------------------------------
    // Product / Service buckets ("mobile app", "web portal", "api",
    // "billing platform", "reporting dashboard").
    // -----------------------------------------------------------------
    private void BuildProductServiceGroups()
    {
        var buckets = new (string Title, string[] Keywords)[]
        {
            ("Product / Service - Mobile App",
                new[] { "mobile", "ios", "android", "iphone", "ipad", "app store", "play store",
                        "mobile app", "push notification", "push notifications", "mobile crash",
                        "tablet", "in-app", "in app" }),
            ("Product / Service - Web Portal",
                new[] { "web portal", "portal", "web app", "website", "browser", "desktop",
                        "chrome", "firefox", "safari", "edge", "web dashboard",
                        "web ui", "sign in", "log in", "logout" }),
            ("Product / Service - API",
                new[] { "api", "endpoint", "rest", "rest api", "graphql", "webhook",
                        "rate limit", "sdk", "openapi", "swagger", "oauth", "token",
                        "api key", "developer", "developer portal" }),
            ("Product / Service - Billing Platform",
                new[] { "billing platform", "billing", "invoice", "payment", "charge",
                        "charged", "refund", "credit card", "receipt", "subscription",
                        "renewal", "auto-upgrade", "downgrade", "pricing", "discount",
                        "quote" }),
            ("Product / Service - Reporting Dashboard",
                new[] { "reporting dashboard", "report", "reports", "reporting", "analytics",
                        "dashboard", "csv export", "pdf export", "export", "metrics",
                        "kpi", "chart", "graph", "data studio", "power bi" }),
        };

        BuildKeywordBuckets(buckets, _sourceTickets);
    }

    private void BuildBusinessImpactGroups()
    {
        var buckets = new (string Title, string[] Keywords)[]
        {
            ("Business Impact - Revenue Impact",
                new[] { "revenue", "revenue impact", "lost deal", "deal loss", "churn",
                        "refund", "downgrade", "cancel", "cancellation", "billing",
                        "pricing", "renewal", "auto-renew", "expansion" }),
            ("Business Impact - Security Impact",
                new[] { "security", "security impact", "breach", "vulnerability",
                        "exploit", "phishing", "credential leak", "data leak",
                        "password reset", "2fa", "two-factor", "mfa", "oauth",
                        "account take", "hijack", "security incident" }),
            ("Business Impact - Compliance Impact",
                new[] { "compliance", "compliance impact", "gdpr", "hipaa", "soc 2",
                        "soc2", "iso 27001", "pci", "audit", "audit log", "data retention",
                        "data residency", "privacy", "ccpa", "dsar" }),
            ("Business Impact - Productivity Impact",
                new[] { "productivity", "productivity impact", "blocked", "blocker",
                        "can't work", "cannot work", "waste time", "slow", "spreadsheet",
                        "manual workaround", "manual process", "downtime", "outage" }),
            ("Business Impact - Minimal Impact",
                new[] { "minimal", "minimal impact", "no impact", "low priority",
                        "cosmetic", "nice to have", "would love", "wish list",
                        "feature request", "small ask", "minor" }),
        };

        BuildKeywordBuckets(buckets, _sourceTickets);
    }

    private void BuildTrendEmergingGroups()
    {
        var sourceTickets = _sourceTickets;
        var tagTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Recurring"] = "Trend - Recurring Issues",
            ["New"]       = "Trend - New Issues",
            ["Spike"]     = "Trend - Spike in Volume",
            ["Seasonal"]  = "Trend - Seasonal Requests",
        };
        var groups = tagTitles.ToDictionary(
            kv => kv.Value,
            kv => GroupHeaderInfo.FromBucket(kv.Value, 0),
            StringComparer.OrdinalIgnoreCase);

        // 1) Recurring Issues — items whose intent label appears
        //    on 3 or more tickets in the dataset.
        var recurringIntents = sourceTickets
            .Where(i => !string.IsNullOrWhiteSpace(i.AssignedIntent))
            .GroupBy(i => i.AssignedIntent, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 3)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3) Spike in Volume — topics whose ticket count in the
        //    newest month is >= 3x the average count per older
        //    month for the same topic (and at least 4 tickets in
        //    the spike month).
        var spikeIntents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Group the per-item intent+month pairs using a custom
        // tuple comparer so the spike detection is case-insensitive
        // without relying on the assigned intent being a single
        // normalised capitalisation.
        var intentMonthPairs = sourceTickets
            .Where(i => !string.IsNullOrWhiteSpace(i.AssignedIntent))
            .Select(i => (Intent: i.AssignedIntent ?? string.Empty, Month: i.MonthYear ?? string.Empty))
            .ToList();
        var grouped = intentMonthPairs
            .GroupBy(p => p, new IntentMonthTupleComparer());
        var perIntentMonths = grouped
            .GroupBy(g => g.Key.Intent, StringComparer.OrdinalIgnoreCase);
        foreach (var byIntent in perIntentMonths)
        {
            var months = byIntent
                .Select(g => (SortKey: ParseMonthYearSortKey(g.Key.Month), Count: g.Count()))
                .Where(x => x.SortKey > 0)
                .OrderBy(x => x.SortKey)
                .ToList();
            if (months.Count < 2) continue;
            var newest = months[^1];
            var priorAvg = months.Take(months.Count - 1).Average(m => (double)m.Count);
            if (priorAvg > 0 && newest.Count >= priorAvg * 3 && newest.Count >= 4)
            {
                spikeIntents.Add(byIntent.Key);
            }
        }

        // 4) Seasonal — items whose MonthYear falls in a known
        //    seasonal reporting window.
        var seasonalMonths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Jan", "Mar", "Jun", "Sep", "Nov", "Dec" };

        foreach (var item in sourceTickets)
        {
            // Priority: Spike > Seasonal > Recurring > New.
            // First signal that matches wins.
            string chosenTitle;
            if (spikeIntents.Contains(item.AssignedIntent ?? string.Empty))
            {
                chosenTitle = tagTitles["Spike"];
            }
            else if (seasonalMonths.Contains(ExtractMonthToken(item.MonthYear)))
            {
                chosenTitle = tagTitles["Seasonal"];
            }
            else if (recurringIntents.Contains(item.AssignedIntent ?? string.Empty))
            {
                chosenTitle = tagTitles["Recurring"];
            }
            else if (ParseMonthYearSortKey(item.MonthYear) == _newestMonthYearSort)
            {
                chosenTitle = tagTitles["New"];
            }
            else
            {
                // Older, non-recurring items fold into New so the
                // bucket is never empty when the dataset has any
                // items at all.
                chosenTitle = tagTitles["New"];
            }

            var header = groups[chosenTitle];
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        // Bucket order: Recurring -> New -> Spike -> Seasonal,
        // matching the user's bullet list and the order they were
        // declared. ItemCount is the sole secondary sort key so the
        // most active trend bucket is the second header when two
        // buckets share rank.
        var orderTitlePairs = new[]
        {
            tagTitles["Recurring"],
            tagTitles["New"],
            tagTitles["Spike"],
            tagTitles["Seasonal"],
        };
        foreach (var title in orderTitlePairs)
        {
            var header = groups[title];
            if (header.ItemCount > 0)
            {
                GroupHeaders.Add(header);
            }
        }
    }

    private static string ExtractMonthToken(string monthYear)
    {
        var parts = (monthYear ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0];
    }

    /// <summary>
    /// Equality comparer used by the trend builder to group
    /// (Intent, MonthYear) tuples in a case-insensitive way
    /// without allocating a new string comparer per call.
    /// </summary>
    private sealed class IntentMonthTupleComparer : IEqualityComparer<(string Intent, string Month)>
    {
        public bool Equals((string Intent, string Month) x,
                           (string Intent, string Month) y)
            => string.Equals(x.Intent, y.Intent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Month,  y.Month,  StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Intent, string Month) obj)
            => HashCode.Combine(
                obj.Intent?.ToLowerInvariant(),
                obj.Month?.ToLowerInvariant());
    }

    private int _newestMonthYearSort;

    /// <summary>
    /// Recompute <see cref="_newestMonthYearSort"/> from the current
    /// working snapshot. Called by the four new category builders
    /// before they inspect month metadata, so the trend
    /// "New vs Recurring" tagging is fresh on every Group click.
    /// </summary>
    private void RefreshNewestMonthYearSort()
    {
        _newestMonthYearSort = _sourceTickets
            .Select(item =>
            {
                var parts = (item.MonthYear ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[1], out int year))
                {
                    int month = 0;
                    for (int m = 1; m <= 12; m++)
                    {
                        if (CultureInfo.InvariantCulture.DateTimeFormat
                            .GetAbbreviatedMonthName(m)
                            .Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                        {
                            month = m;
                            break;
                        }
                    }
                    if (month > 0) return year * 100 + month;
                }
                return 0;
            })
            .DefaultIfEmpty(0)
            .Max();
    }

    private void BuildKeywordBuckets(
        (string Title, string[] Keywords)[] buckets,
        List<FeedbackItem> sourceTickets)
    {
        var groups = new Dictionary<string, GroupHeaderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (title, _) in buckets)
        {
            groups[title] = GroupHeaderInfo.FromBucket(title, 0);
        }

        foreach (var item in sourceTickets)
        {
            // Null-safe text gathering — the analyzer mutates
            // ShortDescription and Text, but legacy SenderName /
            // TicketName fields are still useful signals here.
            var haystack = string.Join(' ',
                item.Text ?? string.Empty,
                item.ShortDescription ?? string.Empty,
                item.TicketName ?? string.Empty,
                item.SenderName ?? string.Empty);

            string? chosen = null;
            int best = 0;
            for (int i = 0; i < buckets.Length; i++)
            {
                int score = 0;
                foreach (var kw in buckets[i].Keywords)
                {
                    if (haystack.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += kw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2 ? 3 : 2;
                    }
                }
                if (score > best)
                {
                    best = score;
                    chosen = buckets[i].Title;
                }
            }
            // Fall back to the LAST bucket so items that match
            // nothing still get a sensible home (Trial Users /
            // Minimal Impact — the catch-all at the bottom of the
            // user-supplied bucket list).
            if (chosen == null)
            {
                chosen = buckets[^1].Title;
            }
            var header = groups[chosen];
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        foreach (var header in groups.Values)
        {
            if (header.ItemCount > 0)
            {
                GroupHeaders.Add(header);
            }
        }
    }

    private void BuildPriorityGroups()
    {
        // High (>=4) / Medium (2-3) / Low (1), most urgent first.
        var groups = new Dictionary<int, GroupHeaderInfo>
        {
            [4] = GroupHeaderInfo.FromBucket("High Priority",   0),
            [3] = GroupHeaderInfo.FromBucket("Medium Priority", 0),
            [2] = GroupHeaderInfo.FromBucket("Low Priority",    0),
        };

        foreach (var item in _sourceTickets)
        {
            // Priorities are stamped in 1..5; clamp into the same
            // buckets the item-card chip uses (PriorityToLabel).
            int bucket = item.Priority >= 4 ? 4
                       : item.Priority >= 2 ? 3
                       : 2;
            var header = groups[bucket];
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        // Most urgent first; unknown / empty buckets dropped.
        foreach (var header in groups.Values
                     .OrderByDescending(h => PriorityBucketSortKey(h.Title)))
        {
            if (header.ItemCount > 0)
            {
                GroupHeaders.Add(header);
            }
        }
    }

    private static int PriorityBucketSortKey(string title)
    {
        // "High Priority" -> 4, "Medium Priority" -> 3,
        // "Low Priority" -> 2. Lets us show the most urgent
        // bucket first without doing a string compare.
        if (title.StartsWith("High",   StringComparison.OrdinalIgnoreCase)) return 4;
        if (title.StartsWith("Medium", StringComparison.OrdinalIgnoreCase)) return 3;
        if (title.StartsWith("Low",    StringComparison.OrdinalIgnoreCase)) return 2;
        return 0;
    }

    private void BuildMonthGroups()
    {
        var groups = new Dictionary<string, GroupHeaderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _sourceTickets)
        {
            var key = string.IsNullOrWhiteSpace(item.MonthYear) ? "Unknown" : item.MonthYear;
            if (!groups.TryGetValue(key, out var header))
            {
                header = GroupHeaderInfo.FromBucket(key, 0);
                groups[key] = header;
            }
            item.GroupKey = header;
            Items.Add(item);
            header.ItemCount++;
        }

        foreach (var header in groups.Values
                     .OrderByDescending(h => ParseMonthYearSortKey(h.Title)))
        {
            GroupHeaders.Add(header);
        }
    }

    private static int ParseMonthYearSortKey(string monthYear)
    {
        // "Mar 2024" -> 202403. Lets us show the newest month first
        // without depending on any DateTime.Parse on short strings.
        var parts = monthYear.Split(' ');
        if (parts.Length == 2 &&
            int.TryParse(parts[1], out int year))
        {
            int month = 0;
            for (int m = 1; m <= 12; m++)
            {
                if (CultureInfo.InvariantCulture.DateTimeFormat
                    .GetAbbreviatedMonthName(m)
                    .Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                {
                    month = m;
                    break;
                }
            }
            return year * 100 + month;
        }
        return 0;
    }

    private void ResetLists()
    {
        Items.Clear();
        GroupHeaders.Clear();
    }

    /// <summary>
    /// Defensive copy of a <see cref="FeedbackItem"/>. The
    /// Grouping button may be clicked several times in a row;
    /// making fresh copies on every click keeps the source data
    /// pristine so later grouping passes start from a clean slate.
    /// </summary>
    private static FeedbackItem Clone(FeedbackItem src) => new()
    {
        Id = src.Id,
        TicketName = src.TicketName,
        SenderName = src.SenderName,
        MonthYear = src.MonthYear,
        Rating = src.Rating,
        Priority = src.Priority,
        ShortDescription = src.ShortDescription,
        Text = src.Text,
        Customer = src.Customer,
        Date = src.Date,
        Source = src.Source,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}