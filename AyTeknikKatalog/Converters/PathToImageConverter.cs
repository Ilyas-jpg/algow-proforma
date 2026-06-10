using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AyTeknikKatalog.Converters;

public class PathToImageConverter : IValueConverter
{
    // Aynı dosya yolu için tekrar tekrar BitmapImage yaratmamak için ufak cache.
    // WeakReference kullanılır — referans tutmadığı için GC normal şekilde toplar.
    // Key normalize: tam yol + LastWriteTimeUtc (dosya değişince eskisini atlar).
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        if (!File.Exists(path)) return null;

        string key;
        try
        {
            var info = new FileInfo(path);
            key = Path.GetFullPath(path).ToLowerInvariant() + "|" + info.LastWriteTimeUtc.Ticks;
        }
        catch
        {
            key = path;
        }

        if (_cache.TryGetValue(key, out var weak) && weak.TryGetTarget(out var cached))
            return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            _cache[key] = new WeakReference<BitmapImage>(bitmap);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
