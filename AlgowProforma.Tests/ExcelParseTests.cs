using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// H1 regresyonu: ayraç-analizli ondalık parse. Eski invariant-first mantık Türkçe "12.500"ü
/// (=12500) 12,5 yapıp fiyatı 1000 KAT bozuyordu — ürün/kütüphane Excel importu artık
/// ExcelDataService.ParseDecimal'i (tek doğru kaynak) kullanır.
/// </summary>
public class ExcelParseTests
{
    [Theory]
    [InlineData("12.500", 12500)]          // TR binlik — 1000× bug'ın kendisi
    [InlineData("12,50", 12.50)]           // TR ondalık
    [InlineData("1.234,56", 1234.56)]      // TR tam biçim
    [InlineData("1,234.56", 1234.56)]      // EN tam biçim
    [InlineData("12.5", 12.5)]             // tek nokta + 1-2 hane → ondalık
    [InlineData("1.234.567", 1234567)]     // çok noktalı → binlik
    [InlineData("1850", 1850)]             // düz tam sayı
    [InlineData("₺1.250", 1250)]           // para sembolü temizliği
    [InlineData("1.250 TL", 1250)]
    public void ParseDecimal_SeparatorAware(string input, decimal expected)
        => Assert.Equal(expected, ExcelDataService.ParseDecimal(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void ParseDecimal_Garbage_ReturnsNull(string input)
        => Assert.Null(ExcelDataService.ParseDecimal(input));
}

/// <summary>Para belgesi girdi sınırları: negatif/taşkın değer eksi tutar üretemez (M fix).</summary>
public class QuoteLineClampTests
{
    [Fact]
    public void DiscountPct_Clamped_0_100()
    {
        var l = new QuoteLine { Quantity = 10m, UnitPrice = 100m, DiscountPct = 150m };
        Assert.Equal(100m, l.DiscountPct);
        Assert.Equal(0m, l.LineNet);          // %100 iskonto → 0, asla eksi değil

        l.DiscountPct = -5m;
        Assert.Equal(0m, l.DiscountPct);
        Assert.Equal(1000m, l.LineNet);
    }

    [Fact]
    public void Quantity_UnitPrice_VatRate_NotNegative()
    {
        var l = new QuoteLine { Quantity = -3m, UnitPrice = -50m, VatRate = -18m };
        Assert.Equal(0m, l.Quantity);
        Assert.Equal(0m, l.UnitPrice);
        Assert.Equal(0m, l.VatRate);
        Assert.Equal(0m, l.LineGross);
    }
}
