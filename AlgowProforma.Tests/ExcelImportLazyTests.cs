using System;
using System.IO;
using AlgowProforma.Services;
using ClosedXML.Excel;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Lazy görsel extract: Import (önizleme) disk'e dokunmaz — gömülü görsel bellekte taşınır,
/// yazım ANCAK ToProduct (gerçek import) anında olur. Eski davranış her combo değişiminde
/// tüm görselleri yeni GUID'lerle diske basıyordu (orphan birikimi + UI donması).
/// </summary>
[Collection("AppPathsIsolation")]
public class ExcelImportLazyTests
{
    // 1×1 saydam PNG — ClosedXML'in gömülü görsel olarak kabul ettiği geçerli en küçük içerik.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static string CreateXlsxWithEmbeddedImage(string dir)
    {
        Directory.CreateDirectory(dir);   // fixture kökü lazy — ilk yazan oluşturur
        var path = Path.Combine(dir, "urunler.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sayfa1");
        ws.Cell(1, 1).Value = "Kod";
        ws.Cell(1, 2).Value = "Ürün Adı";
        ws.Cell(1, 3).Value = "Fiyat";
        ws.Cell(2, 1).Value = "PRD-1";
        ws.Cell(2, 2).Value = "Vana";
        ws.Cell(2, 3).Value = "12.500";
        using var ms = new MemoryStream(TinyPng);
        ws.AddPicture(ms).MoveTo(ws.Cell(2, 4));
        wb.SaveAs(path);
        return path;
    }

    private static ExcelImportOptions Opts() => new()
    {
        SheetName = "Sayfa1",
        HasHeaderRow = true,
        CodeColumn = 1,
        NameColumn = 2,
        PriceColumn = 3,
        ImageColumn = 0,   // otomatik (gömülü)
    };

    [Fact]
    public void Import_DoesNotTouchDisk_ImageStaysInMemory()
    {
        using var tmp = new TempLibraryRoot();
        var xlsx = CreateXlsxWithEmbeddedImage(tmp.Root);

        var rows = ExcelImportService.Import(xlsx, Opts());

        var row = Assert.Single(rows);
        Assert.True(row.HasImage);                       // önizleme "görsel hazır" sayar…
        Assert.Null(row.ImagePath);                      // …ama diske hiçbir şey inmedi
        Assert.NotNull(row.PendingImageBytes);
        Assert.False(Directory.Exists(ImageStorage.Directory) &&
                     Directory.GetFiles(ImageStorage.Directory).Length > 0);
    }

    [Fact]
    public void ToProduct_MaterializesImageOnce_UnderImageStorage()
    {
        using var tmp = new TempLibraryRoot();
        var xlsx = CreateXlsxWithEmbeddedImage(tmp.Root);
        var row = Assert.Single(ExcelImportService.Import(xlsx, Opts()));

        var p1 = ExcelImportService.ToProduct(row);
        var p2 = ExcelImportService.ToProduct(row);      // çift çağrı = çift dosya OLMAMALI

        Assert.False(string.IsNullOrEmpty(p1.ImagePath));
        Assert.True(File.Exists(p1.ImagePath));
        Assert.StartsWith(ImageStorage.Directory, p1.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(p1.ImagePath, p2.ImagePath);
        Assert.Single(Directory.GetFiles(ImageStorage.Directory));
        Assert.Equal(12500m, p1.Price);                  // TR "12.500" = onikibin beşyüz (H1 regresyonu)
    }

    [Fact]
    public void Import_FilePathColumn_DefersCopyUntilToProduct()
    {
        using var tmp = new TempLibraryRoot();
        Directory.CreateDirectory(tmp.Root);   // fixture kökü lazy — ilk yazan oluşturur
        var source = Path.Combine(tmp.Root, "kaynak.png");
        File.WriteAllBytes(source, TinyPng);

        var path = Path.Combine(tmp.Root, "yollu.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("S");
            ws.Cell(1, 1).Value = "PRD-2";
            ws.Cell(1, 2).Value = source;   // hücrede dosya yolu
            wb.SaveAs(path);
        }

        var rows = ExcelImportService.Import(path, new ExcelImportOptions
        {
            SheetName = "S", HasHeaderRow = false, CodeColumn = 1, NameColumn = 0, ImageColumn = 2,
        });

        var row = Assert.Single(rows);
        Assert.Null(row.ImagePath);                      // kopya alınmadı
        Assert.Equal(source, row.PendingImageSource);

        var product = ExcelImportService.ToProduct(row);
        Assert.True(File.Exists(product.ImagePath));     // şimdi alındı
        Assert.NotEqual(source, product.ImagePath);      // kaynağa değil, images\GUID'e
    }
}
