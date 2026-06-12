using System;
using System.IO;
using System.Linq;
using AlgowProforma.Models;
using AlgowProforma.Services;
using ClosedXML.Excel;
using SkiaSharp;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// İnceleme #2'nin High sınıfını KALICI kapatan stres-fixture matrisi:
///   H① — Excel SAYI hücresi 3+ ondalıkta string round-trip + ayraç sezgisiyle 1000× şişiyordu,
///   H② — geniş wordmark logo (10:1) teklif header'ında DocumentLayoutException atıyordu,
///   H③ — 25+ satırlık ürün tablosu ShowEntire ile sayfaya sığmayıp DLE atıyor, küçük taşmada
///         statik footer numarası kayıyordu (artık satır bölme + CurrentPageNumber).
/// Bu testler fix'lerden ÖNCE kırmızıydı; render yolu değiştikçe sınıfın geri gelmesini engeller.
/// </summary>
public class StressRenderTests
{
    static StressRenderTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    // ---------- H① — Excel Number hücresi ----------

    [Fact]
    public void NumberCell_ThreeDecimals_ReadDirectly_NotInflated1000x()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alg-num-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Sayfa1");
                ws.Cell(1, 1).Value = "Kod";
                ws.Cell(1, 2).Value = "Ad";
                ws.Cell(1, 3).Value = "Fiyat";
                ws.Cell(2, 1).Value = "VLV-1";
                ws.Cell(2, 2).Value = "Vana";
                ws.Cell(2, 3).Value = 12.345;      // GERÇEK sayı hücresi — eski yol 12345 yapıyordu
                ws.Cell(3, 1).Value = "VLV-2";
                ws.Cell(3, 2).Value = "Vana büyük";
                ws.Cell(3, 3).Value = "12.500";    // METİN hücresi — TR binlik, 12500 kalmalı
                wb.SaveAs(path);
            }

            var rows = ExcelImportService.Import(path, new ExcelImportOptions
            {
                HasHeaderRow = true, CodeColumn = 1, NameColumn = 2, PriceColumn = 3,
            });

            Assert.Equal(2, rows.Count);
            Assert.Equal(12.345m, rows[0].Price);     // sayı hücresi: tip bilgisi kazanır
            Assert.Equal(12500m, rows[1].Price);      // metin hücresi: ayraç sezgisi kazanır
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void PriceBookImport_NumberCell_NotInflated()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alg-pb-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Fiyat");
                ws.Cell(1, 1).Value = "Kod";
                ws.Cell(1, 2).Value = "Ad";
                ws.Cell(1, 3).Value = "Birim Fiyat";
                ws.Cell(2, 1).Value = "P1";
                ws.Cell(2, 2).Value = "Ürün";
                ws.Cell(2, 3).Value = 7.125;          // 3 ondalıklı gerçek sayı
                wb.SaveAs(path);
            }

            var items = ExcelDataService.ImportPriceItems(path);
            Assert.Single(items);
            Assert.Equal(7.125m, items[0].UnitPrice);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExcelImport_CategoryColumn_FlowsIntoProduct()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alg-cat-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Sayfa1");
                ws.Cell(1, 1).Value = "Kod"; ws.Cell(1, 2).Value = "Ad"; ws.Cell(1, 3).Value = "Kategori";
                ws.Cell(2, 1).Value = "V-1"; ws.Cell(2, 2).Value = "Vana"; ws.Cell(2, 3).Value = "Küresel Vanalar";
                wb.SaveAs(path);
            }

            var rows = ExcelImportService.Import(path, new ExcelImportOptions
            {
                HasHeaderRow = true, CodeColumn = 1, NameColumn = 2, CategoryColumn = 3,
            });

            Assert.Single(rows);
            Assert.Equal("Küresel Vanalar", rows[0].Category);
            Assert.Equal("Küresel Vanalar", ExcelImportService.ToProduct(rows[0]).Category);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // ---------- H② — geniş logo ----------

    [Fact]
    public void QuotePdf_WideWordmarkLogo_10to1_Renders()
    {
        var logo = CreatePng(600, 60);   // 10:1 wordmark — FitHeight döneminde DLE adayı
        var pdf = Path.Combine(Path.GetTempPath(), $"alg-widelogo-{Guid.NewGuid():N}.pdf");
        try
        {
            var brand = new BrandInfo { Name = "Geniş Marka", LogoPath = logo };
            QuotePdfService.Generate(SampleQuote(3), brand, pdf);
            AssertValidPdf(pdf);
        }
        finally { TryDelete(pdf); TryDelete(logo); }
    }

    [Fact]
    public void QuotePdf_ExtremeWideLogo_20to1_Renders()
    {
        var logo = CreatePng(1200, 60);
        var pdf = Path.Combine(Path.GetTempPath(), $"alg-xwidelogo-{Guid.NewGuid():N}.pdf");
        try
        {
            QuotePdfService.Generate(SampleQuote(2), new BrandInfo { Name = "M", LogoPath = logo }, pdf);
            AssertValidPdf(pdf);
        }
        finally { TryDelete(pdf); TryDelete(logo); }
    }

    // ---------- H③ — sınırsız ürün tablosu ----------

    [Fact]
    public void Catalog_TableProduct40Rows_AutoFlow_SplitsAndRenders()
    {
        var catalog = Catalog.CreateDefault();
        catalog.Products.Add(TableProduct(40));               // tek sayfaya imkânsız
        catalog.Products.Add(new Product { Name = "Standart Ürün", Price = 100m });

        var pdf = Path.Combine(Path.GetTempPath(), $"alg-table40-{Guid.NewGuid():N}.pdf");
        try
        {
            new PdfService().Generate(catalog, pdf);          // fix öncesi: DocumentLayoutException
            AssertValidPdf(pdf);
        }
        finally { TryDelete(pdf); }
    }

    [Fact]
    public void Catalog_TableProduct40Rows_ManualMode_ScalesAndRenders()
    {
        var catalog = Catalog.CreateDefault();
        var table = TableProduct(40);
        catalog.Products.Add(table);
        catalog.UseCustomPageLayouts = true;
        var page = new CustomPageEntry("3x3");
        page.ProductIds.Add(table.Id);
        catalog.CustomPages.Add(page);

        var pdf = Path.Combine(Path.GetTempPath(), $"alg-table40m-{Guid.NewGuid():N}.pdf");
        try
        {
            new PdfService().Generate(catalog, pdf);          // sabit hücrede ScaleToFit güvenliği
            AssertValidPdf(pdf);
        }
        finally { TryDelete(pdf); }
    }

    [Fact]
    public void Quote_35Lines_MultiPage_Renders()
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"alg-q35-{Guid.NewGuid():N}.pdf");
        try
        {
            QuotePdfService.Generate(SampleQuote(35), new BrandInfo { Name = "Çok Kalemli A.Ş." }, pdf);
            AssertValidPdf(pdf);
        }
        finally { TryDelete(pdf); }
    }

    // ---------- kategori gruplaması — Türkçe İ katlama ----------

    [Fact]
    public void BuildCategoryGroups_TurkishDottedI_FoldsToSingleGroup()
    {
        var products = new[]
        {
            new Product { Name = "A", Category = "İthal" },
            new Product { Name = "B", Category = "ithal" },   // OrdinalIgnoreCase bunu AYRI sayıyordu
            new Product { Name = "C", Category = "IŞIK" },
            new Product { Name = "D", Category = "ışık" },
        };

        var groups = PdfService.BuildCategoryGroups(products);

        Assert.Equal(2, groups.Count);                        // İthal + IŞIK — 4 değil 2 bölüm
        Assert.Equal("İthal", groups[0].Category);            // görünen ad = ilk görülen yazım
        Assert.Equal(2, groups[0].Products.Count);
        Assert.Equal(2, groups[1].Products.Count);
    }

    // ---------- yardımcılar ----------

    private static Quote SampleQuote(int lineCount)
    {
        var q = new Quote { QuoteNo = "STR-2026-0001", CustomerCompany = "Stres Test Ltd." };
        for (int i = 1; i <= lineCount; i++)
            q.Lines.Add(new QuoteLine
            {
                Name = $"Kalem {i} — orta uzunlukta açıklamalı ürün adı",
                Quantity = i, UnitPrice = 10m + i, VatRate = 20m,
            });
        return q;
    }

    private static Product TableProduct(int rows)
    {
        var t = new ProductTable { Title = "Ölçü Tablosu" };
        t.Columns.Add(new ProductTableColumn { Header = "DN" });
        t.Columns.Add(new ProductTableColumn { Header = "PN" });
        t.Columns.Add(new ProductTableColumn { Header = "Ağırlık" });
        for (int i = 0; i < rows; i++)
        {
            var r = new ProductTableRow();
            r.Cells.Add(new ProductTableCell { Value = $"DN{15 + i * 5}" });
            r.Cells.Add(new ProductTableCell { Value = "PN16" });
            r.Cells.Add(new ProductTableCell { Value = $"{1.2 + i * 0.3:0.0} kg" });
            t.Rows.Add(r);
        }
        return new Product { Name = "Sınırsız Tablo Ürünü", Code = "TBL-40", HasTable = true, Table = t };
    }

    /// <summary>QuestPDF'in kendi decode zinciriyle (SkiaSharp) gerçek PNG üretir — sahte baytlar olmaz.</summary>
    private static string CreatePng(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Navy);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        var path = Path.Combine(Path.GetTempPath(), $"alg-logo-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void AssertValidPdf(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 1000, "PDF boş/çok küçük");
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
