using System.Globalization;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class TournamentStatusToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TournamentStatus status)
        {
            return status switch
            {
                TournamentStatus.Planned => AppResources.StatusPlanned,
                TournamentStatus.InProgress => AppResources.StatusInProgress,
                TournamentStatus.Finished => AppResources.StatusFinished,
                TournamentStatus.Archived => AppResources.StatusArchived,
                _ => value.ToString()
            };
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
