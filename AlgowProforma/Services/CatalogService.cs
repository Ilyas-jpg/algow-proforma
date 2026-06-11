using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

public class CatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
    };

    public Catalog Load(string path)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex) { throw new InvalidDataException($"Katalog dosyası okunamadı: {Path.GetFileName(path)}", ex); }
        try
        {
            var catalog = JsonSerializer.Deserialize<Catalog>(json, JsonOptions);
            if (catalog is null)
                throw new InvalidDataException("Geçersiz katalog dosyası: içerik okunamadı.");
            return catalog;
        }
        catch (JsonException ex)
        {
            // Bozuk JSON ham JsonException yerine temiz hata — çağıran (ListEntries/cleanup) güvenle yakalar.
            throw new InvalidDataException($"Katalog dosyası bozuk: {Path.GetFileName(path)}", ex);
        }
    }

    public void Save(string path, Catalog catalog)
    {
        catalog.LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        AtomicFile.WriteAllText(path, json);   // atomik (R-1): autosave/katalog yazımı yarıda kalsa bozulmaz
    }
}
