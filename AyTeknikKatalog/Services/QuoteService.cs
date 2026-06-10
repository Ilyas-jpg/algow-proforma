using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

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
        File.WriteAllText(path, JsonSerializer.Serialize(quote, JsonStore.Options));
    }

    public Quote? Load(string id)
    {
        var path = Path.Combine(AppPaths.QuotesDir, id + ".json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<Quote>(File.ReadAllText(path), JsonStore.Options); }
        catch { return null; }
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

    /// <summary>Sıradaki teklif numarası: CRK-{yıl}-{0001}. Yıl bazlı sayaç counters.json'da tutulur.</summary>
    private static readonly object NoLock = new();

    public string GenerateNextNo()
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
            return $"CRK-{year}-{next:0000}";
        }
    }
}
