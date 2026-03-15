using System.Globalization;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class MatchStatusToStartButtonVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MatchStatus status && status == MatchStatus.Scheduled;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
