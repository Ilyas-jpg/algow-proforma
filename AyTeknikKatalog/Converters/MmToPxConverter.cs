using System;
using System.Globalization;
using System.Windows.Data;

namespace AyTeknikKatalog.Converters;

/// <summary>
/// CoverElement mm değerlerini canvas pixel'e çevirir. values[0] = mm, values[1] = scale (px/mm).
/// </summary>
public class MmToPxConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;
        double mm = System.Convert.ToDouble(values[0], CultureInfo.InvariantCulture);
        double scale = System.Convert.ToDouble(values[1], CultureInfo.InvariantCulture);
        return mm * scale;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
