using System.Globalization;

namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Maps a row's duplicate status to a row background <see cref="Brush"/>
/// used by the SfDataGrid. <c>DataGridRow.Background</c> and
/// <c>DataGridCell.Background</c> both expect a Brush (not a Color), so this
/// converter wraps the color in a SolidColorBrush.
/// </summary>
public class RowHighlightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RowHighlight h)
        {
            var color = h switch
            {
                RowHighlight.PossibleDuplicate => Color.FromArgb("#DBEAFE"),  // light blue
                RowHighlight.LikelyDuplicate => Color.FromArgb("#FEF3C7"),  // light amber
                _ => Colors.Transparent
            };
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
