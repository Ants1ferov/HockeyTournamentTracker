using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>Возвращает true, когда value (int) равен parameter (int). Для привязки видимости вкладок по SelectedTabIndex.</summary>
public sealed class IndexEqualsToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int index || parameter is null)
            return false;
        if (parameter is int p)
            return index == p;
        if (int.TryParse(parameter.ToString(), out var parsed))
            return index == parsed;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
