using System;
using System.IO;
using System.Linq;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Kategori başlık sayfaları: gruplama kuralları (kategorisizler önde başlıksız, kategoriler ilk
/// görülme sırasıyla, ad eşleşmesi harf duyarsız) + tam PDF render smoke (toggle açık/kapalı).
/// Toggle KAPALIYKEN eski yol birebir korunur — regresyon sınırı budur.
/// </summary>
public class CategoryPagesTests
{
    static CategoryPagesTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static Product P(string name, string category = "") =>
        new() { Name = name, Code = name.ToUpperInvariant(), Price = 100m, Currency = "TL", Category = category };

    [Fact]
    public void BuildCategoryGroups_UncategorizedFirst_ThenFirstSeenOrder()
    {
        var groups = PdfService.BuildCategoryGroups(new[]
        {
            P("a", "Vanalar"),
            P("b"),                 // kategorisiz
            P("c", "Rakorlar"),
            P("d", "vanalar"),      // harf farkı — aynı grup, görünen ad ilk yazım
            P("e"),
        });

        Assert.Equal(3, groups.Count);
        Assert.Equal("", groups[0].Category);                       // kategorisizler ÖNDE
        Assert.Equal(new[] { "b", "e" }, groups[0].Products.Select(p => p.Name));
        Assert.Equal("Vanalar", groups[1].Category);                // ilk görülme sırası + ilk yazım
        Assert.Equal(new[] { "a", "d" }, groups[1].Products.Select(p => p.Name));
        Assert.Equal("Rakorlar", groups[2].Category);
    }

    [Fact]
    public void BuildCategoryGroups_AllUncategorized_SingleHeaderlessGroup()
    {
        var groups = PdfService.BuildCategoryGroups(new[] { P("a"), P("b") });
        var g = Assert.Single(groups);
        Assert.Equal("", g.Category);
        Assert.Equal(2, g.Products.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]   // toggle kapalı = eski yol — birebir çalışmaya devam etmeli
    public void Generate_WithCategories_ProducesValidPdf(bool useCategoryPages)
    {
        var catalog = Catalog.CreateDefault();
        catalog.Brand.Name = "Kategori Testi";
        catalog.UseCategoryPages = useCategoryPages;
        catalog.LayoutId = "2x2";
        catalog.Products.Add(P("Küresel Vana DN25", "Küresel Vanalar"));
        catalog.Products.Add(P("Küresel Vana DN50", "Küresel Vanalar"));
        catalog.Products.Add(P("Çek Valf 1/2"));                    // kategorisiz — önde, başlıksız
        catalog.Products.Add(P("Rakor 3/4", "Rakor Grubu"));

        var path = Path.Combine(Path.GetTempPath(), $"alg-kat-{Guid.NewGuid():N}.pdf");
        try
        {
            new PdfService().Generate(catalog, path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000, $"useCategoryPages={useCategoryPages}: PDF boş/çok küçük");
            Assert.Equal((byte)'%', bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'D', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Generate_CategoriesWithToggleOn_LargerThanWithout()
    {
        // Bölüm başlık sayfaları gerçekten ekleniyor mu? Aynı katalog, toggle açıkken
        // 2 ek sayfa (2 kategori) içerir → PDF kapalı hâlinden anlamlı büyük olmalı.
        Catalog Build(bool toggle)
        {
            var c = Catalog.CreateDefault();
            c.Brand.Name = "Boyut Testi";
            c.UseCategoryPages = toggle;
            c.LayoutId = "2x2";
            c.Products.Add(P("v1", "Vanalar"));
            c.Products.Add(P("r1", "Rakorlar"));
            return c;
        }

        var pOn = Path.Combine(Path.GetTempPath(), $"alg-kat-on-{Guid.NewGuid():N}.pdf");
        var pOff = Path.Combine(Path.GetTempPath(), $"alg-kat-off-{Guid.NewGuid():N}.pdf");
        try
        {
            new PdfService().Generate(Build(true), pOn);
            new PdfService().Generate(Build(false), pOff);
            Assert.True(new FileInfo(pOn).Length > new FileInfo(pOff).Length,
                "Toggle açıkken bölüm sayfaları eklenmedi (boyut artmadı)");
        }
        finally
        {
            if (File.Exists(pOn)) File.Delete(pOn);
            if (File.Exists(pOff)) File.Delete(pOff);
        }
    }

    [Fact]
    public void ProductClone_And_LibraryUpsert_CarryCategory()
    {
        var src = P("Vana", "Küresel Vanalar");
        Assert.Equal("Küresel Vanalar", src.Clone().Category);

        var library = new System.Collections.Generic.List<Product>();
        ProductLibraryService.UpsertAll(library, new[] { src });
        Assert.Equal("Küresel Vanalar", library.Single().Category);

        // Güncelleme dalı da taşımalı (mevcut kayıt üstüne yeni kategori)
        var updated = P("Vana", "Endüstriyel");
        ProductLibraryService.UpsertAll(library, new[] { updated });
        Assert.Equal("Endüstriyel", library.Single().Category);
    }
}
