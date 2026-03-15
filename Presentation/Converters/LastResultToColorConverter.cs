using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>
/// Converts last-result value to circle color: 0 = gray (empty), 1 = green (win), 2 = red (loss).
/// </summary>
public sealed class LastResultToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int v)
            return Colors.Gray;
        return v switch
        {
            1 => Colors.Green,
            2 => Colors.Red,
            _ => Colors.Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
