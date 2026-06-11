using System;
using System.Globalization;
using System.Windows.Data;

namespace AlgowProforma.Converters;

public class PriceFormatConverter : IValueConverter
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return d.ToString("N2", TrCulture);
        return "0,00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && decimal.TryParse(s.Replace(".", "").Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return 0m;
    }
}
