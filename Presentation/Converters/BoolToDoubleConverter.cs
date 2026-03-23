using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>Параметр: "trueValue|falseValue" через |, например "86|0".</summary>
public sealed class BoolToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var whenTrue = 86.0;
        var whenFalse = 0.0;
        if (parameter is string s && s.Contains('|', StringComparison.Ordinal))
        {
            var parts = s.Split('|');
            if (parts.Length >= 2 &&
                double.TryParse(parts[0].Trim(), NumberStyles.Any, culture, out var a) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Any, culture, out var b))
            {
                whenTrue = a;
                whenFalse = b;
            }
        }

        return value is true ? whenTrue : whenFalse;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
