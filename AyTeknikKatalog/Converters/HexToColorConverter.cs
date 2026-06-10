using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AyTeknikKatalog.Converters;

/// <summary>
/// Hex string (#RRGGBB veya boş) → System.Windows.Media.Color. Boş ise Transparent.
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Colors.Transparent;
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex.StartsWith("#") ? hex : "#" + hex);
        }
        catch
        {
            return Colors.Black;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
