using System.Globalization;

namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Maps a status string to the pill background color used by the
/// Status column. "Possible duplicate" and "Editing" use a light-blue
/// pill; "Unique" uses a light-gray pill; "Likely duplicate" uses a
/// light-amber pill. Anything else returns transparent.
/// </summary>
public class StatusPillColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string ?? string.Empty;
        return s switch
        {
            "Possible duplicate" => Color.FromArgb("#DBEAFE"),  // light blue
            "Editing" => Color.FromArgb("#DBEAFE"),  // light blue
            "Likely duplicate" => Color.FromArgb("#FEF3C7"),  // light amber
            "Unique" => Color.FromArgb("#F1F5F9"),  // light gray
            _ => Colors.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a status string to the pill text color. "Unique" uses a muted
/// gray so it visually recedes; everything else uses a darker shade of
/// its background hue.
/// </summary>
public class StatusPillTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string ?? string.Empty;
        return s switch
        {
            "Possible duplicate" => Color.FromArgb("#1D4ED8"),  // blue-700
            "Editing" => Color.FromArgb("#1D4ED8"),  // blue-700
            "Likely duplicate" => Color.FromArgb("#92400E"),  // amber-800
            "Unique" => Color.FromArgb("#475569"),  // slate-600
            _ => Color.FromArgb("#475569")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
