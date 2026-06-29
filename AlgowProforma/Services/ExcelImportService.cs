using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

public class ExcelImportOptions
{
    public string? SheetName { get; set; }
    public bool HasHeaderRow { get; set; } = true;
    public int CodeColumn { get; set; } = 1;        // 1-based, 0 = skip
    public int NameColumn { get; set; } = 2;        // ürün adı / açıklama
    public int PriceColumn { get; set; } = 0;       // 0 = skip
    public int CategoryColumn { get; set; } = 0;    // 0 = skip (katalog bölümü — kategori sayfaları)
    public int ImageColumn { get; set; } = 0;       // 0 = otomatik (satıra anchored görsel)
    public string Currency { get; set; } = "TL";
}

public class ExcelImportPreviewRow
{
    public int RowNumber { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public string Category { get; set; } = "";

    /// <summary>Dolu = görsel zaten diskte (materialize edilmiş). Önizleme aşamasında null kalır.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Gömülü görselin bellekteki baytları — diske ANCAK gerçek import anında yazılır.
    /// Eski davranış her önizlemede (combo değişimi dahil) TÜM görselleri diske basıyordu →
    /// orphan birikimi + UI donması.</summary>
    public byte[]? PendingImageBytes { get; set; }

    /// <summary>Hücrede dosya-yolu verilen görselin kaynağı — kopya gerçek import anında alınır.</summary>
    public string? PendingImageSource { get; set; }

    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath)
                            || PendingImageBytes is { Length: > 0 }
                            || !string.IsNullOrWhiteSpace(PendingImageSource);
}

public static class ExcelImportService
{
    public static List<string> GetSheetNames(string xlsxPath)
    {
        ImportLimits.EnsureExcelSize(xlsxPath);
        using var wb = new XLWorkbook(xlsxPath);
        return wb.Worksheets.Select(w => w.Name).ToList();
    }

    public static List<ExcelImportPreviewRow> Import(string xlsxPath, ExcelImportOptions options)
    {
        ImportLimits.EnsureExcelSize(xlsxPath);
        var results = new List<ExcelImportPreviewRow>();
        using var wb = new XLWorkbook(xlsxPath);

        var ws = string.IsNullOrWhiteSpace(options.SheetName)
            ? wb.Worksheets.First()
            : wb.Worksheet(options.SheetName);

        var imagesByRow = ExtractImagesByRow(ws);

        var range = ws.RangeUsed();
        if (range is null) return results;

        int firstRow = options.HasHeaderRow ? range.FirstRow().RowNumber() + 1 : range.FirstRow().RowNumber();
        int lastRow = range.LastRow().RowNumber();

        for (int row = firstRow; row <= lastRow; row++)
        {
            var code = options.CodeColumn > 0 ? GetCellText(ws, row, options.CodeColumn) : "";
            var name = options.NameColumn > 0 ? GetCellText(ws, row, options.NameColumn) : "";
            var category = options.CategoryColumn > 0 ? GetCellText(ws, row, options.CategoryColumn) : "";
            // H① fix: Number hücresi doğrudan okunur — string round-trip + ayraç sezgisi yalnız metinde.
            decimal? price = options.PriceColumn > 0 ? ExcelDataService.CellDecimal(ws, row, options.PriceColumn) : null;

            // Önizleme disk'e DOKUNMAZ — kaynak referansı/baytlar bellekte taşınır,
            // yazım ToProduct (gerçek import) anında olur.
            string? pendingSource = null;
            byte[]? pendingBytes = null;
            if (options.ImageColumn > 0)
            {
                var fromCell = GetCellText(ws, row, options.ImageColumn);
                if (!string.IsNullOrWhiteSpace(fromCell) && File.Exists(fromCell))
                    pendingSource = fromCell;
            }
            if (pendingSource is null && imagesByRow.TryGetValue(row, out var bytes))
                pendingBytes = bytes;

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name)
                && pendingSource is null && pendingBytes is null)
                continue;

            results.Add(new ExcelImportPreviewRow
            {
                RowNumber = row,
                Code = code.Trim(),
                Name = name.Trim(),
                Price = price,
                Category = category.Trim(),
                PendingImageSource = pendingSource,
                PendingImageBytes = pendingBytes,
            });
        }

        return results;
    }

    public static Product ToProduct(ExcelImportPreviewRow row, string currency = "TL") => new()
    {
        Code = row.Code,
        Name = row.Name,
        Price = row.Price ?? 0m,
        Currency = currency,
        Category = row.Category,
        ImagePath = MaterializeImage(row) ?? "",
    };

    /// <summary>Bekleyen görseli ANCAK gerçek import anında diske yazar; sonucu row'a cache'ler
    /// (çift çağrı = çift dosya olmaz). Yazım hatasında ürün görselsiz devam eder.</summary>
    internal static string? MaterializeImage(ExcelImportPreviewRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.ImagePath)) return row.ImagePath;
        try
        {
            if (row.PendingImageBytes is { Length: > 0 })
                return row.ImagePath = SaveBytes(row.PendingImageBytes);
            if (!string.IsNullOrWhiteSpace(row.PendingImageSource) && File.Exists(row.PendingImageSource))
                return row.ImagePath = SaveCopy(row.PendingImageSource!);
        }
        catch { /* disk dolu / kilitli — görselsiz import, satır verisi kaybolmaz */ }
        return null;
    }

    private static string GetCellText(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell is null) return "";
        if (cell.IsEmpty()) return "";
        try
        {
            if (cell.DataType == XLDataType.Number)
                return cell.GetValue<double>().ToString(CultureInfo.InvariantCulture);
            return cell.GetString() ?? "";
        }
        catch { return cell.Value.ToString() ?? ""; }
    }

    private static Dictionary<int, byte[]> ExtractImagesByRow(IXLWorksheet ws)
    {
        var map = new Dictionary<int, byte[]>();
        foreach (var pic in ws.Pictures)
        {
            try
            {
                int rowNumber = pic.TopLeftCell?.Address?.RowNumber ?? 0;
                if (rowNumber <= 0) continue;
                using var ms = new MemoryStream();
                pic.ImageStream.Position = 0;
                pic.ImageStream.CopyTo(ms);
                if (!map.ContainsKey(rowNumber))
                    map[rowNumber] = ms.ToArray();
            }
            catch { /* skip unreadable pictures */ }
        }
        return map;
    }

    private static readonly string[] AllowedImageExts = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    private static string SaveCopy(string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        // Güvenlik (L1): yalnız gerçek görsel uzantıları kopyalanır — kötü-niyetli .xlsx'in görsel-sütunu
        // hücresine konmuş hassas yerel yol (örn. ...\.ssh\id_rsa) "ürün görseli" diye images\'a sızmasın.
        if (Array.IndexOf(AllowedImageExts, ext) < 0)
            throw new InvalidOperationException("Desteklenmeyen görsel türü: " + ext);
        Directory.CreateDirectory(ImageStorage.Directory);
        var target = Path.Combine(ImageStorage.Directory, $"{Guid.NewGuid():N}{ext}");
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    private static string SaveBytes(byte[] bytes)
    {
        // Güvenlik (L3): magic-byte tanınmıyorsa REDDET (eski sessiz ".png" fallback yok) — yalnız
        // gerçek görsel baytı diske yazılır.
        var ext = DetectImageExtension(bytes)
            ?? throw new InvalidOperationException("Tanınmayan görsel formatı (gömülü resim).");
        Directory.CreateDirectory(ImageStorage.Directory);
        var target = Path.Combine(ImageStorage.Directory, $"{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(target, bytes);
        return target;
    }

    private static string? DetectImageExtension(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
        if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D) return ".bmp";
        // WebP: "RIFF"...."WEBP"
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return ".webp";
        return null;   // tanınmayan → reddet (çağıran görselsiz devam eder)
    }
}
