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
}
