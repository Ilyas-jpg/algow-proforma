using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AlgowProforma.Models;

namespace AlgowProforma.Converters;

/// <summary>
/// Ürün Id'sini (string) gerçek Product nesnesine çevirir.
/// MultiBinding: values[0] = id, values[1] = Catalog.Products koleksiyonu.
/// </summary>
public class ProductIdLookupConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return null;
        if (values[0] is not string id) return null;
        if (values[1] is not IEnumerable products) return null;
        foreach (var item in products)
            if (item is Product p && p.Id == id)
            {
                // parameter ile alt özelliği seçebiliriz: "Name" (varsayılan) ya da "Object"
                return parameter as string == "Object"
                    ? (object)p
                    : (object)(p.Name ?? string.Empty);
            }
        return null;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
