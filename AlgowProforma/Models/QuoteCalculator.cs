using System;

namespace AlgowProforma.Models;

/// <summary>Teklif toplam sonuçları (tek doğru kaynak).</summary>
public readonly record struct QuoteTotals(
    decimal LinesNet,        // satır iskontoları sonrası net toplam (genel iskonto öncesi, KDV matrahı brüt)
    decimal GeneralDiscount, // genel (teklif geneli) iskonto tutarı
    decimal NetTotal,        // KDV matrahı = LinesNet - GeneralDiscount
    decimal VatTotal,        // toplam KDV
    decimal GrandTotal);     // genel toplam = NetTotal + VatTotal

/// <summary>
/// Teklif toplam motoru. Tek merkez — UI elle hesap yapmaz, PDF de buradan okur.
/// Kritik kural: genel iskonto, farklı KDV oranlı satırlar olduğunda KDV matrahına
/// ORANSAL dağıtılır (satır net payına göre), böylece KDV tutarı doğru kalır.
/// Birim fiyatlar KDV HARİÇ (net) varsayılır.
/// </summary>
public static class QuoteCalculator
{
    public static QuoteTotals Compute(Quote q)
    {
        decimal linesNet = 0m;
        foreach (var l in q.Lines) linesNet += l.LineNet;

        decimal genDisc = q.DiscountMode switch
        {
            DiscountMode.Yuzde => Round(linesNet * Clamp(q.DiscountValue, 0m, 100m) / 100m),
            DiscountMode.Tutar => Clamp(q.DiscountValue, 0m, linesNet),
            _ => 0m
        };

        // Genel iskonto oranı — her satırın matrahına ve dolayısıyla KDV'sine eşit oranda uygulanır.
        decimal factor = linesNet > 0m ? (linesNet - genDisc) / linesNet : 1m;

        decimal vat = 0m;
        foreach (var l in q.Lines)
            vat += Round(l.LineNet * factor * l.VatRate / 100m);

        decimal net = linesNet - genDisc;
        return new QuoteTotals(linesNet, genDisc, net, vat, net + vat);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal Clamp(decimal v, decimal min, decimal max) => v < min ? min : (v > max ? max : v);
}
