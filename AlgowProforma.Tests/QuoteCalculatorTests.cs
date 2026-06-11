using AlgowProforma.Models;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Para hesabı = en kritik test yüzeyi. QuoteCalculator satır iskontosu, genel iskonto (yüzde/tutar),
/// farklı KDV oranlarında ORANSAL dağıtım ve AwayFromZero yuvarlamayı doğru yapmalı.
/// </summary>
public class QuoteCalculatorTests
{
    private static QuoteLine Line(decimal qty, decimal price, decimal disc = 0, decimal vat = 20) =>
        new() { Quantity = qty, UnitPrice = price, DiscountPct = disc, VatRate = vat };

    [Fact]
    public void SingleLine_NoDiscount()
    {
        var q = new Quote();
        q.Lines.Add(Line(10, 100, vat: 20));
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(1000m, t.LinesNet);
        Assert.Equal(0m, t.GeneralDiscount);
        Assert.Equal(1000m, t.NetTotal);
        Assert.Equal(200m, t.VatTotal);
        Assert.Equal(1200m, t.GrandTotal);
    }

    [Fact]
    public void LineDiscount_Applied()
    {
        var q = new Quote();
        q.Lines.Add(Line(10, 100, disc: 10, vat: 20)); // 1000 -> 900 net
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(900m, t.LinesNet);
        Assert.Equal(180m, t.VatTotal);
        Assert.Equal(1080m, t.GrandTotal);
    }

    [Fact]
    public void GeneralPercentDiscount_SingleVat()
    {
        var q = new Quote { DiscountMode = DiscountMode.Yuzde, DiscountValue = 10 };
        q.Lines.Add(Line(10, 100, vat: 20)); // 1000
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(1000m, t.LinesNet);
        Assert.Equal(100m, t.GeneralDiscount);
        Assert.Equal(900m, t.NetTotal);
        Assert.Equal(180m, t.VatTotal);   // 1000 * 0.9 * 0.20
        Assert.Equal(1080m, t.GrandTotal);
    }

    [Fact]
    public void GeneralDiscount_MixedVat_ProportionalDistribution()
    {
        // KRİTİK: genel iskonto farklı KDV oranlı satırlara oransal yansır → KDV doğru kalır.
        var q = new Quote { DiscountMode = DiscountMode.Yuzde, DiscountValue = 10 };
        q.Lines.Add(Line(1, 1000, vat: 20)); // net 1000, KDV %20
        q.Lines.Add(Line(1, 1000, vat: 10)); // net 1000, KDV %10
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(2000m, t.LinesNet);
        Assert.Equal(200m, t.GeneralDiscount);
        Assert.Equal(1800m, t.NetTotal);
        Assert.Equal(270m, t.VatTotal);     // 1000*0.9*0.20 + 1000*0.9*0.10 = 180 + 90
        Assert.Equal(2070m, t.GrandTotal);
    }

    [Fact]
    public void GeneralFixedDiscount_ClampedToLinesNet()
    {
        var q = new Quote { DiscountMode = DiscountMode.Tutar, DiscountValue = 5000 };
        q.Lines.Add(Line(1, 1000, vat: 20)); // 1000
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(1000m, t.GeneralDiscount); // toplamla sınırlı
        Assert.Equal(0m, t.NetTotal);
        Assert.Equal(0m, t.VatTotal);
        Assert.Equal(0m, t.GrandTotal);
    }

    [Fact]
    public void DiscountModeYok_IgnoresValue()
    {
        var q = new Quote { DiscountMode = DiscountMode.Yok, DiscountValue = 50 };
        q.Lines.Add(Line(1, 1000, vat: 20));
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(0m, t.GeneralDiscount);
        Assert.Equal(1000m, t.NetTotal);
    }

    [Fact]
    public void Rounding_AwayFromZero()
    {
        var q = new Quote();
        q.Lines.Add(Line(1, 1.005m, vat: 0)); // 1.005 -> 1.01 (away from zero, banker's olsa 1.00)
        var t = QuoteCalculator.Compute(q);
        Assert.Equal(1.01m, t.LinesNet);
    }

    [Fact]
    public void EmptyQuote_AllZero()
    {
        var t = QuoteCalculator.Compute(new Quote());
        Assert.Equal(0m, t.LinesNet);
        Assert.Equal(0m, t.GrandTotal);
    }
}
