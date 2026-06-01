using System.Globalization;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class MatchStatusToBadgeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MatchStatus status) return Colors.Transparent;
        var dict = Application.Current?.Resources;
        if (dict is null) return Colors.Transparent;
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        string key = status switch
        {
            MatchStatus.InProgress => dark ? "StatusLiveSurfaceDark" : "StatusLiveSurface",
            MatchStatus.Finished   => dark ? "StatusFinishedSurfaceDark" : "StatusFinishedSurface",
            MatchStatus.Cancelled  => dark ? "StatusFinishedSurfaceDark" : "StatusFinishedSurface",
            _ => string.Empty
        };
        return key.Length > 0 && dict.TryGetValue(key, out var c) ? c : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MatchStatusToBadgeTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MatchStatus status) return Colors.Gray;
        var dict = Application.Current?.Resources;
        if (dict is null) return Colors.Gray;
        string key = status == MatchStatus.InProgress ? "StatusLive" : "StatusFinished";
        return dict.TryGetValue(key, out var c) ? c : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MatchStatusToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MatchStatus status ? status switch
        {
            MatchStatus.InProgress => "LIVE",
            MatchStatus.Finished   => "Завершён",
            MatchStatus.Cancelled  => "Отменён",
            _                      => string.Empty
        } : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MatchStatusBadgeVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MatchStatus status && status != MatchStatus.Scheduled;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
