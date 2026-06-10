using System;
using System.Globalization;
using System.Text;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

/// <summary>
/// Teklifi düz METİN olarak üretir (WhatsApp / mail gövdesi / hızlı paylaşım için).
/// Toplamlar yine QuoteCalculator'dan (q.Totals) — PDF ile aynı doğru kaynak.
/// </summary>
public static class QuoteTextService
{
    private static readonly CultureInfo Tr = new("tr-TR");

    public static string Build(Quote q, BrandInfo brand)
    {
        var t = q.Totals;
        var sb = new StringBuilder();

        var no = string.IsNullOrWhiteSpace(q.QuoteNo) ? "" : " — " + q.QuoteNo;
        var rev = q.Revision > 0 ? $" (rev.{q.Revision})" : "";
        sb.AppendLine("FİYAT TEKLİFİ" + no + rev);
        sb.AppendLine($"Tarih: {q.Date.ToString("dd.MM.yyyy", Tr)}  ·  Geçerlilik: {q.ValidityDays} gün ({q.ValidUntil.ToString("dd.MM.yyyy", Tr)})");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(q.CustomerCompany))
            sb.AppendLine("Sayın " + q.CustomerCompany);
        sb.AppendLine(new string('-', 44));

        int i = 1;
        foreach (var l in q.Lines)
        {
            var code = string.IsNullOrWhiteSpace(l.Code) ? "" : $" ({l.Code})";
            var disc = l.DiscountPct > 0 ? $"  −%{Num(l.DiscountPct)}" : "";
            sb.AppendLine($"{i}. {l.Name}{code}");
            sb.AppendLine($"   {Num(l.Quantity)} {l.Unit} × {Money(l.UnitPrice, q.Currency)}{disc} = {Money(l.LineNet, q.Currency)}");
            i++;
        }

        sb.AppendLine(new string('-', 44));
        sb.AppendLine($"Ara Toplam   : {Money(t.LinesNet, q.Currency)}");
        if (t.GeneralDiscount > 0)
            sb.AppendLine($"İskonto      : −{Money(t.GeneralDiscount, q.Currency)}");
        sb.AppendLine($"KDV          : {Money(t.VatTotal, q.Currency)}");
        sb.AppendLine($"GENEL TOPLAM : {Money(t.GrandTotal, q.Currency)}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(q.PaymentTerms)) sb.AppendLine("Ödeme: " + q.PaymentTerms);
        if (!string.IsNullOrWhiteSpace(q.DeliveryTime)) sb.AppendLine("Teslim Süresi: " + q.DeliveryTime);
        if (!string.IsNullOrWhiteSpace(q.DeliveryPlace)) sb.AppendLine("Teslim Yeri: " + q.DeliveryPlace);
        if (!string.IsNullOrWhiteSpace(q.Notes)) { sb.AppendLine(); sb.AppendLine(q.Notes); }

        sb.AppendLine();
        sb.AppendLine(brand.Name);
        if (!string.IsNullOrWhiteSpace(brand.Phone)) sb.AppendLine(brand.Phone);
        if (!string.IsNullOrWhiteSpace(brand.Email)) sb.AppendLine(brand.Email);
        if (!string.IsNullOrWhiteSpace(brand.Web)) sb.AppendLine(brand.Web);

        return sb.ToString();
    }

    private static string Num(decimal v) =>
        (v == Math.Truncate(v)) ? ((long)v).ToString(Tr) : v.ToString("#,##0.##", Tr);

    private static string Money(decimal v, string currency)
    {
        string sym = (currency ?? "TL").Trim().ToUpperInvariant() switch
        {
            "TL" or "TRY" or "₺" => "₺",
            "USD" or "$" => "$",
            "EUR" or "€" => "€",
            _ => currency ?? "",
        };
        return v.ToString("#,##0.00", Tr) + " " + sym;
    }
}
