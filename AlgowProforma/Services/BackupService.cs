using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AlgowProforma.Services;

/// <summary>
/// Tek-tık veri yedeği: LibraryRoot'un tamamı (kataloglar + data + images + Teklifler) tek ZIP.
/// Geri yükleme YIKICI DEĞİL — mevcut kök önce "root.pre-restore-{zaman}" kopyasına taşınır,
/// açılım yarıda kalırsa otomatik geri konur. .trash içerikleri yedeğe girmez.
/// </summary>
public static class BackupService
{
    /// <summary>Yedek alır; (eklenen, atlanan) dosya sayısı döner. Kilitli dosya yedeği durdurmaz —
    /// atlanır ve sayılır ("elden geldiğince tam" yedek, hiç alınamamıştan iyidir).</summary>
    public static (int Added, int Skipped) CreateBackup(string zipPath)
    {
        var root = AppPaths.LibraryRoot;
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Veri klasörü bulunamadı: " + root);
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("Yedek dosya yolu boş.", nameof(zipPath));

        // Ayraçlı karşılaştırma: çıplak StartsWith kardeş klasörü de ("...Kataloglar-Yedek\x.zip")
        // kök içi sanıp reddediyordu. Kök + '\' öneki yalnız GERÇEK alt yolları yakalar.
        var fullZip = Path.GetFullPath(zipPath);
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (fullZip.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullZip, rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yedek, veri klasörünün İÇİNE alınamaz — başka bir konum seçin.");

        if (File.Exists(fullZip)) File.Delete(fullZip);

        int added = 0, skipped = 0;
        using var zip = ZipFile.Open(fullZip, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file);
            if (IsTrash(rel)) continue;   // çöp yedeğe taşınmaz
            try
            {
                zip.CreateEntryFromFile(file, rel.Replace('\\', '/'), CompressionLevel.Optimal);
                added++;
            }
            catch { skipped++; }   // kilitli/erişilemeyen dosya — yedeğin kalanı yaşar
        }
        return (added, skipped);
    }

    /// <summary>Yedeği geri yükler; mevcut kökün taşındığı güvenlik kopyası yolunu döner
    /// (boş = kök zaten yoktu). Açılım başarısız olursa güvenlik kopyası geri konur ve fırlatılır.</summary>
    public static string RestoreBackup(string zipPath)
    {
        ValidateBackupZip(zipPath);

        var root = AppPaths.LibraryRoot;
        string safety = "";
        if (Directory.Exists(root))
        {
            safety = root + ".pre-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.Move(root, safety);
        }

        try
        {
            // .NET 8 ExtractToDirectory zip-slip'e (kökten kaçan path) karşı korumalı.
            ZipFile.ExtractToDirectory(zipPath, root);
        }
        catch
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
            if (safety.Length > 0 && !Directory.Exists(root))
                try { Directory.Move(safety, root); } catch { }
            throw;
        }
        return safety;
    }

    /// <summary>Yanlış ZIP'i erken keser: gerçek yedekte data/ klasörü (settings/pricebook...) bulunur.</summary>
    internal static void ValidateBackupZip(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var ok = zip.Entries.Any(e =>
            e.FullName.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.StartsWith("data\\", StringComparison.OrdinalIgnoreCase));
        if (!ok)
            throw new InvalidDataException(
                "Bu dosya bir Algow Proforma yedeği gibi görünmüyor (içinde data/ klasörü yok).");
    }

    private static bool IsTrash(string relativePath)
    {
        var norm = relativePath.Replace('/', '\\');
        return norm.StartsWith(".trash\\", StringComparison.OrdinalIgnoreCase)
            || norm.Contains("\\.trash\\", StringComparison.OrdinalIgnoreCase);
    }
}
