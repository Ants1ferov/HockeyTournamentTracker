using System.Globalization;

namespace HockeyTournamentTracker.Presentation.Converters;

/// <summary>Преобразует #RRGGBB или RRGGBB в Color; пустое значение — Transparent.</summary>
public sealed class HexColorToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return Colors.Transparent;

        s = s.Trim();
        if (s.StartsWith('#'))
            s = s[1..];
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            var r = (byte)((rgb >> 16) & 0xff);
            var g = (byte)((rgb >> 8) & 0xff);
            var b = (byte)(rgb & 0xff);
            return Color.FromRgb(r, g, b);
        }

        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
