using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AlgowProforma.Services;

namespace AlgowProforma.Converters;

/// <summary>
/// Excel önizleme satırı → küçük resim. Lazy-extract sonrası görsel henüz diskte OLMAYABİLİR;
/// bu converter önce diske inmiş yolu, yoksa bellekteki gömülü baytları, en son hücredeki
/// dosya-yolu kaynağını dener. DecodePixelWidth ile yalnız thumbnail boyutunda decode eder
/// (56px hücre için tam-boy decode israftır).
/// </summary>
public class ExcelRowToImageConverter : IValueConverter
{
    // Satır örneği yaşadıkça thumbnail'i tut (virtualization recycling'de yeniden decode etme);
    // satır GC olunca girdi de düşer.
    private static readonly ConditionalWeakTable<ExcelImportPreviewRow, BitmapImage> _cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ExcelImportPreviewRow row) return null;
        if (_cache.TryGetValue(row, out var cached)) return cached;

        var bmp = Create(row);
        if (bmp != null) _cache.Add(row, bmp);
        return bmp;
    }

    private static BitmapImage? Create(ExcelImportPreviewRow row)
    {
        try
        {
            if (row.PendingImageBytes is { Length: > 0 })
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(row.PendingImageBytes, writable: false);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 112;   // 56px hücre × 2 (yüksek DPI payı)
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }

            var path = !string.IsNullOrWhiteSpace(row.ImagePath) ? row.ImagePath
                     : !string.IsNullOrWhiteSpace(row.PendingImageSource) ? row.PendingImageSource
                     : null;
            if (path is null || !File.Exists(path)) return null;

            var fromFile = new BitmapImage();
            fromFile.BeginInit();
            fromFile.UriSource = new Uri(path, UriKind.Absolute);
            fromFile.CacheOption = BitmapCacheOption.OnLoad;
            fromFile.DecodePixelWidth = 112;
            fromFile.EndInit();
            fromFile.Freeze();
            return fromFile;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
