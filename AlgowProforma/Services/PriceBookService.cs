using System;
using System.Collections.Generic;
using System.Linq;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// Fiyat havuzu kalıcılığı + toplu işlemler. Tek JSON dosyası (pricebook.json).
/// </summary>
public class PriceBookService
{
    public List<PriceItem> Load() => JsonStore.LoadList<PriceItem>(AppPaths.PriceBookFile);

    public void Save(IEnumerable<PriceItem> items) =>
        JsonStore.SaveList(AppPaths.PriceBookFile, items);

    /// <summary>Kod ile tekilleştirilmiş birleştirme (Excel içe aktarımda dedup). Aynı kod varsa GÜNCELLER.</summary>
    public static void UpsertByCode(IList<PriceItem> target, PriceItem incoming)
    {
        var code = (incoming.Code ?? "").Trim();
        PriceItem? existing = string.IsNullOrEmpty(code)
            ? null
            : target.FirstOrDefault(p => string.Equals((p.Code ?? "").Trim(), code, StringComparison.OrdinalIgnoreCase));

        if (existing is null) { target.Add(incoming); return; }

        existing.Name = incoming.Name;
        existing.Description = incoming.Description;
        existing.Unit = incoming.Unit;
        existing.UnitPrice = incoming.UnitPrice;
        existing.Currency = incoming.Currency;
        existing.VatRate = incoming.VatRate;
        existing.Category = incoming.Category;
        existing.UpdatedAt = DateTime.Now;
    }

    /// <summary>Toplu yüzde zam/indirim (örn +10, -5). Verilen kalemlerin birim fiyatını günceller.</summary>
    public static void ApplyPercentChange(IEnumerable<PriceItem> items, decimal percent)
    {
        foreach (var it in items)
        {
            it.UnitPrice = Math.Round(it.UnitPrice * (1m + percent / 100m), 2, MidpointRounding.AwayFromZero);
            it.UpdatedAt = DateTime.Now;
        }
    }
}
