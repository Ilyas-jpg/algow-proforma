using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AyTeknikKatalog.Services;

/// <summary>Uygulama veri yolları — tek merkez. Hepsi MyDocuments\Algow Proforma Kataloglar altında.</summary>
public static class AppPaths
{
    public static string LibraryRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Algow Proforma Kataloglar");

    public static string DataDir => EnsureDir(Path.Combine(LibraryRoot, "data"));
    public static string QuotesDir => EnsureDir(Path.Combine(LibraryRoot, "Teklifler"));

    public static string PriceBookFile => Path.Combine(DataDir, "pricebook.json");
    public static string CustomersFile => Path.Combine(DataDir, "customers.json");
    public static string CountersFile => Path.Combine(DataDir, "counters.json");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string EmailLogsFile => Path.Combine(DataDir, "email-send-log.json");

    private static string EnsureDir(string p) { Directory.CreateDirectory(p); return p; }
}

/// <summary>
/// Ortak JSON depolama yardımcısı. CatalogService ile aynı encoder ayarı —
/// JavaScriptEncoder.Create(UnicodeRanges.All) Türkçe karakteri JSON'a düz UTF-8 yazar
/// (\u kaçışı yok), File.WriteAllText UTF-8 (BOM'suz) yazar → encoding tuzağı yok.
/// </summary>
public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
    };

    public static List<T> LoadList<T>(string path)
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options) ?? new(); }
        catch { return new(); }
    }

    public static void SaveList<T>(string path, IEnumerable<T> items) =>
        File.WriteAllText(path, JsonSerializer.Serialize(items, Options));

    public static T LoadOrNew<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new(); }
        catch { return new(); }
    }

    public static void SaveObject<T>(string path, T obj) =>
        File.WriteAllText(path, JsonSerializer.Serialize(obj, Options));
}
