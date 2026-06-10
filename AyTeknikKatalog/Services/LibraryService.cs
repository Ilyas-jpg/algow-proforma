using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

public class LibraryService
{
    private readonly CatalogService _catalogService = new();
    private readonly PdfService _pdfService = new();

    public string LibraryPath { get; }

    public LibraryService()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        LibraryPath = Path.Combine(documents, "Algow Proforma Kataloglar");
        Directory.CreateDirectory(LibraryPath);
    }

    /// <summary>
    /// images/ klasöründeki dosyalardan, hiçbir katalog JSON'unda referansı olmayanları siler.
    /// Kütüphane açılırken bir kez çağrılır. Disk şişmesini önler.
    /// </summary>
    public int CleanupOrphanImages()
    {
        if (!Directory.Exists(LibraryPath)) return 0;
        var imagesDir = ImageStorage.Directory;
        if (!Directory.Exists(imagesDir)) return 0;

        // 1. Tüm katalog JSON'larını oku, referans verilen path'leri topla
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var jsonPath in Directory.GetFiles(LibraryPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                // basit string search — performant + güvenli (JSON parse exception'a düşmez)
                var idx = 0;
                while ((idx = json.IndexOf(imagesDir, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var end = json.IndexOfAny(new[] { '"', '\\' }, idx + imagesDir.Length + 1);
                    if (end < 0) break;
                    var path = json.Substring(idx, end - idx).Replace("\\\\", "\\");
                    referencedPaths.Add(path);
                    idx = end;
                }
            }
            catch { /* bozuk json - geç */ }
        }

        // 2. images/ klasöründekileri tara, referans olmayanları sil
        int deleted = 0;
        foreach (var file in Directory.GetFiles(imagesDir))
        {
            if (!referencedPaths.Contains(file))
            {
                try { File.Delete(file); deleted++; }
                catch { /* read-only veya kilitli — atla */ }
            }
        }
        return deleted;
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
        _pdfService.Generate(catalog, pdfPath);
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
        _pdfService.GenerateSingleProduct(catalog, product, pngPath);
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
        if (File.Exists(entry.PdfPath)) File.Delete(entry.PdfPath);
        if (File.Exists(entry.JsonPath))
        {
            try { File.SetAttributes(entry.JsonPath, FileAttributes.Normal); } catch { }
            File.Delete(entry.JsonPath);
        }
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
