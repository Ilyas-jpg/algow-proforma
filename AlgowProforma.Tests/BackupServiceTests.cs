using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// ZIP yedek/geri-yükle uçtan-uca (geçici kök): yedek .trash'i dışlar, geri yükleme mevcut veriyi
/// pre-restore güvenlik kopyasına taşıyıp içeriği birebir geri getirir, yanlış zip erken reddedilir.
/// </summary>
[Collection("AppPathsIsolation")]
public class BackupServiceTests
{
    private static void Seed()
    {
        Directory.CreateDirectory(Path.Combine(AppPaths.LibraryRoot, "data"));
        Directory.CreateDirectory(Path.Combine(AppPaths.LibraryRoot, "Teklifler"));
        Directory.CreateDirectory(Path.Combine(AppPaths.LibraryRoot, "images", ".trash"));
        File.WriteAllText(Path.Combine(AppPaths.LibraryRoot, "data", "settings.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(AppPaths.LibraryRoot, "Teklifler", "t1.json"), "{\"QuoteNo\":\"2026-0001\"}");
        File.WriteAllBytes(Path.Combine(AppPaths.LibraryRoot, "images", "g1.png"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(AppPaths.LibraryRoot, "images", ".trash", "cop.png"), new byte[] { 9 });
    }

    [Fact]
    public void CreateBackup_IncludesData_ExcludesTrash()
    {
        using var tmp = new TempLibraryRoot();
        Seed();
        var zipPath = Path.Combine(Path.GetTempPath(), "alg-bak-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            var (added, skipped) = BackupService.CreateBackup(zipPath);

            Assert.Equal(3, added);     // settings + teklif + görsel (.trash'teki HARİÇ)
            Assert.Equal(0, skipped);
            using var zip = ZipFile.OpenRead(zipPath);
            Assert.Contains(zip.Entries, e => e.FullName == "data/settings.json");
            Assert.Contains(zip.Entries, e => e.FullName == "Teklifler/t1.json");
            Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains(".trash"));
        }
        finally { if (File.Exists(zipPath)) File.Delete(zipPath); }
    }

    [Fact]
    public void RestoreBackup_RoundTrip_MovesCurrentToSafetyCopy()
    {
        using var tmp = new TempLibraryRoot();
        Seed();
        var zipPath = Path.Combine(Path.GetTempPath(), "alg-bak-" + Guid.NewGuid().ToString("N") + ".zip");
        string safety = "";
        try
        {
            BackupService.CreateBackup(zipPath);

            // Yedek sonrası veri "bozulsun" — geri yükleme eski hâli getirmeli
            File.WriteAllText(Path.Combine(AppPaths.LibraryRoot, "data", "settings.json"), "{\"BOZUK\":true}");

            safety = BackupService.RestoreBackup(zipPath);

            Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(AppPaths.LibraryRoot, "data", "settings.json")));
            Assert.True(File.Exists(Path.Combine(AppPaths.LibraryRoot, "Teklifler", "t1.json")));
            // Mevcut veri silinmedi — güvenlik kopyasında "bozuk" hali duruyor
            Assert.False(string.IsNullOrEmpty(safety));
            Assert.Contains("BOZUK", File.ReadAllText(Path.Combine(safety, "data", "settings.json")));
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            try { if (safety.Length > 0 && Directory.Exists(safety)) Directory.Delete(safety, true); } catch { }
        }
    }

    [Fact]
    public void RestoreBackup_RejectsForeignZip()
    {
        using var tmp = new TempLibraryRoot();
        Seed();
        var zipPath = Path.Combine(Path.GetTempPath(), "alg-yabanci-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntry("rastgele.txt");

            Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(zipPath));
            // Reddedilen geri yükleme mevcut veriye DOKUNMAZ
            Assert.True(File.Exists(Path.Combine(AppPaths.LibraryRoot, "data", "settings.json")));
        }
        finally { if (File.Exists(zipPath)) File.Delete(zipPath); }
    }
}
