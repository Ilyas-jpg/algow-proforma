using System;
using System.IO;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;
using Xunit;

namespace AyTeknikKatalog.Tests;

/// <summary>
/// Katalog kalıcılığı: tam round-trip (Türkçe + görsel yolları korunur), bozuk JSON temiz hata verir
/// (ham JsonException değil), atomik Save geçici dosya bırakmaz.
/// </summary>
public class CatalogServiceTests
{
    private static string TempJson() => Path.Combine(Path.GetTempPath(), $"alg-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void RoundTrip_PreservesData_IncludingTurkishAndImagePaths()
    {
        var svc = new CatalogService();
        var cat = new Catalog
        {
            Brand = new BrandInfo { Name = "Çörek Şişe Ünlü A.Ş.", LogoPath = @"C:\img\logo.png" },
        };
        cat.Products.Add(new Product { Name = "Vana DN25", ImagePath = @"C:\img\p1.png", Price = 1850m });
        cat.References.Add(new Reference { Name = "İş Ortağı", LogoPath = @"C:\img\ref.png" });
        cat.Cover.CustomCoverImagePath = @"C:\img\cover.png";
        cat.Cover.Elements.Add(new CoverElement { Type = CoverElementType.Image, Content = @"C:\img\el.png" });

        var path = TempJson();
        try
        {
            svc.Save(path, cat);
            var loaded = svc.Load(path);

            Assert.Equal("Çörek Şişe Ünlü A.Ş.", loaded.Brand.Name);
            Assert.Equal(@"C:\img\logo.png", loaded.Brand.LogoPath);
            Assert.Single(loaded.Products);
            Assert.Equal("Vana DN25", loaded.Products[0].Name);
            Assert.Equal(@"C:\img\p1.png", loaded.Products[0].ImagePath);
            Assert.Equal(1850m, loaded.Products[0].Price);
            Assert.Single(loaded.References);
            Assert.Equal(@"C:\img\cover.png", loaded.Cover.CustomCoverImagePath);
            Assert.Single(loaded.Cover.Elements);
            Assert.Equal(@"C:\img\el.png", loaded.Cover.Elements[0].Content);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_CorruptJson_ThrowsInvalidData_NotRawJsonException()
    {
        var svc = new CatalogService();
        var path = TempJson();
        File.WriteAllText(path, "{ bu gecerli json degil ");
        try { Assert.Throws<InvalidDataException>(() => svc.Load(path)); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Save_IsAtomic_Overwrites_NoTmpLeftBehind()
    {
        var svc = new CatalogService();
        var path = TempJson();
        try
        {
            svc.Save(path, new Catalog { Brand = new BrandInfo { Name = "A" } });
            svc.Save(path, new Catalog { Brand = new BrandInfo { Name = "B" } });
            Assert.Equal("B", svc.Load(path).Brand.Name);
            Assert.False(File.Exists(path + ".tmp")); // geçici dosya temizlendi
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
