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
        var path = AppPaths.EmailLogsFile;
        if (!File.Exists(path)) return new();
        string text;
        try { text = File.ReadAllText(path); }
        catch { return new(); }
        return Parse(text).OrderByDescending(l => l.SentAt).ToList();
    }

    public void Append(EmailSendLog log)
    {
        var path = AppPaths.EmailLogsFile;
        MigrateLegacyArray(path);
        var line = JsonSerializer.Serialize(log, LineOptions) + Environment.NewLine;
        // Çökme anında son satır '\n'siz yarım kalmış olabilir — üstüne yazmak yerine yeni satıra
        // başla; yoksa yarım satır SONRAKİ kaydı da zehirler (iki kayıt birden okunamaz olur).
        if (LastByteIsNotNewline(path)) line = Environment.NewLine + line;
        File.AppendAllText(path, line);
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

    /// <summary>Eski tek-array dosyayı bir defaya mahsus JSONL'a çevirir (atomik yazım).</summary>
    private static void MigrateLegacyArray(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path);
            if (!text.TrimStart().StartsWith('[')) return;   // zaten JSONL
            var logs = Parse(text);
            if (logs.Count == 0 && text.Trim().Length > 2)
            {
                // Array görünümlü ama parse edilemedi — üzerine yazıp veri yok etme; kopyala, dokunma.
                AtomicFile.BackupCorrupt(path);
                return;
            }
            var sb = new StringBuilder();
            foreach (var l in logs.OrderBy(l => l.SentAt))   // dosya kronolojik akar, Load desc sıralar
                sb.AppendLine(JsonSerializer.Serialize(l, LineOptions));
            AtomicFile.WriteAllText(path, sb.ToString());
        }
        catch { /* migrasyon başarısız — Append yine sona ekler, Load satır-fallback ile okur */ }
    }
}
