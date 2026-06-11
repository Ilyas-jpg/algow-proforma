using System.Collections.Generic;
using System.Linq;

namespace AlgowProforma.Models;

public class PageLayout
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Columns { get; init; }
    public int Rows { get; init; }

    public int Total => Columns * Rows;
    public IReadOnlyList<int> Cells => Enumerable.Range(0, Total).ToList();
    public string Summary => $"{Columns} sütun × {Rows} satır = {Total} ürün";

    public static PageLayout Default => Standart;

    public static PageLayout Tek { get; } = new()
    {
        Id = "1x1", Name = "1 Ürün", Columns = 1, Rows = 1,
        Description = "Sayfa başına 1 ürün — tek büyük showcase kartı",
    };

    public static PageLayout Ikili { get; } = new()
    {
        Id = "1x2", Name = "2 Ürün", Columns = 1, Rows = 2,
        Description = "Sayfa başına 2 ürün — alt alta iki tam genişlik kart",
    };

    public static PageLayout Premium { get; } = new()
    {
        Id = "2x2", Name = "4 Ürün", Columns = 2, Rows = 2,
        Description = "Sayfa başına 4 ürün — geniş kartlar, görsel öne çıkarma",
    };

    public static PageLayout Detayli { get; } = new()
    {
        Id = "2x3", Name = "6 Ürün", Columns = 2, Rows = 3,
        Description = "Sayfa başına 6 ürün — daha geniş ürün kartları",
    };

    public static PageLayout SekizUrun { get; } = new()
    {
        Id = "2x4", Name = "8 Ürün", Columns = 2, Rows = 4,
        Description = "Sayfa başına 8 ürün — iki sütun, dört satır",
    };

    public static PageLayout Standart { get; } = new()
    {
        Id = "3x3", Name = "9 Ürün", Columns = 3, Rows = 3,
        Description = "Sayfa başına 9 ürün — varsayılan denge",
    };

    public static PageLayout Kompakt { get; } = new()
    {
        Id = "4x3", Name = "12 Ürün (4×3)", Columns = 4, Rows = 3,
        Description = "Sayfa başına 12 ürün — geniş yerleşim, dar kartlar",
    };

    public static PageLayout Yogun { get; } = new()
    {
        Id = "3x4", Name = "12 Ürün (3×4)", Columns = 3, Rows = 4,
        Description = "Sayfa başına 12 ürün — uzun yerleşim, kısa kartlar",
    };

    public static PageLayout CokYogun { get; } = new()
    {
        Id = "4x4", Name = "16 Ürün", Columns = 4, Rows = 4,
        Description = "Sayfa başına 16 ürün — utility katalog",
    };

    public static IReadOnlyList<PageLayout> All { get; } = new List<PageLayout>
    {
        Tek,
        Ikili,
        Premium,
        Detayli,
        SekizUrun,
        Standart,
        Kompakt,
        Yogun,
        CokYogun,
    };

    public static PageLayout GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Default;
        // Eski "2x1" id'si (yan yana 2 ürün) → şimdi "1x2" (alt alta) ile aynı düzene haritalanır
        if (id == "2x1") return Ikili;
        foreach (var l in All)
            if (l.Id == id) return l;
        return Default;
    }
}
