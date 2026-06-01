using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>
/// true → фон чётной строки таблицы (зебра, тема-зависимо), false → прозрачный.
/// </summary>
public sealed class BoolToZebraColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true)
            return Colors.Transparent;

        var dict = Application.Current?.Resources;
        if (dict is null) return Colors.Transparent;

        var key = Application.Current?.RequestedTheme == AppTheme.Dark
            ? "TableCellAltDark"
            : "TableCellAltLight";
        return dict.TryGetValue(key, out var color) ? color : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
