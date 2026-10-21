namespace DataGrid_Duplicate_Sample;

public interface IDuplicateAnalyzer
{
    Task<List<DuplicateResult>> AnalyzeAsync(IReadOnlyList<CustomerPair> candidatePairs, CancellationToken cancellationToken = default);
}
