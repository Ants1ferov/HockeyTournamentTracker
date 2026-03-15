using System.Globalization;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class StageTypeToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StageType type)
            return string.Empty;
        return type switch
        {
            StageType.Swiss => AppResources.StageTypeSwiss,
            StageType.PlayOff => AppResources.StageTypePlayOff,
            _ => value.ToString() ?? string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
