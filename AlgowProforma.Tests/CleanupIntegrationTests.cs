using System;
using System.IO;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// CleanupOrphanImages uçtan-uca: gerçek dosya sistemi (geçici kök) üzerinde
/// kök-taşınma senaryosunda görselin KORUNDUĞUNU, gerçek öksüzün trash'e taşındığını doğrular.
/// ImageStorage.Directory artık AppPaths.LibraryRoot'tan türediği için izole çalışır.
/// </summary>
[Collection("AppPathsIsolation")]
public class CleanupIntegrationTests
{
    private static string PutImage(string guidName)
    {
        Directory.CreateDirectory(ImageStorage.Directory);
        var p = Path.Combine(ImageStorage.Directory, guidName);
        File.WriteAllBytes(p, new byte[] { 1, 2, 3 });
        return p;
    }

    [Fact]
    public void Cleanup_PreservesImage_WhenCatalogJsonHoldsOldRootPath()
    {
        using var tmp = new TempLibraryRoot();
        var guidName = Guid.NewGuid().ToString("N") + ".png";
        var physical = PutImage(guidName);

        // Katalog JSON'u, görseli ESKİ (artık var olmayan) kökün mutlak path'iyle referanslıyor —
        // OneDrive KFM / Belgeler taşınması senaryosu.
        var cat = new Catalog();
        cat.Products.Add(new Product { Name = "X", ImagePath = Path.Combine(@"C:\EskiKok\images", guidName) });
        new CatalogService().Save(Path.Combine(AppPaths.LibraryRoot, "katalog.json"), cat);

        var moved = new LibraryService().CleanupOrphanImages();

        Assert.Equal(0, moved);
        Assert.True(File.Exists(physical));   // eski davranışta trash'e gider, 14 günde silinirdi
    }

    [Fact]
    public void Cleanup_StillMovesTrueOrphan_ToTrash()
    {
        using var tmp = new TempLibraryRoot();
        var orphanName = Guid.NewGuid().ToString("N") + ".png";
        var physical = PutImage(orphanName);
        File.SetCreationTime(physical, DateTime.Now.AddMinutes(-10));  // gerçek orphan = eski (yeni-dosya yaş filtresini geçer)

        var moved = new LibraryService().CleanupOrphanImages();

        Assert.Equal(1, moved);
        Assert.False(File.Exists(physical));
        Assert.True(File.Exists(Path.Combine(ImageStorage.Directory, ".trash", orphanName)));
    }

    // L7: DL-2 koruma katmanları — kütüphane görselleri ve protectedPaths trash'e GİTMEMELİ
    // (geçmişte tam da bu yüzeyde veri kaybı vardı; refactor bunu sessizce kırmasın).

    [Fact]
    public void Cleanup_PreservesImage_ReferencedOnlyByProductLibrary()
    {
        using var tmp = new TempLibraryRoot();
        var guidName = Guid.NewGuid().ToString("N") + ".png";
        var physical = PutImage(guidName);

        // Hiçbir katalogda yok, yalnız kalıcı ürün kütüphanesinde referanslı (reference-counted).
        new ProductLibraryService().Save(new[] { new Product { Name = "K", ImagePath = physical } });

        var moved = new LibraryService().CleanupOrphanImages();

        Assert.Equal(0, moved);
        Assert.True(File.Exists(physical));
    }

    [Fact]
    public void Cleanup_PreservesImage_GivenInProtectedPaths()
    {
        using var tmp = new TempLibraryRoot();
        var guidName = Guid.NewGuid().ToString("N") + ".png";
        var physical = PutImage(guidName);

        // Açık çalışma oturumunun (henüz kaydedilmemiş) görselleri protectedPaths ile korunur.
        var moved = new LibraryService().CleanupOrphanImages(new[] { physical });

        Assert.Equal(0, moved);
        Assert.True(File.Exists(physical));
    }

    [Fact]
    public void Cleanup_SkipsRecentlyCreatedImage_NotYetSaved()
    {
        using var tmp = new TempLibraryRoot();
        var guidName = Guid.NewGuid().ToString("N") + ".png";
        var physical = PutImage(guidName);   // CreationTime = şimdi (yeni yüklendi, henüz kaydedilmedi)

        // M1: hiçbir yerde referanslı olmasa bile son 5 dk içinde oluşturulan görsel bu turda atlanır
        // (Excel materialize / kapak ekleme yarışı) → kalıcı kayıp önlenir.
        var moved = new LibraryService().CleanupOrphanImages();

        Assert.Equal(0, moved);
        Assert.True(File.Exists(physical));
    }
}
