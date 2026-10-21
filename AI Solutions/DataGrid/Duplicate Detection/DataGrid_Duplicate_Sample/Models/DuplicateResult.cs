namespace DataGrid_Duplicate_Sample;

public enum DuplicateClassification
{
    NotDuplicate,
    PossibleDuplicate,
    LikelyDuplicate
}

public enum RowHighlight
{
    None,
    PossibleDuplicate,
    LikelyDuplicate
}

public class SupportingSignal
{
    public string Field { get; set; } = string.Empty;       // name | email | phone | address | company
    public string Strength { get; set; } = "weak";          // strong | moderate | weak
    public string Reason { get; set; } = string.Empty;
}

public class ConflictingSignal
{
    public string Field { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class DuplicateResult
{
    public string PairId { get; set; } = string.Empty;
    public string RecordAId { get; set; } = string.Empty;
    public string RecordBId { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public DuplicateClassification Classification { get; set; }
    public RowHighlight RowHighlight { get; set; } = RowHighlight.None;
    public string? GroupId { get; set; }
    public List<SupportingSignal> SupportingSignals { get; set; } = new();
    public List<ConflictingSignal> ConflictingSignals { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class DuplicateAnalysisResponse
{
    public List<DuplicateResult> Results { get; set; } = new();
}
