using System;
using System.Collections.Generic;
using System.Linq;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

/// <summary>
/// Kalıcı, yeniden-kullanılabilir GÖRSEL ÜRÜN KÜTÜPHANESİ (product-library.json). Katalog-bağımsız:
/// bir kez kaydedilen ürün (görseli dahil) tüm yeni kataloglarda tekrar kullanılır. PriceBook desenini
/// aynalar. Görsel yaşam-döngüsü REFERENCE-COUNTED: kütüphane ürünü paylaşılan images\{guid}.png'yi
/// tutar; LibraryService.CleanupOrphanImages kütüphane görsellerini korur → fiziksel kopya gerekmez.
/// </summary>
public class ProductLibraryService
{
    public List<Product> Load() => JsonStore.LoadList<Product>(AppPaths.ProductLibraryFile);

    public void Save(IEnumerable<Product> items) =>
        JsonStore.SaveList(AppPaths.ProductLibraryFile, items);

    /// <summary>
    /// Gelen ürünleri kütüphaneye ekler/günceller. Tekilleştirme anahtarı: Kod (varsa) yoksa Ad
    /// (büyük/küçük harf duyarsız). Aynı anahtar varsa GÜNCELLER, yoksa Clone'unu ekler (snapshot).
    /// Yeni eklenen ürün sayısını döndürür.
    /// </summary>
    public static int UpsertAll(IList<Product> library, IEnumerable<Product> incoming)
    {
        int added = 0;
        foreach (var src in incoming)
        {
            if (src is null) continue;
            var key = DedupKey(src);
            Product? existing = string.IsNullOrEmpty(key)
                ? null
                : library.FirstOrDefault(x => string.Equals(DedupKey(x), key, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                library.Add(src.Clone());   // snapshot — kütüphane katalog instance'ına bağlı kalmaz
                added++;
            }
            else
            {
                existing.Name = src.Name;
                existing.Code = src.Code;
                existing.Price = src.Price;
                existing.Currency = src.Currency;
                existing.ImagePath = src.ImagePath;
                existing.IsFeatured = src.IsFeatured;
                existing.HasTable = src.HasTable;
                existing.Table = src.Table?.Clone();
            }
        }
        return added;
    }

    /// <summary>Kütüphane ürününü kataloğa eklenebilir BAĞIMSIZ kopyaya çevirir (yeni Id).</summary>
    public static Product ToCatalogProduct(Product libraryItem)
    {
        var p = libraryItem.Clone();
        p.Id = Guid.NewGuid().ToString("N");
        return p;
    }

    private static string DedupKey(Product p)
    {
        var code = (p.Code ?? "").Trim();
        return code.Length > 0 ? code : (p.Name ?? "").Trim();
    }
}
