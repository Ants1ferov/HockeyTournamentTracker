using System.Globalization;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class TournamentStatusToBadgeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TournamentStatus status)
            return Colors.Transparent;

        var dict = Application.Current?.Resources;
        if (dict is null) return Colors.Transparent;

        string key = status switch
        {
            TournamentStatus.InProgress => "StatusLiveSurface",
            TournamentStatus.Finished => "StatusFinishedSurface",
            _ => "StatusPlannedSurface"
        };

        if (Application.Current?.RequestedTheme == AppTheme.Dark)
        {
            key = status switch
            {
                TournamentStatus.InProgress => "StatusLiveSurfaceDark",
                TournamentStatus.Finished => "StatusFinishedSurfaceDark",
                _ => "StatusPlannedSurfaceDark"
            };
        }

        return dict.TryGetValue(key, out var color) ? color : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class TournamentStatusToBadgeTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TournamentStatus status)
            return Colors.Black;

        var dict = Application.Current?.Resources;
        if (dict is null) return Colors.Black;

        string key = status switch
        {
            TournamentStatus.InProgress => "StatusLive",
            TournamentStatus.Finished => "StatusFinished",
            _ => "StatusPlanned"
        };

        return dict.TryGetValue(key, out var color) ? color : Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
