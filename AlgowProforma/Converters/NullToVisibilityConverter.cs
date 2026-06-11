using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AlgowProforma.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true,
        };

        var inverted = parameter is string p && p.Equals("Inverted", StringComparison.OrdinalIgnoreCase);
        if (inverted) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
