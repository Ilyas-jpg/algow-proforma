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
    public int ImageColumn { get; set; } = 0;       // 0 = otomatik (satıra anchored görsel)
    public string Currency { get; set; } = "TL";
}

public class ExcelImportPreviewRow
{
    public int RowNumber { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
    public string? ImagePath { get; set; }
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);
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
            decimal? price = options.PriceColumn > 0 ? ParseDecimal(GetCellText(ws, row, options.PriceColumn)) : null;

            string? imagePath = null;
            if (options.ImageColumn > 0)
            {
                var fromCell = GetCellText(ws, row, options.ImageColumn);
                if (!string.IsNullOrWhiteSpace(fromCell) && File.Exists(fromCell))
                    imagePath = SaveCopy(fromCell);
            }
            if (imagePath is null && imagesByRow.TryGetValue(row, out var bytes))
                imagePath = SaveBytes(bytes);

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name) && imagePath is null)
                continue;

            results.Add(new ExcelImportPreviewRow
            {
                RowNumber = row,
                Code = code.Trim(),
                Name = name.Trim(),
                Price = price,
                ImagePath = imagePath,
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
        ImagePath = row.ImagePath ?? "",
    };

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

    private static decimal? ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace("₺", "").Replace("TL", "").Replace("$", "").Replace("€", "").Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out v)) return v;
        return null;
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

    private static string SaveCopy(string sourcePath)
    {
        Directory.CreateDirectory(ImageStorage.Directory);
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var target = Path.Combine(ImageStorage.Directory, $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}");
        File.Copy(sourcePath, target, overwrite: false);
        return target;
    }

    private static string SaveBytes(byte[] bytes)
    {
        Directory.CreateDirectory(ImageStorage.Directory);
        var ext = DetectImageExtension(bytes);
        var target = Path.Combine(ImageStorage.Directory, $"{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(target, bytes);
        return target;
    }

    private static string DetectImageExtension(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
        if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D) return ".bmp";
        return ".png";
    }
}
