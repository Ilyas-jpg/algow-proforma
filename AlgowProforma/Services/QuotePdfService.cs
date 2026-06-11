using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AlgowProforma.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlgowProforma.Services;

/// <summary>
/// Premium fiyat teklifi PDF üretimi (QuestPDF). Tek doğru kaynak: toplamlar QuoteCalculator'dan (q.Totals).
/// Türkçe için Segoe UI (her Windows'ta var, PDF'e gömülür). Üretim sonrası Linearize (hızlı ilk render).
/// Palet, Ayarlar'daki "Teklif teması"ndan gelir (AppSettings.QuoteThemeId) — katalog temasından bağımsız.
/// </summary>
public class QuotePdfService
{
    // Tema → palet eşlemesi (PdfService ile aynı alan adlandırması). theme=null → PdfTheme.Default
    // ("klasik") = eski sabit lacivert görünüm. (SecondaryHex teklif şablonunda kullanılmıyor.)
    private readonly string Primary;
    private readonly string Accent;
    private readonly string Ink;
    private readonly string Muted;
    private readonly string Line;
    private readonly string Tint;
    private const string White = "#FFFFFF";

    private static readonly CultureInfo Tr = new("tr-TR");

    private QuotePdfService(PdfTheme theme)
    {
        Primary = theme.PrimaryHex;
        Accent = theme.AccentHex;
        Ink = theme.TextHex;
        Muted = theme.MutedHex;
        Line = theme.BorderHex;
        Tint = theme.SurfaceHex;
    }

    public static void Generate(Quote q, BrandInfo brand, string outputPath, PdfTheme? theme = null)
        => new QuotePdfService(theme ?? PdfTheme.Default).Render(q, brand, outputPath);

    private void Render(Quote q, BrandInfo brand, string outputPath)
    {
        var totals = q.Totals;

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(9.5f).FontColor(Ink).LineHeight(1.25f));

                page.Header().Element(c => Header(c, q, brand));
                page.Content().Element(c => Content(c, q, brand, totals));
                page.Footer().Element(c => Footer(c, brand));
            });
        })
        .WithSettings(new DocumentSettings
        {
            // PdfService kataloğuyla aynı disiplin (feedback_questpdf_linearize_default): stream deflate +
            // JPEG ~90 + 200 DPI (QuestPDF default 288 overkill) → küçük dosya + hızlı viewer decode.
            CompressDocument = true,
            ImageCompressionQuality = ImageCompressionQuality.High,
            ImageRasterDpi = 200,
        })
        .GeneratePdf(outputPath);

        PdfPostProcessor.LinearizeIfNeeded(outputPath, hasCoverImage: true);
    }

    // ---------- başlık ----------
    private void Header(IContainer c, Quote q, BrandInfo brand)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Height(44).Element(e =>
                {
                    if (!string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        e.AlignLeft().Image(brand.LogoPath).FitHeight();
                    else
                        e.AlignLeft().Text(string.IsNullOrWhiteSpace(brand.Name) ? "FİRMA ADI" : brand.Name)
                         .FontSize(18).Bold().FontColor(Primary);
                });

                row.RelativeItem().AlignRight().Column(t =>
                {
                    t.Item().AlignRight().Text("FİYAT TEKLİFİ").FontSize(21).Bold().FontColor(Primary).LetterSpacing(0.04f);
                    var no = string.IsNullOrWhiteSpace(q.QuoteNo) ? "" : q.QuoteNo + (q.Revision > 0 ? $"  ·  rev.{q.Revision}" : "");
                    if (!string.IsNullOrEmpty(no))
                        t.Item().AlignRight().PaddingTop(2).Text(no).FontSize(10).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Primary);
        });
    }

    // ---------- içerik ----------
    private void Content(IContainer c, Quote q, BrandInfo brand, QuoteTotals totals)
    {
        c.PaddingVertical(14).Column(col =>
        {
            col.Spacing(14);

            // müşteri + teklif bilgileri
            col.Item().Row(row =>
            {
                row.RelativeItem(1.4f).Element(e => CustomerBox(e, q));
                row.ConstantItem(16);
                row.RelativeItem(1f).Element(e => MetaBox(e, q));
            });

            // kalem tablosu
            col.Item().Element(e => LinesTable(e, q));

            // toplamlar
            col.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(240).Element(e => TotalsBox(e, q, totals));
            });

            // şartlar / notlar
            col.Item().Element(e => Terms(e, q));
        });
    }

    private void CustomerBox(IContainer c, Quote q)
    {
        c.Background(Tint).Border(1).BorderColor(Line).Padding(12).Column(col =>
        {
            col.Item().Text("SAYIN").FontSize(8).Bold().FontColor(Accent).LetterSpacing(0.08f);
            col.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(q.CustomerCompany) ? "—" : q.CustomerCompany)
               .FontSize(12).Bold().FontColor(Ink);

            if (!string.IsNullOrWhiteSpace(q.CustomerContact))
                col.Item().Text(q.CustomerContact + Honorific(q.CustomerSalutation)).FontSize(9.5f).FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(q.CustomerAddress))
                col.Item().PaddingTop(4).Text(q.CustomerAddress).FontSize(9).FontColor(Muted);

            var tax = string.Join("  ·  ", new[]
            {
                string.IsNullOrWhiteSpace(q.CustomerTaxOffice) ? null : $"VD: {q.CustomerTaxOffice}",
                string.IsNullOrWhiteSpace(q.CustomerTaxNumber) ? null : $"VKN: {q.CustomerTaxNumber}",
            }.Where(s => s != null));
            if (!string.IsNullOrEmpty(tax))
                col.Item().PaddingTop(2).Text(tax).FontSize(8.5f).FontColor(Muted);
        });
    }

    private void MetaBox(IContainer c, Quote q)
    {
        c.Border(1).BorderColor(Line).Padding(12).Column(col =>
        {
            MetaRow(col, "Teklif No", string.IsNullOrWhiteSpace(q.QuoteNo) ? "—" : q.QuoteNo);
            MetaRow(col, "Tarih", q.Date.ToString("dd.MM.yyyy", Tr));
            MetaRow(col, "Geçerlilik", $"{q.ValidityDays} gün  ({q.ValidUntil.ToString("dd.MM.yyyy", Tr)})");
            MetaRow(col, "Para Birimi", q.Currency);
        });
    }

    private void MetaRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingVertical(1.5f).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9).FontColor(Muted);
            r.RelativeItem().AlignRight().Text(value).FontSize(9.5f).Bold().FontColor(Ink);
        });
    }

    private void LinesTable(IContainer c, Quote q)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(24);    // #
                cols.RelativeColumn();      // açıklama
                cols.ConstantColumn(52);    // miktar
                cols.ConstantColumn(46);    // birim
                cols.ConstantColumn(76);    // birim fiyat
                cols.ConstantColumn(38);    // isk %
                cols.ConstantColumn(82);    // tutar
            });

            table.Header(h =>
            {
                HeadCell(h, "#", TextAlign.Center);
                HeadCell(h, "AÇIKLAMA", TextAlign.Left);
                HeadCell(h, "MİKTAR", TextAlign.Right);
                HeadCell(h, "BİRİM", TextAlign.Center);
                HeadCell(h, "BİRİM FİYAT", TextAlign.Right);
                HeadCell(h, "İSK %", TextAlign.Right);
                HeadCell(h, "TUTAR", TextAlign.Right);
            });

            int i = 1;
            foreach (var l in q.Lines)
            {
                string bg = (i % 2 == 0) ? Tint : White;

                BodyCell(table, bg).AlignCenter().Text(i.ToString()).FontColor(Muted);

                BodyCell(table, bg).Column(cc =>
                {
                    cc.Item().Text(string.IsNullOrWhiteSpace(l.Name) ? "—" : l.Name).FontSize(9.5f).Bold().FontColor(Ink);
                    var sub = string.Join("  ·  ", new[]
                    {
                        string.IsNullOrWhiteSpace(l.Code) ? null : l.Code,
                        string.IsNullOrWhiteSpace(l.Description) ? null : l.Description,
                    }.Where(s => s != null));
                    if (!string.IsNullOrEmpty(sub))
                        cc.Item().Text(sub).FontSize(8).FontColor(Muted);
                });

                BodyCell(table, bg).AlignRight().Text(Num(l.Quantity));
                BodyCell(table, bg).AlignCenter().Text(l.Unit ?? "");
                BodyCell(table, bg).AlignRight().Text(Money(l.UnitPrice, q.Currency));
                BodyCell(table, bg).AlignRight().Text(l.DiscountPct > 0 ? Num(l.DiscountPct) : "—").FontColor(Muted);
                BodyCell(table, bg).AlignRight().Text(Money(l.LineNet, q.Currency)).Bold();

                i++;
            }
        });
    }

    private void HeadCell(TableCellDescriptor h, string text, TextAlign align)
    {
        var cell = h.Cell().Background(Primary).PaddingVertical(6).PaddingHorizontal(6);
        var t = align switch
        {
            TextAlign.Right => cell.AlignRight(),
            TextAlign.Center => cell.AlignCenter(),
            _ => cell.AlignLeft(),
        };
        t.Text(text).FontSize(8).Bold().FontColor(White).LetterSpacing(0.03f);
    }

    private IContainer BodyCell(TableDescriptor table, string bg)
        => table.Cell().Background(bg).BorderBottom(1).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);

    private void TotalsBox(IContainer c, Quote q, QuoteTotals t)
    {
        c.Column(col =>
        {
            TotalRow(col, "Ara Toplam", Money(t.LinesNet, q.Currency), false);
            if (t.GeneralDiscount > 0)
                TotalRow(col, "İskonto", "− " + Money(t.GeneralDiscount, q.Currency), false);
            TotalRow(col, "KDV", Money(t.VatTotal, q.Currency), false);
            col.Item().PaddingTop(4).Background(Primary).Padding(8).Row(r =>
            {
                r.RelativeItem().Text("GENEL TOPLAM").FontSize(10).Bold().FontColor(White);
                r.RelativeItem().AlignRight().Text(Money(t.GrandTotal, q.Currency)).FontSize(12).Bold().FontColor(White);
            });
        });
    }

    private void TotalRow(ColumnDescriptor col, string label, string value, bool bold)
    {
        col.Item().PaddingVertical(3).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9.5f).FontColor(Muted);
            r.RelativeItem().AlignRight().Text(value).FontSize(10).Bold().FontColor(Ink);
        });
    }

    private void Terms(IContainer c, Quote q)
    {
        c.PaddingTop(4).Column(col =>
        {
            col.Spacing(3);
            TermRow(col, "Ödeme", q.PaymentTerms);
            TermRow(col, "Teslim Süresi", q.DeliveryTime);
            TermRow(col, "Teslim Yeri", q.DeliveryPlace);
            TermRow(col, "Kur Notu", q.ExchangeRateNote);
            if (!string.IsNullOrWhiteSpace(q.Notes))
            {
                col.Item().PaddingTop(6).Text("Notlar").FontSize(8.5f).Bold().FontColor(Accent);
                col.Item().Text(q.Notes).FontSize(9).FontColor(Muted);
            }
        });
    }

    private void TermRow(ColumnDescriptor col, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Row(r =>
        {
            r.ConstantItem(96).Text(label).FontSize(9).Bold().FontColor(Ink);
            r.RelativeItem().Text(value).FontSize(9).FontColor(Muted);
        });
    }

    // ---------- altlık ----------
    private void Footer(IContainer c, BrandInfo brand)
    {
        c.Column(col =>
        {
            col.Item().PaddingBottom(6).LineHorizontal(1).LineColor(Line);
            col.Item().Row(row =>
            {
                var contact = string.Join("   ·   ", new[]
                {
                    string.IsNullOrWhiteSpace(brand.Address) ? null : brand.Address,
                    string.IsNullOrWhiteSpace(brand.Phone) ? null : brand.Phone,
                    string.IsNullOrWhiteSpace(brand.Email) ? null : brand.Email,
                    string.IsNullOrWhiteSpace(brand.Web) ? null : brand.Web,
                }.Where(s => s != null));

                row.RelativeItem().Text(contact).FontSize(7.5f).FontColor(Muted);
                row.ConstantItem(70).AlignRight().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(7.5f).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                    t.Span(" / ").FontSize(7.5f).FontColor(Muted);
                    t.TotalPages().FontSize(7.5f).FontColor(Muted);
                });
            });
        });
    }

    // ---------- yardımcılar ----------
    private enum TextAlign { Left, Center, Right }

    private static string Honorific(Salutation s) => s switch
    {
        Salutation.Bey => " Bey",
        Salutation.Hanim => " Hanım",
        _ => "",
    };

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
