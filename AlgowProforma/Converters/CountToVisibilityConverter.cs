using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AlgowProforma.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0,
        };

        var inverted = parameter is string p && p.Equals("Inverted", StringComparison.OrdinalIgnoreCase);
        var isEmpty = count == 0;
        var show = inverted ? !isEmpty : isEmpty;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
