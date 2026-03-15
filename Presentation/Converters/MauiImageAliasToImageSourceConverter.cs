using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>Converts a MauiImage alias (filename without extension) to ImageSource for binding.</summary>
public sealed class MauiImageAliasToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string alias && !string.IsNullOrWhiteSpace(alias))
                return ImageSource.FromFile(alias);
        }
        catch
        {
            // Игнорируем ошибки загрузки изображения, чтобы не ломать страницу
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
