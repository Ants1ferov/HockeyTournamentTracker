using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNotNull = value is not null;
        if (IsInvert(parameter))
            return !isNotNull;
        return isNotNull;
    }

    private static bool IsInvert(object? parameter)
    {
        if (parameter is null) return false;
        var s = parameter.ToString();
        return string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
