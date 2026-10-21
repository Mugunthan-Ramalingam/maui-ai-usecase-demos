using CommunityToolkit.Mvvm.ComponentModel;

namespace DataGrid_Duplicate_Sample;

public partial class Customer : ObservableObject
{
    [ObservableProperty] private string? id = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string city = string.Empty;
    [ObservableProperty] private string state = string.Empty;
    [ObservableProperty] private string postalCode = string.Empty;
    [ObservableProperty] private string company = string.Empty;

    /// <summary>
    /// Drives the row background color via RowHighlightConverter.
    /// PossibleDuplicate = light blue (live, while typing).
    /// LikelyDuplicate   = light amber (full scan result).
    /// </summary>
    [ObservableProperty] private RowHighlight rowHighlight = RowHighlight.None;

    /// <summary>
    /// Confidence percentage shown in the Match column ("82%" or "—").
    /// </summary>
    [ObservableProperty] private string match = "—";

    /// <summary>
    /// Status text shown in the Status pill column.
    /// </summary>
    [ObservableProperty] private string status = "Unique";

    /// <summary>
    /// True while the user is actively editing this row's cell.
    /// </summary>
    [ObservableProperty] private bool isEditing;

    /// <summary>
    /// True when the AI scan (or live check) has classified this row as a
    /// possible or likely duplicate. Drives the visibility of the
    /// "delete" affordance in the grid — unique rows never expose a
    /// delete icon.
    /// </summary>
    [ObservableProperty] private bool isDuplicate;
}
