using System;
using System.Collections.Concurrent;
using System.IO;

namespace AlgowProforma.Services;

/// <summary>
/// QuestPDF paylaşımlı görsel cache'i. Aynı dosya tekrarlı render'larda (kapak önizlemesi her
/// değişiklikte yenilenir, 13 tasarım thumbnail'i, toplu gönderimde mail başına logo, ardışık PDF
/// üretimleri) her seferinde diskten okunup yeniden decode edilmesin. QuestPDF Image nesneleri
/// resmî shared-image API'siyle document'lar arası güvenle paylaşılır.
/// Geçersizleme: tam yol + LastWriteTimeUtc + uzunluk — kullanıcı görseli değiştirirse cache ıskalar.
/// Decode hatası fırlatır (eski .Image(path) davranışıyla aynı sınıf hata; çağıranlar zaten
/// File.Exists guard'lı). Bellek bütçesi aşılınca toptan sıfırlanır (nadir; yeniden dolar).
/// </summary>
internal static class PdfImageCache
{
    private static readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static long _approxBytes;

    private const int MaxEntries = 256;
    private const long MaxApproxBytes = 128L * 1024 * 1024;   // 128 MB kaynak-dosya toplamı

    private sealed record Entry(DateTime StampUtc, long Length, QuestPDF.Infrastructure.Image Image);

    public static QuestPDF.Infrastructure.Image Get(string path)
    {
        var fi = new FileInfo(path);
        if (_cache.TryGetValue(path, out var e) && e.StampUtc == fi.LastWriteTimeUtc && e.Length == fi.Length)
            return e.Image;

        var img = QuestPDF.Infrastructure.Image.FromFile(path);

        if (_cache.Count >= MaxEntries || System.Threading.Interlocked.Read(ref _approxBytes) >= MaxApproxBytes)
        {
            // NOT: evict edilen Image'lar bilinçli olarak Dispose EDİLMEZ — eşzamanlı bir render
            // (kapak önizleme / katalog üretimi / thumbnail) referansı hâlâ kullanıyor olabilir
            // (use-after-dispose = crash). Bellek GC+finalizer ile döner; bütçe yeniden dolumu sınırlar.
            _cache.Clear();
            System.Threading.Interlocked.Exchange(ref _approxBytes, 0);
        }
        var entry = new Entry(fi.LastWriteTimeUtc, fi.Length, img);
        if (_cache.TryAdd(path, entry))
        {
            System.Threading.Interlocked.Add(ref _approxBytes, fi.Length);
        }
        else if (_cache.TryGetValue(path, out var old))
        {
            // Değişen dosya: girdiyi tazele + sayaç farkını işle (eski drift: fark hiç işlenmiyordu,
            // sayaç gerçek toplamdan kopup bütçe sıfırlamasını erken/geç tetikliyordu).
            _cache[path] = entry;
            System.Threading.Interlocked.Add(ref _approxBytes, fi.Length - old.Length);
        }
        else
        {
            _cache[path] = entry;   // yarış: eşzamanlı Clear araya girdi — girdi yeni sayılır
            System.Threading.Interlocked.Add(ref _approxBytes, fi.Length);
        }
        return img;
    }
}
