using System.Globalization;

// Aliases for the Syncfusion toggle-glyph converter below.
// The header template's binding context is a GroupResult, so we
// receive that type at runtime even though XAML can't see it.
using GResult = Syncfusion.Maui.DataSource.Extensions.GroupResult;

namespace ListView_IntentGrouping.Converters;

/// <summary>
/// Returns <c>True</c> when the integer is greater than zero.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a 5-character rating string (e.g. "\u2605\u2605\u2605\u2605\u2606")
/// based on an integer rating from 1 to 5.
/// </summary>
public class RatingToStarsConverter : IValueConverter
{
    private const string Filled = "\u2605";
    private const string Empty = "\u2606";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int rating = value is int i ? Math.Clamp(i, 0, 5) : 0;
        return string.Concat(Enumerable.Repeat(Filled, rating))
             + string.Concat(Enumerable.Repeat(Empty, 5 - rating));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps an integer priority (1-5) to a friendly label:
///   1 = Low, 2-3 = Medium, 4-5 = High.
/// </summary>
public class PriorityToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int priority = value is int i ? i : 0;
        return priority switch
        {
            >= 4 => "High",
            >= 2 => "Medium",
            >= 1 => "Low",
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps an integer priority (1-5) to a background colour that
/// matches the severity. Used to colour-code the priority chip on
/// each item card.
/// </summary>
public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int priority = value is int i ? i : 0;
        return priority switch
        {
            >= 4 => "#A50E0E", // red - high
            >= 2 => "#B45F06", // amber - medium
            >= 1 => "#5F6368", // grey - low
            _ => "#5F6368"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
/// <summary>
/// Maps <see cref="GResult.IsExpand"/> to a rotation in degrees so
/// a down-pointing chevron turns to point right when the group is
/// collapsed.
/// </summary>
public sealed class GroupExpandRotationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isExpanded = value is bool b && b;
        return isExpanded ? 0d : -90d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}