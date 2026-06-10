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

    // Crash-recovery autosave — Belgeler/kurulum DIŞINDA, LocalAppData altında.
    // Tek kaynak: MainViewModel autosave'i buraya yazar, LibraryService.CleanupOrphanImages buradan
    // okuyup autosave görsellerini korur (DL-2). Path; dizini yazıcı oluşturur (eager create yok).
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlgowProforma");
    public static string AutoSaveFile => Path.Combine(AppDataDir, "autosave.json");

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
        catch { AtomicFile.BackupCorrupt(path); return new(); }   // L4: sessiz reset yerine bozuğu yedekle
    }

    public static void SaveList<T>(string path, IEnumerable<T> items) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(items, Options));

    public static T LoadOrNew<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new(); }
        catch { AtomicFile.BackupCorrupt(path); return new(); }   // L4: sessiz reset yerine bozuğu yedekle
    }

    public static void SaveObject<T>(string path, T obj) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(obj, Options));
}

/// <summary>
/// Atomik dosya yazımı. File.WriteAllText yarıda kesilirse dosya yarım/bozuk kalır → çökme/güç
/// kesintisinde customers/pricebook/katalog/autosave JSON'u kaybolur (R-1/L4). Çözüm: .tmp'ye yaz,
/// sonra File.Replace ile yerine koy — OS düzeyinde atomik, okuyucu ya tam eski ya tam yeni görür.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);   // .NET default = UTF-8 BOM'suz (mevcut davranışla birebir)
        if (File.Exists(path))
        {
            // File.Replace atomik. Hidden/kilit edge'inde fallback: Delete+Move (yine de tmp güvende).
            try { File.Replace(tmp, path, null); }
            catch (Exception) { File.Delete(path); File.Move(tmp, path); }
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    /// <summary>Bozuk dosyayı, üzerine yazmadan önce .corrupt kopyasına al (kalıcı kayıp önleme).
    /// Sabit ad (overwrite) → tekrarlı açılışta .corrupt birikmez.</summary>
    public static void BackupCorrupt(string path)
    {
        try { if (File.Exists(path)) File.Copy(path, path + ".corrupt", overwrite: true); }
        catch { }
    }
}
