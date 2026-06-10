using System.Collections.Generic;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;
using Xunit;

namespace AyTeknikKatalog.Tests;

/// <summary>
/// Görsel ürün kütüphanesi (Faz 2) dedup/upsert mantığı. Save/Load gerçek MyDocuments path'ine
/// yazdığından burada I/O test edilmez — saf mantık (UpsertAll + ToCatalogProduct) doğrulanır.
/// </summary>
public class ProductLibraryServiceTests
{
    [Fact]
    public void UpsertAll_DistinctCodes_AllAdded()
    {
        var lib = new List<Product>();
        var added = ProductLibraryService.UpsertAll(lib, new[]
        {
            new Product { Code = "A1", Name = "Vana", ImagePath = "img/a.png" },
            new Product { Code = "A2", Name = "Rakor" },
        });
        Assert.Equal(2, added);
        Assert.Equal(2, lib.Count);
    }

    [Fact]
    public void UpsertAll_SameCode_UpdatesNotDuplicates()
    {
        var lib = new List<Product> { new Product { Code = "A1", Name = "Eski", Price = 100m } };
        var added = ProductLibraryService.UpsertAll(lib, new[]
        {
            new Product { Code = "A1", Name = "Yeni", Price = 150m, ImagePath = "img/new.png" },
        });
        Assert.Equal(0, added);          // aynı kod → güncellendi, eklenmedi
        Assert.Single(lib);
        Assert.Equal("Yeni", lib[0].Name);
        Assert.Equal(150m, lib[0].Price);
        Assert.Equal("img/new.png", lib[0].ImagePath);
    }

    [Fact]
    public void UpsertAll_NoCode_DedupsByName()
    {
        var lib = new List<Product>();
        ProductLibraryService.UpsertAll(lib, new[] { new Product { Name = "Conta", Price = 5m } });
        var added = ProductLibraryService.UpsertAll(lib, new[] { new Product { Name = "Conta", Price = 7m } });
        Assert.Equal(0, added);
        Assert.Single(lib);
        Assert.Equal(7m, lib[0].Price);
    }

    [Fact]
    public void ToCatalogProduct_FreshId_PreservesFields()
    {
        var libItem = new Product { Id = "lib-id", Code = "A1", Name = "Vana", Price = 100m, ImagePath = "img/a.png" };
        var p = ProductLibraryService.ToCatalogProduct(libItem);
        Assert.NotEqual("lib-id", p.Id);  // bağımsız yeni Id
        Assert.Equal("A1", p.Code);
        Assert.Equal("Vana", p.Name);
        Assert.Equal(100m, p.Price);
        Assert.Equal("img/a.png", p.ImagePath);
    }
}
