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

        var moved = new LibraryService().CleanupOrphanImages();

        Assert.Equal(1, moved);
        Assert.False(File.Exists(physical));
        Assert.True(File.Exists(Path.Combine(ImageStorage.Directory, ".trash", orphanName)));
    }
}
