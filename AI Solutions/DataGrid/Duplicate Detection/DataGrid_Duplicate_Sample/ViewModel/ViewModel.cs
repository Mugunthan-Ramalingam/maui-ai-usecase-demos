using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DataGrid_Duplicate_Sample;

public partial class ViewModel : ObservableObject
{
    private readonly IDuplicateAnalyzer _analyzer;
    private readonly PairCandidateGenerator _generator = new();
    private CancellationTokenSource? cancelToken;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<DuplicateResult> Duplicates { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready.";

    /// <summary>
    /// True after the user has run the AI scan at least once. Drives the
    /// visibility of the Match / Status / Delete columns on the grid —
    /// those columns are intentionally hidden on first load.
    /// </summary>
    [ObservableProperty]
    private bool showAnalysisColumns;


    public ViewModel(IDuplicateAnalyzer analyzer)
    {
        _analyzer = analyzer;
        foreach (var c in CustomerSeed.Build())
            Customers.Add(c);
    }

    /// <summary>
    /// Used by code-behind after a row has been removed by the delete
    /// command so that surviving rows that no longer have any duplicate
    /// partner clear their IsDuplicate flag and the delete icon
    /// disappears immediately (no need to re-run the scan).
    /// </summary>
    public void NotifyCustomersChanged()
    {
        OnPropertyChanged(nameof(Customers));
    }

    [RelayCommand]
    private async Task DeleteRow(Customer? customer)
    {
        if (customer is null) return;

        // Confirm with the user before deleting.
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            var confirmed = await page.DisplayAlertAsync(
                                "Delete row",
                                $"Are you sure you want to delete the row \"{customer.Name}\"?",
                                "Delete",
                                "Cancel");

            if (!confirmed)
                return;
        }

        var index = Customers.IndexOf(customer);
        if (index < 0) 
            return;

        Customers.RemoveAt(index);

        // Re-evaluate duplicates among the remaining rows using the results we
        // already have (no re-scan needed): drop every pair that referenced the
        // deleted row. Surviving pairs keep their highlight; rows that lost
        // their only duplicate partner get cleared back to "Unique".
        var remainingResults = Duplicates
            .Where(r => r.RecordAId != customer.Id && r.RecordBId != customer.Id)
            .ToList();

        if (remainingResults.Count == 0)
        {
            // No duplicate pair is left anywhere in the grid.
            ResetAllCustomerAnalysisState();
            ShowAnalysisColumns = false;
            Duplicates.Clear();
            StatusMessage = "No duplicates found.";
        }
        else
        {
            ApplyResults(remainingResults, source: HighlightSource.Scan);
            StatusMessage = $"Deleted \"{customer.Name}\". Remaining duplicate pair(s): {remainingResults.Count}.";
        }
    }

    /// <summary>
    /// Resets the per-row duplicate-analysis fields on every customer so that
    /// after a delete (or any other reset-driven flow) the grid is back to
    /// a clean "Unique" / no-highlight state.
    /// </summary>
    private void ResetAllCustomerAnalysisState()
    {
        foreach (var c in Customers)
        {
            c.RowHighlight = RowHighlight.None;
            c.Match = "—";
            c.Status = "Unique";
            c.IsDuplicate = false;
            c.IsEditing = false;
        }
    }

    [RelayCommand]
    private async Task IdentifyDuplicatesAsync()
    {
        if (IsBusy)
            return;

        cancelToken?.Cancel();
        var ct = new CancellationTokenSource();
        cancelToken = ct;

        try
        {
            IsBusy = true;
            // Keep the Match / Status / Delete columns HIDDEN while the scan is
            // running so users don't see a transient flash of "—" / "Unique"
            // baseline values. We only reveal the columns AFTER results land.
            ShowAnalysisColumns = false;
            StatusMessage = "Building candidate pairs…";
            var pairs = _generator.BuildPairs(Customers);
            StatusMessage = $"Sending candidate pair(s) to Azure OpenAI…";

            // Reset everyone to "Unique" before a full scan so stale highlights don't linger.
            ResetAllCustomerAnalysisState();

            var results = await _analyzer.AnalyzeAsync(pairs, ct.Token);
            results = MergeIdenticalAddressResults(results, Customers);

            var duplicateCount = results.Count(r => r.Classification != DuplicateClassification.NotDuplicate);

            if (duplicateCount == 0)
            {
                // No duplicates anywhere: don't reveal the Match / Status /
                // Delete columns at all — just show the popup.
                ApplyResults(results, source: HighlightSource.Scan);
                ShowAnalysisColumns = false;
                StatusMessage = "No duplicates found.";

                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page is not null)
                {
                    await page.DisplayAlertAsync(
                        "No duplicates",
                        "No duplicates found.",
                        "OK");
                }
                return;
            }

            ApplyResults(results, source: HighlightSource.Scan);

            // Reveal the Match / Status / Delete columns only now that real
            // results are applied.
            ShowAnalysisColumns = true;
            StatusMessage = $"Done. {results.Count(r => r.Classification == DuplicateClassification.LikelyDuplicate)} likely · "
                          + $"{results.Count(r => r.Classification == DuplicateClassification.PossibleDuplicate)} possible.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
            ShowAnalysisColumns = false;
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
            ShowAnalysisColumns = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Triggered by SfDataGrid's CurrentCellEndEdit event after a user
    /// finishes editing a cell. Runs live duplicate detection for the
    /// affected row. Results show up in the row's Match / Status columns
    /// and the row is tinted with the live-edit (light-blue) highlight.
    /// </summary>
    public async Task OnCellCommittedAsync(Customer edited, CancellationToken ct)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusMessage = $"Live check for {edited.Id}…";

            // While we're checking, the row is in "Editing" state.
            edited.RowHighlight = RowHighlight.PossibleDuplicate;
            edited.Status = "Editing";
            edited.IsEditing = true;

            var others = Customers.Where(c => c.Id != edited.Id).ToList();
            var pairs = _generator.BuildPairs(new[] { edited }.Concat(others))
                .Where(p => p.A.Id == edited.Id || p.B.Id == edited.Id)
                .ToList();

            if (pairs.Count == 0)
            {
                StatusMessage = $"No candidate matches for {edited.Id}.";
                edited.Status = "Unique";
                edited.Match = "—";
                edited.IsEditing = false;
                edited.RowHighlight = RowHighlight.None;
                return;
            }

            var results = await _analyzer.AnalyzeAsync(pairs, ct);
            var all = new[] { edited }.Concat(others).ToList();
            var addressOnlyResults = BuildIdenticalAddressResults(all, all);
            foreach (var extra in addressOnlyResults)
            {
                if (results.Any(r =>
                    (r.RecordAId == extra.RecordAId && r.RecordBId == extra.RecordBId) ||
                    (r.RecordAId == extra.RecordBId && r.RecordBId == extra.RecordAId)))
                    continue;
                if (extra.RecordAId != edited.Id && extra.RecordBId != edited.Id) continue;
                results.Add(extra);
            }

            ApplyResults(results, focusRowId: edited.Id, source: HighlightSource.LiveEdit);
            StatusMessage = $"Live check complete for {edited.Id} — {results.Count} candidate pair(s) evaluated.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Live check error: " + ex.Message;
            edited.Status = "Unique";
            edited.Match = "—";
            edited.IsEditing = false;
            edited.RowHighlight = RowHighlight.None;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private enum HighlightSource { LiveEdit, Scan }

    private void ApplyResults(List<DuplicateResult> results, string? focusRowId = null, HighlightSource source = HighlightSource.Scan)
    {
        // 1. Clear stale highlights / match / status from rows that aren't affected by the new results.
        var affectedIds = new HashSet<string>();
        foreach (var r in results)
        {
            if (r.Classification == DuplicateClassification.NotDuplicate) continue;
            affectedIds.Add(r.RecordAId);
            affectedIds.Add(r.RecordBId);
        }

        foreach (var c in Customers)
        {
            if (focusRowId is not null && c.Id != focusRowId) continue;
            if (!affectedIds.Contains(c.Id))
            {
                c.RowHighlight = RowHighlight.None;
                c.Match = "—";
                c.Status = "Unique";
                c.IsDuplicate = false;
                c.IsEditing = false;
            }
        }

        // 2. For each row, take the strongest verdict it appears in.
        var bestById = new Dictionary<string, (DuplicateResult result, RowHighlight highlight)>();
        foreach (var r in results)
        {
            if (r.Classification == DuplicateClassification.NotDuplicate) continue;

            var tier = (int)r.RowHighlight;
            foreach (var id in new[] { r.RecordAId, r.RecordBId })
            {
                if (focusRowId is not null && id != focusRowId) continue; // live check: only the edited row

                if (!bestById.TryGetValue(id, out var existing) || tier > (int)existing.highlight)
                    bestById[id] = (r, r.RowHighlight);
            }
        }

        // 3. Paint rows.
        foreach (var c in Customers)
        {
            if (focusRowId is not null && c.Id != focusRowId) continue;

            if (bestById.TryGetValue(c.Id, out var best))
            {
                c.Match = $"{best.result.Confidence}%";
                c.Status = best.result.Classification switch
                {
                    DuplicateClassification.LikelyDuplicate   => "Likely duplicate",
                    DuplicateClassification.PossibleDuplicate => "Possible duplicate",
                    _ => "Unique"
                };

                c.IsDuplicate = best.result.Classification is
                    DuplicateClassification.LikelyDuplicate or
                    DuplicateClassification.PossibleDuplicate;

                c.IsEditing = false;
                c.RowHighlight = source == HighlightSource.LiveEdit
                    ? RowHighlight.PossibleDuplicate
                    : best.highlight;
            }
            else
            {
                c.Match = "—";
                c.Status = "Unique";
                c.IsDuplicate = false;
                c.IsEditing = false;
                c.RowHighlight = RowHighlight.None;
            }
        }

        // 4. Reset and refill the duplicates review list.
        Duplicates.Clear();
        var toShow = focusRowId is null
            ? results
            : results.Where(r => r.RecordAId == focusRowId || r.RecordBId == focusRowId);
        foreach (var r in toShow) Duplicates.Add(r);
    }

    /// <summary>
    /// Returns a new list of DuplicateResults containing the AI's existing
    /// results, plus a PossibleDuplicate (75% confidence) for every pair of
    /// customers that share an identical normalized address. If a same-address
    /// pair was already classified LikelyDuplicate by the AI, the AI's
    /// verdict is kept (we never downgrade). The AI never gets to overwrite a
    /// real duplicate with a weaker verdict.
    /// </summary>
    private static List<DuplicateResult> MergeIdenticalAddressResults(List<DuplicateResult> existingResults,IEnumerable<Customer> customers)
    {
        var merged = new List<DuplicateResult>(existingResults);
        var list = customers.ToList();
        var groups = list
            .Where(c => !string.IsNullOrWhiteSpace(c.Address))
            .GroupBy(c => StringSimilarity.NormalizeAddress(c.Address),
                     StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0 && g.Count() > 1);

        int seq = 0;
        foreach (var group in groups)
        {
            var members = group.ToList();
            for (int i = 0; i < members.Count; i++)
            for (int j = i + 1; j < members.Count; j++)
            {
                var a = members[i];
                var b = members[j];
                var existing = merged.FirstOrDefault(r =>
                    (r.RecordAId == a.Id && r.RecordBId == b.Id) ||
                    (r.RecordAId == b.Id && r.RecordBId == a.Id));

                if (existing is not null && existing.Classification == DuplicateClassification.LikelyDuplicate)
                {
                    continue;
                }

                var local = new DuplicateResult
                {
                    PairId    = $"ADDR{++seq:D4}",
                    RecordAId = a.Id,
                    RecordBId = b.Id,
                    Confidence = 75,
                    Classification = DuplicateClassification.PossibleDuplicate,
                    RowHighlight  = RowHighlight.PossibleDuplicate,
                    GroupId       = $"ADDR-GROUP-{group.Key}",
                    Summary       = "Same normalized address as another record.",
                    SupportingSignals = new()
                    {
                        new SupportingSignal
                        {
                            Field = "address",
                            Strength = "moderate",
                            Reason  = $"Both records share the address “{a.Address}”."
                        }
                    }
                };

                if (existing is null)
                {
                    merged.Add(local);
                }
                else
                {
                    var idx = merged.IndexOf(existing);
                    merged[idx] = local;
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Build (but do not apply) all identical-address pair results across
    /// <paramref name="universe"/>. Returns PossibleDuplicate at 75%.
    /// Used by the live-typing and live-edit paths, which don't want to
    /// mutate the AI's result list — they just want to paint the
    /// matching rows directly.
    /// </summary>
    private static List<DuplicateResult> BuildIdenticalAddressResults(IEnumerable<Customer> universe,IEnumerable<Customer> _)
    {
        var list = universe.ToList();
        var byAddress = list
            .Where(c => !string.IsNullOrWhiteSpace(c.Address))
            .GroupBy(c => StringSimilarity.NormalizeAddress(c.Address),
                     StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0 && g.Count() > 1);

        var results = new List<DuplicateResult>();
        int seq = 0;
        foreach (var group in byAddress)
        {
            var members = group.ToList();
            for (int i = 0; i < members.Count; i++)
            for (int j = i + 1; j < members.Count; j++)
            {
                var a = members[i];
                var b = members[j];
                results.Add(new DuplicateResult
                {
                    PairId    = $"ADDR{++seq:D4}",
                    RecordAId = a.Id,
                    RecordBId = b.Id,
                    Confidence = 75,
                    Classification = DuplicateClassification.PossibleDuplicate,
                    RowHighlight  = RowHighlight.PossibleDuplicate,
                    GroupId       = $"ADDR-GROUP-{group.Key}",
                    Summary       = "Same normalized address as another record.",
                    SupportingSignals = new()
                    {
                        new SupportingSignal
                        {
                            Field = "address",
                            Strength = "moderate",
                            Reason  = $"Both records share the address “{a.Address}”."
                        }
                    }
                });
            }
        }

        return results;
    }
}
