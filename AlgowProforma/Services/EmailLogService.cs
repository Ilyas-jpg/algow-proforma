using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// E-posta gönderim logu — JSONL (satır başına bir kayıt). Append O(1) dosya-sonu eklemesidir;
/// eski format (tek JSON array — her mail tüm dosyayı yeniden yazardı, toplu gönderimde O(n²))
/// ilk Append'te bir defaya mahsus JSONL'a göç eder. Yarım satır (çökme anı) okuyucuda atlanır —
/// kayıp tek kayıtla sınırlı kalır.
///
/// Yıllık rotasyon: aktif dosyanın son yazımı geçmiş yıla aitse Append öncesi
/// "email-send-log-{yıl}.json" arşivine devrilir — aktif dosya küçük kalır, Load tüm arşivleri
/// birleştirir (Gönderim Geçmişi penceresi yılları kesintisiz görür).
/// </summary>
public class EmailLogService
{
    private static readonly JsonSerializerOptions LineOptions = new()
    {
        WriteIndented = false,                    // satır = tek kayıt; girinti JSONL'ı bozar
        Encoder = JsonStore.Options.Encoder,      // Türkçe karakter düz UTF-8 (JsonStore ile aynı)
        PropertyNameCaseInsensitive = true,
    };

    public List<EmailSendLog> Load()
    {
        var list = new List<EmailSendLog>();
        foreach (var path in ActivePlusArchives())
        {
            try { list.AddRange(Parse(File.ReadAllText(path))); }
            catch { /* okunamayan arşiv kalanları engellemez */ }
        }
        return list.OrderByDescending(l => l.SentAt).ToList();
    }

    /// <summary>Aktif dosya + yıl arşivleri ("email-send-log-2025.json" ...).</summary>
    private static IEnumerable<string> ActivePlusArchives()
    {
        var path = AppPaths.EmailLogsFile;
        if (File.Exists(path)) yield return path;

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) yield break;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        foreach (var f in Directory.GetFiles(dir, $"{stem}-*{ext}"))
            yield return f;
    }

    public void Append(EmailSendLog log)
    {
        var path = AppPaths.EmailLogsFile;
        RotateIfYearChanged(path);
        MigrateLegacyArray(path);
        var line = JsonSerializer.Serialize(log, LineOptions) + Environment.NewLine;
        // Çökme anında son satır '\n'siz yarım kalmış olabilir — üstüne yazmak yerine yeni satıra
        // başla; yoksa yarım satır SONRAKİ kaydı da zehirler (iki kayıt birden okunamaz olur).
        if (LastByteIsNotNewline(path)) line = Environment.NewLine + line;
        File.AppendAllText(path, line);
    }

    /// <summary>Aktif log geçmiş yıldan kalmaysa "{stem}-{yıl}{ext}" arşivine devir (yedeksiz ezme yok:
    /// arşiv adı doluysa ardışık ek alır). Append yolunda tek LastWriteTime okuması — O(1) korunur.</summary>
    private static void RotateIfYearChanged(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            int fileYear = File.GetLastWriteTime(path).Year;
            if (fileYear >= DateTime.Now.Year) return;

            var dir = Path.GetDirectoryName(path) ?? "";
            var stem = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var archive = Path.Combine(dir, $"{stem}-{fileYear}{ext}");
            int n = 2;
            while (File.Exists(archive)) archive = Path.Combine(dir, $"{stem}-{fileYear} ({n++}){ext}");
            File.Move(path, archive);
        }
        catch { /* rotasyon başarısız — append aynı dosyaya devam eder, veri kaybolmaz */ }
    }

    /// <summary>Append başına TÜM dosyayı okumadan ilk anlamlı baytı yoklar (O(1) vaadi).</summary>
    private static bool StartsWithArrayMarker(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> buf = stackalloc byte[64];
            int read = fs.Read(buf);
            for (int i = 0; i < read; i++)
            {
                byte b = buf[i];
                if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') continue;
                if (i == 0 && b == 0xEF && read >= 3 && buf[1] == 0xBB && buf[2] == 0xBF) { i += 2; continue; } // BOM
                return b == (byte)'[';
            }
            return false;
        }
        catch { return false; }
    }

    private static bool LastByteIsNotNewline(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return false;
            fs.Seek(-1, SeekOrigin.End);
            return fs.ReadByte() != '\n';
        }
        catch { return false; }
    }

    /// <summary>Array (eski) veya JSONL (yeni) içeriği okur. (internal: format-uyumluluk testleri)</summary>
    internal static List<EmailSendLog> Parse(string text)
    {
        if (text.TrimStart().StartsWith('['))
        {
            try { return JsonSerializer.Deserialize<List<EmailSendLog>>(text, JsonStore.Options) ?? new(); }
            catch { /* karışık içerik (yarım migrasyon ardına append) — satır moduna düş */ }
        }
        var list = new List<EmailSendLog>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] != '{') continue;
            try
            {
                var item = JsonSerializer.Deserialize<EmailSendLog>(line, LineOptions);
                if (item != null) list.Add(item);
            }
            catch { /* yarım/bozuk satır — tek kayıt feda, kalanı yaşar */ }
        }
        return list;
    }

    /// <summary>Eski tek-array dosyayı bir defaya mahsus JSONL'a çevirir (atomik yazım).
    /// Yeniden yazmadan ÖNCE orijinal ".pre-jsonl.bak" kopyasına alınır: array-parse başarısız olup
    /// satır moduna düşülen KARIŞIK dosyada (array + ardına eklenmiş satırlar) array içindeki çok
    /// satırlı kayıtlar kurtarılamaz — yedek olmadan rewrite onları kalıcı yutuyordu.</summary>
    private static void MigrateLegacyArray(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            if (!StartsWithArrayMarker(path)) return;   // zaten JSONL — tam okuma YOK (O(1))

            var text = File.ReadAllText(path);
            var logs = Parse(text);
            if (logs.Count == 0 && text.Trim().Length > 2)
            {
                // Array görünümlü ama parse edilemedi — üzerine yazıp veri yok etme; kopyala, dokunma.
                AtomicFile.BackupCorrupt(path);
                return;
            }
            try { File.Copy(path, path + ".pre-jsonl.bak", overwrite: true); }
            catch { /* yedek alınamadıysa migrasyonu yine de durdurma — en kötüsü eski davranış */ }
            var sb = new StringBuilder();
            foreach (var l in logs.OrderBy(l => l.SentAt))   // dosya kronolojik akar, Load desc sıralar
                sb.AppendLine(JsonSerializer.Serialize(l, LineOptions));
            AtomicFile.WriteAllText(path, sb.ToString());
        }
        catch { /* migrasyon başarısız — Append yine sona ekler, Load satır-fallback ile okur */ }
    }
}
