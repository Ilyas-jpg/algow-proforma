using System;
using System.IO;

namespace AyTeknikKatalog.Services;

/// <summary>
/// İçe aktarım üst sınırları. Devasa dosya = UI donması / bellek taşması (ClosedXML tüm sayfayı belleğe
/// alır, görsel decode RAM'i şişirir). Sınır aşılırsa açık Türkçe hatayla erken durdurulur (L3).
/// </summary>
public static class ImportLimits
{
    public const long MaxExcelBytes = 25 * 1024 * 1024;   // 25 MB — binlerce satırlık liste bile bunun çok altında
    public const long MaxImageBytes = 20 * 1024 * 1024;   // 20 MB — yüksek çözünürlüklü fotoğraf dahil bol pay

    /// <summary>Excel yoksa veya sınırı aşıyorsa açık hatayla fırlatır (çağıran import UI'ları zaten try/catch'li).</summary>
    public static void EnsureExcelSize(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Excel dosyası bulunamadı.", path);
        if (info.Length > MaxExcelBytes)
            throw new InvalidOperationException(
                $"Excel dosyası çok büyük ({Mb(info.Length)} MB). En fazla {Mb(MaxExcelBytes)} MB desteklenir.");
    }

    /// <summary>Görsel boyutunu kontrol eder; UI seçim akışı için exception yerine bool + mesaj döner.</summary>
    public static bool IsImageSizeOk(string path, out string error)
    {
        error = "";
        var info = new FileInfo(path);
        if (!info.Exists) { error = "Görsel dosyası bulunamadı."; return false; }
        if (info.Length > MaxImageBytes)
        {
            error = $"Görsel çok büyük ({Mb(info.Length)} MB). En fazla {Mb(MaxImageBytes)} MB desteklenir.";
            return false;
        }
        return true;
    }

    private static long Mb(long bytes) => bytes / (1024 * 1024);
}
