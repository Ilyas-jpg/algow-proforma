using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

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
        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<Catalog>(json, JsonOptions);
        if (catalog is null)
            throw new InvalidDataException("Geçersiz katalog dosyası: içerik okunamadı.");
        return catalog;
    }

    public void Save(string path, Catalog catalog)
    {
        catalog.LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        File.WriteAllText(path, json);
    }
}
