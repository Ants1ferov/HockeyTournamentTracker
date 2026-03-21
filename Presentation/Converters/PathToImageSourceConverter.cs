using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // FromFile кэширует по пути: при замене PNG на диске список мог показывать старую картинку.
        // FromStream каждый раз читает актуальное содержимое файла.
        return ImageSource.FromStream(() => File.OpenRead(path));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
