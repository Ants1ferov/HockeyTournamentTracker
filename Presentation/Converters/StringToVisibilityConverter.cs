using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>Конвертер для отображения заголовка группы: непустая строка — видимо, пустая — скрыто.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
