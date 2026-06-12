using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// Teklif kalıcılığı (her teklif ayrı JSON: Teklifler\{Id}.json) + numaralandırma + arşiv.
/// Numara KAYITTA atanır (atılan taslak numara tüketmez → numara atlaması olmaz).
/// </summary>
public class QuoteService
{
    /// <summary>Teklifi kaydeder. QuoteNo boşsa yeni numara üretir. PDF ayrıca QuotePdfService ile üretilir.</summary>
    public void Save(Quote quote)
    {
        if (string.IsNullOrWhiteSpace(quote.QuoteNo))
            quote.QuoteNo = GenerateNextNo();
        quote.UpdatedAt = DateTime.Now;

        var path = Path.Combine(AppPaths.QuotesDir, quote.Id + ".json");
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(quote, JsonStore.Options));   // atomik (R-1)
    }

    public Quote? Load(string id)
    {
        var path = Path.Combine(AppPaths.QuotesDir, id + ".json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<Quote>(File.ReadAllText(path), JsonStore.Options); }
        catch { AtomicFile.BackupCorrupt(path); return null; }
    }

    /// <summary>Tüm teklifleri yükler (en yeni önce). Bozuk dosyaları atlar.</summary>
    public List<Quote> LoadAll()
    {
        var list = new List<Quote>();
        foreach (var file in Directory.GetFiles(AppPaths.QuotesDir, "*.json"))
        {
            try
            {
                var q = JsonSerializer.Deserialize<Quote>(File.ReadAllText(file), JsonStore.Options);
                if (q is not null) list.Add(q);
            }
            catch { /* bozuk dosya — atla */ }
        }
        return list.OrderByDescending(q => q.CreatedAt).ToList();
    }

    public void Delete(string id)
    {
        var path = Path.Combine(AppPaths.QuotesDir, id + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Teklif PDF dosya adı — revizyonlar "-rev{n}" eki alır (önceki revizyonun PDF'inin
    /// üstüne yazmayı önler). Editör ve Teklifler panosu aynı adı buradan türetir.</summary>
    public static string PdfFileName(Quote q)
    {
        var safeNo = string.IsNullOrWhiteSpace(q.QuoteNo) ? q.Id : q.QuoteNo;
        return safeNo + (q.Revision > 0 ? $"-rev{q.Revision}" : "") + ".pdf";
    }

    private static readonly object NoLock = new();

    /// <summary>
    /// Sıradaki teklif numarası. Ön ek <see cref="AppSettings.QuoteNoPrefix"/>'ten gelir
    /// (örn. "TKF" → TKF-{yıl}-{0001}); boşsa salt {yıl}-{0001}. Yıl bazlı sayaç counters.json'da tutulur.
    /// </summary>
    public string GenerateNextNo() => GenerateNextNo(new SettingsService().Load().QuoteNoPrefix);

    /// <summary>Test edilebilir çekirdek: ön eki açıkça alır (settings I/O olmadan).</summary>
    internal string GenerateNextNo(string? prefix)
    {
        // Çok pencere açıkken aynı anda kayıt → aynı teklif no (fiş mükerrer) riskini engelle.
        lock (NoLock)
        {
            int year = DateTime.Today.Year;
            var counters = JsonStore.LoadOrNew<Dictionary<string, int>>(AppPaths.CountersFile);
            var key = year.ToString();
            int next = counters.TryGetValue(key, out var n) ? n + 1 : 1;
            counters[key] = next;
            JsonStore.SaveObject(AppPaths.CountersFile, counters);
            return FormatNo(prefix, year, next);
        }
    }

    /// <summary>Numara biçimi (yan etkisiz, test edilebilir): ön ek boşsa "{yıl}-{0001}", doluysa "{ÖNEK}-{yıl}-{0001}".</summary>
    internal static string FormatNo(string? prefix, int year, int seq)
    {
        var p = (prefix ?? "").Trim();
        return string.IsNullOrEmpty(p) ? $"{year}-{seq:0000}" : $"{p}-{year}-{seq:0000}";
    }
}
