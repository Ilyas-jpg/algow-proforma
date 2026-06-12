using System;
using System.Collections.Generic;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// DL-1 fix çekirdeği. CleanupOrphanImages yalnız referansta OLMAYAN görselleri trash'e taşır;
/// dolayısıyla referansların DOĞRU toplanması veri-kaybını önleyen şeydir. Eski bug'da kayıtlı
/// kataloglardan HİÇBİR referans toplanmıyordu (backslash kaçış hatası) → görseller siliniyordu.
/// </summary>
public class ImageCleanupTests
{
    [Fact]
    public void CollectImagePaths_GathersAllReferenceTypes_AndIgnoresNonImages()
    {
        var cat = new Catalog { Brand = new BrandInfo { LogoPath = "L" } };
        cat.Cover.CustomCoverImagePath = "C";
        cat.Cover.Elements.Add(new CoverElement { Type = CoverElementType.Image, Content = "E1" });
        cat.Cover.Elements.Add(new CoverElement { Type = CoverElementType.Title, Content = "T" }); // metin → toplanmamalı
        cat.Products.Add(new Product { ImagePath = "P1" });
        cat.Products.Add(new Product { ImagePath = "P2" });
        cat.References.Add(new Reference { LogoPath = "R1" });

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LibraryService.CollectImagePaths(cat, set);

        Assert.Contains("L", set);   // marka logo
        Assert.Contains("C", set);   // özel kapak görseli
        Assert.Contains("E1", set);  // kapak Image elementi
        Assert.Contains("P1", set);  // ürün görselleri
        Assert.Contains("P2", set);
        Assert.Contains("R1", set);  // referans logosu
        Assert.DoesNotContain("T", set); // Title elementi görsel değil
        Assert.Equal(6, set.Count);
    }

    [Fact]
    public void CollectImagePaths_SkipsEmptyAndWhitespacePaths()
    {
        var cat = new Catalog { Brand = new BrandInfo { LogoPath = "" } };
        cat.Cover.CustomCoverImagePath = "   ";
        cat.Products.Add(new Product { ImagePath = "" });
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LibraryService.CollectImagePaths(cat, set);
        Assert.Empty(set);
    }

    // ── Kök-taşınma dayanıklılığı: JSON'daki mutlak path eski kökü gösterse bile GUID dosya-adı
    //    eşleşmesi görseli korur. Eski davranışta OneDrive KFM sonrası TÜM görseller trash'e giderdi.

    [Fact]
    public void IsReferenced_MatchesByGuidFileName_WhenDocumentsRootMoved()
    {
        var oldRef    = @"C:\Users\u\Documents\Algow Proforma Kataloglar\images\3f2a9c1d4b5e6f708192a3b4c5d6e7f8.png";
        var movedFile = @"C:\Users\u\OneDrive\Belgeler\Algow Proforma Kataloglar\images\3f2a9c1d4b5e6f708192a3b4c5d6e7f8.png";
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { oldRef };
        var names = LibraryService.BuildFileNameSet(paths);

        Assert.DoesNotContain(movedFile, paths);                       // tam-path eşleşmesi boşa düşüyor…
        Assert.True(LibraryService.IsReferenced(movedFile, paths, names)); // …ad eşleşmesi kurtarıyor
    }

    [Fact]
    public void IsReferenced_StillFlagsTrueOrphans()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\root\images\aaaa1111.png" };
        var names = LibraryService.BuildFileNameSet(paths);

        Assert.False(LibraryService.IsReferenced(@"C:\root\images\bbbb2222.png", paths, names));
    }

    [Fact]
    public void BuildFileNameSet_IsCaseInsensitive_AndSkipsBlank()
    {
        var names = LibraryService.BuildFileNameSet(new[] { @"C:\x\ABC.PNG", "", "   " });
        Assert.Single(names);
        Assert.Contains("abc.png", names);
    }
}
