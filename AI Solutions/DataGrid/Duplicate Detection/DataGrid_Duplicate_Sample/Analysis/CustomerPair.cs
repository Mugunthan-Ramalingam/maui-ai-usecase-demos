namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Lightweight pair representation sent to the AI analyzer. Mirrors the schema
/// the system prompt expects and carries the pre-computed heuristics so the
/// model doesn't have to re-derive them.
/// </summary>
public class CustomerPair
{
    public string PairId { get; set; } = string.Empty;
    public CustomerSummary A { get; set; } = new();
    public CustomerSummary B { get; set; } = new();
}

public class CustomerSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
}
