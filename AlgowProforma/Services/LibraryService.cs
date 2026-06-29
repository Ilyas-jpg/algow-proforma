using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

public class LibraryService
{
    private readonly CatalogService _catalogService = new();
    // PdfService BİLEREK field değil: ApplyTheme/ApplyLayout mutable instance-state taşır.
    // Paylaşılan tek instance, arka-plan Save (Task.Run) ile UI'daki GenerateSingleProduct
    // çakışınca tema/düzen state'i yarışıyordu — her operasyon kendi taze instance'ını kurar.

    public string LibraryPath { get; }

    public LibraryService()
    {
        // Tek kaynak AppPaths.LibraryRoot (üretimde = Belgeler\Algow Proforma Kataloglar).
        LibraryPath = AppPaths.LibraryRoot;
        Directory.CreateDirectory(LibraryPath);
    }

    /// <summary>
    /// images/ klasöründe hiçbir katalog tarafından referans verilmeyen "öksüz" görselleri ayıklar.
    /// Kütüphane açılırken bir kez çağrılır. Kalıcı SİLMEZ — geri-dönülebilir images\.trash'e taşır.
    /// </summary>
    public int CleanupOrphanImages(IReadOnlyCollection<string>? protectedPaths = null)
    {
        if (!Directory.Exists(LibraryPath)) return 0;
        var imagesDir = ImageStorage.Directory;
        if (!Directory.Exists(imagesDir)) return 0;

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 0. Açık çalışma oturumunun (henüz kaydedilmemiş olabilir) görselleri — ASLA silinmez.
        if (protectedPaths != null)
            foreach (var pp in protectedPaths)
                if (!string.IsNullOrWhiteSpace(pp)) referenced.Add(pp);

        // 1. Tüm kayıtlı katalogları DESERIALIZE et, gerçek görsel referanslarını topla.
        //    DL-1 fix: eski "string search" anahtarı tek-backslash, JSON ise çift-backslash (\\) yazıyordu
        //    → IndexOf HİÇ eşleşmiyor → kayıtlı-ama-açık-olmayan katalogların görselleri siliniyordu.
        //    Artık modelden topluyoruz (marka logo + kapak + kapak elementleri + ürünler + referanslar).
        foreach (var jsonPath in Directory.GetFiles(LibraryPath, "*.json"))
        {
            try { CollectImagePaths(_catalogService.Load(jsonPath), referenced); }
            catch { /* katalog değil / bozuk — atla */ }
        }

        // 2. Kalıcı marka profili + crash-recovery autosave katalogu da korunur (DL-2 fix).
        try
        {
            var bp = BrandProfileService.Load();
            if (bp != null && !string.IsNullOrWhiteSpace(bp.LogoPath)) referenced.Add(bp.LogoPath);
        }
        catch { }
        try
        {
            if (File.Exists(AppPaths.AutoSaveFile))
                CollectImagePaths(_catalogService.Load(AppPaths.AutoSaveFile), referenced);
        }
        catch { }

        // 2b. Görsel ürün kütüphanesi (product-library.json) görselleri de korunur — reference-counted (Faz 2).
        //     Bir görsel hiçbir katalogda olmasa bile kütüphanede yaşıyorsa silinmez.
        try
        {
            foreach (var p in new ProductLibraryService().Load())
                if (!string.IsNullOrWhiteSpace(p.ImagePath)) referenced.Add(p.ImagePath);
        }
        catch { }

        // 3. Referansı olmayanları KALICI SİLME yerine .trash'e taşı (geri-dönülebilir, R-5).
        //    14 günden eski trash gerçekten silinir → disk geri kazanılır + 2 haftalık güvenlik penceresi.
        var trashDir = Path.Combine(imagesDir, ".trash");
        PurgeOldTrash(trashDir);

        // Kök-taşınma dayanıklılığı: JSON'daki referanslar yazıldıkları günkü MUTLAK path'i taşır.
        // Belgeler kökü taşınırsa (OneDrive KFM / kullanıcı taşıması) tam-path eşleşmesi TOPLUCA boşa
        // düşer → tüm görseller trash'e gider, 14 günde kalıcı silinirdi. Dosya adları GUID
        // (ImageStorage.CreateNewPath) olduğundan ad, çakışmasız ikinci kimliktir.
        var referencedNames = BuildFileNameSet(referenced);

        int moved = 0;
        foreach (var file in Directory.GetFiles(imagesDir))   // alt klasör (.trash) dahil DEĞİL
        {
            if (IsReferenced(file, referenced, referencedNames)) continue;
            // Yarış koruması: yeni yüklenmiş ama henüz hiçbir JSON'a/kütüphaneye bağlanmamış görsel
            // (Excel import materialize / kapak ekleme) bu anlık taramada "öksüz" görünebilir. Son
            // 5 dk içinde oluşturulanı bu turda atla — gerçekten öksüzse sonraki temizlik alır.
            try { if (File.GetCreationTime(file) > DateTime.Now.AddMinutes(-5)) continue; } catch { }
            try
            {
                Directory.CreateDirectory(trashDir);
                var dest = Path.Combine(trashDir, Path.GetFileName(file));
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(file, dest);
                try { File.SetLastWriteTime(dest, DateTime.Now); } catch { } // trash-zamanı = purge penceresi başlangıcı
                moved++;
            }
            catch { /* read-only / kilitli — atla */ }
        }
        return moved;
    }

    /// <summary>Bir kataloğun kullandığı tüm görsel yollarını topla. (internal: test bu DL-1 çekirdeğini doğrular)</summary>
    internal static void CollectImagePaths(Catalog c, HashSet<string> set)
    {
        void Add(string? p) { if (!string.IsNullOrWhiteSpace(p)) set.Add(p!); }
        Add(c.Brand?.LogoPath);
        Add(c.Cover?.CustomCoverImagePath);
        if (c.Cover?.Elements != null)
            foreach (var el in c.Cover.Elements)
                if (el.Type == CoverElementType.Image) Add(el.Content);
        if (c.Products != null)
            foreach (var p in c.Products) Add(p?.ImagePath);
        if (c.References != null)
            foreach (var r in c.References) Add(r?.LogoPath);
    }

    /// <summary>Referans path'lerinden dosya-adı kümesi çıkarır. (internal: kök-taşınma testleri)</summary>
    internal static HashSet<string> BuildFileNameSet(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var n = Path.GetFileName(p);
            if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
        }
        return names;
    }

    /// <summary>Önce tam path, eşleşmezse GUID dosya-adı üzerinden referans kontrolü.</summary>
    internal static bool IsReferenced(string file, HashSet<string> referencedPaths, HashSet<string> referencedNames)
        => referencedPaths.Contains(file) || referencedNames.Contains(Path.GetFileName(file));

    /// <summary>14 günden eski .trash dosyalarını gerçekten siler (disk reclaim + güvenlik penceresi).</summary>
    private static void PurgeOldTrash(string trashDir)
    {
        if (!Directory.Exists(trashDir)) return;
        var cutoff = DateTime.Now.AddDays(-14);
        foreach (var f in Directory.GetFiles(trashDir))
            try { if (File.GetLastWriteTime(f) < cutoff) File.Delete(f); } catch { }
    }

    public List<CatalogEntry> ListEntries()
    {
        if (!Directory.Exists(LibraryPath)) return new List<CatalogEntry>();
        var entries = new List<CatalogEntry>();

        foreach (var pdfPath in Directory.GetFiles(LibraryPath, "*.pdf"))
        {
            var jsonPath = Path.ChangeExtension(pdfPath, ".json");
            if (!File.Exists(jsonPath)) continue;

            try
            {
                var catalog = _catalogService.Load(jsonPath);
                entries.Add(new CatalogEntry
                {
                    Name = Path.GetFileNameWithoutExtension(pdfPath),
                    PdfPath = pdfPath,
                    JsonPath = jsonPath,
                    CreatedAt = catalog.LastModified,
                    ProductCount = catalog.Products.Count,
                    ReferenceCount = catalog.References.Count,
                    BrandName = string.IsNullOrWhiteSpace(catalog.Brand.Name) ? "(Adsız Katalog)" : catalog.Brand.Name,
                    LibraryLabel = catalog.LibraryLabel,
                });
            }
            catch
            {
            }
        }

        return entries.OrderByDescending(e => e.CreatedAt).ToList();
    }

    public CatalogEntry Save(Catalog catalog)
    {
        var rawName = !string.IsNullOrWhiteSpace(catalog.LibraryLabel)
            ? catalog.LibraryLabel
            : (string.IsNullOrWhiteSpace(catalog.Brand.Name) ? "Katalog" : catalog.Brand.Name);
        var baseName = MakeUniqueBaseName(SafeFileName(rawName), withJson: true);
        var pdfPath = Path.Combine(LibraryPath, $"{baseName}.pdf");
        var jsonPath = Path.Combine(LibraryPath, $"{baseName}.json");

        catalog.LastModified = DateTime.Now;
        new PdfService().Generate(catalog, pdfPath);
        _catalogService.Save(jsonPath, catalog);

        try { File.SetAttributes(jsonPath, FileAttributes.Hidden); } catch { }

        return new CatalogEntry
        {
            Name = baseName,
            PdfPath = pdfPath,
            JsonPath = jsonPath,
            CreatedAt = catalog.LastModified,
            ProductCount = catalog.Products.Count,
            ReferenceCount = catalog.References.Count,
            BrandName = string.IsNullOrWhiteSpace(catalog.Brand.Name) ? "(Adsız Katalog)" : catalog.Brand.Name,
            LibraryLabel = catalog.LibraryLabel,
        };
    }

    public string SaveSingleProduct(Catalog catalog, Product product, string? customLabel = null)
    {
        var rawName = !string.IsNullOrWhiteSpace(customLabel)
            ? customLabel!
            : (!string.IsNullOrWhiteSpace(product.Name) ? product.Name : "Urun");
        var baseName = MakeUniqueBaseName(SafeFileName(rawName), withJson: false, extension: ".png");
        var pngPath = Path.Combine(LibraryPath, $"{baseName}.png");
        new PdfService().GenerateSingleProduct(catalog, product, pngPath);
        return pngPath;
    }

    public CatalogEntry Rename(CatalogEntry entry, string newLabel)
    {
        var catalog = _catalogService.Load(entry.JsonPath);
        catalog.LibraryLabel = newLabel?.Trim() ?? string.Empty;
        try { File.SetAttributes(entry.JsonPath, FileAttributes.Normal); } catch { }
        _catalogService.Save(entry.JsonPath, catalog);
        try { File.SetAttributes(entry.JsonPath, FileAttributes.Hidden); } catch { }
        return new CatalogEntry
        {
            Name = entry.Name,
            PdfPath = entry.PdfPath,
            JsonPath = entry.JsonPath,
            CreatedAt = catalog.LastModified,
            ProductCount = entry.ProductCount,
            ReferenceCount = entry.ReferenceCount,
            BrandName = entry.BrandName,
            LibraryLabel = catalog.LibraryLabel,
        };
    }

    public Catalog Load(CatalogEntry entry) => _catalogService.Load(entry.JsonPath);

    public void Delete(CatalogEntry entry)
    {
        // Kalıcı silme yerine geri-dönülebilir taşıma: kütüphane kökündeki .trash'e iner,
        // 14 günden eskiler purge edilir (images\.trash ile aynı disiplin). ListEntries yalnız
        // kök *.pdf taradığından trash içeriği listede görünmez.
        var trashDir = Path.Combine(LibraryPath, ".trash");
        PurgeOldTrash(trashDir);

        // Önce PDF (kilitlenme adayı — viewer'da açıksa burada fırlar, hiçbir şey taşınmamış olur).
        MoveToTrash(entry.PdfPath, trashDir);
        if (File.Exists(entry.JsonPath))
        {
            try { File.SetAttributes(entry.JsonPath, FileAttributes.Normal); } catch { }
            MoveToTrash(entry.JsonPath, trashDir);
        }
    }

    /// <summary>Dosyayı trash'e taşır; ad çakışmasında eskisini ezer. Kilitli dosyada fırlatır
    /// (çağıran LibraryWindow zaten try/catch + kullanıcı mesajlı).</summary>
    private static void MoveToTrash(string path, string trashDir)
    {
        if (!File.Exists(path)) return;
        Directory.CreateDirectory(trashDir);
        var dest = Path.Combine(trashDir, Path.GetFileName(path));
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(path, dest);
        try { File.SetLastWriteTime(dest, DateTime.Now); } catch { } // trash-zamanı = purge penceresi başlangıcı
    }

    public CatalogEntry? GetMostRecent() => ListEntries().FirstOrDefault();

    private static string SafeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Katalog";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var c in input.Trim())
        {
            if (Array.IndexOf(invalid, c) >= 0) sb.Append('-');
            else sb.Append(c);
        }
        var result = sb.ToString().TrimEnd('.', ' ');
        if (result.Length > 80) result = result[..80].TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "Katalog" : result;
    }

    private string MakeUniqueBaseName(string baseName, bool withJson, string extension = ".pdf")
    {
        var candidate = baseName;
        int n = 2;
        while (File.Exists(Path.Combine(LibraryPath, $"{candidate}{extension}")) ||
               (withJson && File.Exists(Path.Combine(LibraryPath, $"{candidate}.json"))))
        {
            candidate = $"{baseName} ({n++})";
        }
        return candidate;
    }

}
