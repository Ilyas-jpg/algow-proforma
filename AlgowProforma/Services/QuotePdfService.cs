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
///
/// Şablonlar (Quote.TemplateId): "modern" (varsayılan, eski birebir görünüm) · "kompakt" (dar kenar,
/// küçük punto — uzun kalem listesi tek sayfaya) · "sade" (tek renk, dolgusuz — mürekkep dostu).
/// Dil (Quote.Language): "tr" (varsayılan) · "en" (PROFORMA INVOICE — ihracat; sayı biçimi en-US).
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

    // ---- şablon/dil durumu — Compose başında quote'tan set edilir (instance per render) ----
    private L10n L = Tr10n;
    private CultureInfo Cul = new("tr-TR");
    private bool Compact;
    private bool Plain;

    /// <summary>Sade şablonda renkli vurgular tek renge (mürekkep) iner.</summary>
    private string Pr => Plain ? Ink : Primary;
    private string Ac => Plain ? Ink : Accent;

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
        => new QuotePdfService(theme ?? PdfTheme.Default).RenderToFile(q, brand, outputPath);

    private static readonly object PreviewLock = new();

    /// <summary>
    /// İlk sayfanın PNG'si (96 DPI) — teklif editöründeki canlı önizleme paneli için.
    /// Hata editörü çökertmez: null döner, ayrıntı render-errors.log'a düşer.
    /// </summary>
    public static byte[]? GeneratePreviewPngBytes(Quote q, BrandInfo brand, PdfTheme? theme = null)
    {
        lock (PreviewLock)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                var images = new QuotePdfService(theme ?? PdfTheme.Default)
                    .Compose(q, brand)
                    .GenerateImages(new ImageGenerationSettings
                    {
                        ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
                        RasterDpi = 96,
                    });
                foreach (var img in images) return img;   // ilk sayfa yeter
                return null;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(AppPaths.AppDataDir);
                    File.AppendAllText(Path.Combine(AppPaths.AppDataDir, "render-errors.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Quote preview: {ex}{Environment.NewLine}");
                }
                catch { }
                return null;
            }
        }
    }

    private void RenderToFile(Quote q, BrandInfo brand, string outputPath)
    {
        Compose(q, brand)
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

    /// <summary>Belge kompozisyonu — dosya üretimi ve önizleme aynı kaynaktan beslenir (drift yok).</summary>
    private Document Compose(Quote q, BrandInfo brand)
    {
        var en = string.Equals(q.Language, "en", StringComparison.OrdinalIgnoreCase);
        L = en ? En10n : Tr10n;
        Cul = new CultureInfo(en ? "en-US" : "tr-TR");
        Compact = string.Equals(q.TemplateId, "kompakt", StringComparison.OrdinalIgnoreCase);
        Plain = string.Equals(q.TemplateId, "sade", StringComparison.OrdinalIgnoreCase);

        var totals = q.Totals;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(Compact ? 26 : 34);
                page.DefaultTextStyle(t => t.FontFamily("Segoe UI")
                    .FontSize(Compact ? 8.7f : 9.5f).FontColor(Ink).LineHeight(1.25f));

                page.Header().Element(c => Header(c, q, brand));
                page.Content().Element(c => Content(c, q, brand, totals));
                page.Footer().Element(c => Footer(c, brand));
            });
        });
    }

    // ---------- başlık ----------
    private void Header(IContainer c, Quote q, BrandInfo brand)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Height(Compact ? 36 : 44).Element(e =>
                {
                    if (!string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        // H② fix: FitHeight genişliği sınırlamaz — geniş wordmark logo (örn. 10:1)
                        // 44pt yüksekliğe ölçeklenince satırı taşırıp DocumentLayoutException atıyordu
                        // (o markada TÜM teklif üretimi kırılır). Katalog deseni: MaxWidth + FitArea —
                        // logo hem yükseklik hem genişlik kutusuna sığacak şekilde ölçeklenir.
                        e.AlignLeft().AlignMiddle().MaxWidth(220).Image(PdfImageCache.Get(brand.LogoPath)).FitArea();
                    else
                        e.AlignLeft().Text(string.IsNullOrWhiteSpace(brand.Name) ? L.CompanyFallback : brand.Name)
                         .FontSize(Compact ? 15 : 18).Bold().FontColor(Pr);
                });

                row.RelativeItem().AlignRight().Column(t =>
                {
                    t.Item().AlignRight().Text(L.Title)
                        .FontSize(Compact ? 17 : 21).Bold().FontColor(Pr).LetterSpacing(0.04f);
                    var no = string.IsNullOrWhiteSpace(q.QuoteNo) ? "" : q.QuoteNo + (q.Revision > 0 ? $"  ·  rev.{q.Revision}" : "");
                    if (!string.IsNullOrEmpty(no))
                        t.Item().AlignRight().PaddingTop(2).Text(no).FontSize(10).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(Compact ? 7 : 10).LineHorizontal(Plain ? 1.2f : 2).LineColor(Pr);
        });
    }

    // ---------- içerik ----------
    private void Content(IContainer c, Quote q, BrandInfo brand, QuoteTotals totals)
    {
        c.PaddingVertical(Compact ? 10 : 14).Column(col =>
        {
            col.Spacing(Compact ? 10 : 14);

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
        c.Background(Plain ? White : Tint).Border(1).BorderColor(Line).Padding(Compact ? 9 : 12).Column(col =>
        {
            col.Item().Text(L.Dear).FontSize(8).Bold().FontColor(Ac).LetterSpacing(0.08f);
            col.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(q.CustomerCompany) ? "—" : q.CustomerCompany)
               .FontSize(12).Bold().FontColor(Ink);

            if (!string.IsNullOrWhiteSpace(q.CustomerContact))
                col.Item().Text(q.CustomerContact + Honorific(q.CustomerSalutation)).FontSize(9.5f).FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(q.CustomerAddress))
                col.Item().PaddingTop(4).Text(q.CustomerAddress).FontSize(9).FontColor(Muted);

            var tax = string.Join("  ·  ", new[]
            {
                string.IsNullOrWhiteSpace(q.CustomerTaxOffice) ? null : $"{L.TaxOffice}: {q.CustomerTaxOffice}",
                string.IsNullOrWhiteSpace(q.CustomerTaxNumber) ? null : $"{L.TaxNo}: {q.CustomerTaxNumber}",
            }.Where(s => s != null));
            if (!string.IsNullOrEmpty(tax))
                col.Item().PaddingTop(2).Text(tax).FontSize(8.5f).FontColor(Muted);
        });
    }

    /// <summary>EN proforma uluslararası tarih biçimi kullanır ("15 Jun 2026") — "15.06.2026"
    /// ABD'li okuyucuda ay/gün karışıklığı yaratır. TR'de yerleşik dd.MM.yyyy korunur.</summary>
    private string DateFmt => ReferenceEquals(L, En10n) ? "dd MMM yyyy" : "dd.MM.yyyy";

    private void MetaBox(IContainer c, Quote q)
    {
        c.Border(1).BorderColor(Line).Padding(Compact ? 9 : 12).Column(col =>
        {
            MetaRow(col, L.QuoteNo, string.IsNullOrWhiteSpace(q.QuoteNo) ? "—" : q.QuoteNo);
            MetaRow(col, L.Date, q.Date.ToString(DateFmt, Cul));
            MetaRow(col, L.Validity, $"{q.ValidityDays} {L.DaysSuffix}  ({q.ValidUntil.ToString(DateFmt, Cul)})");
            MetaRow(col, L.Currency, q.Currency);
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
                cols.ConstantColumn(46);    // isk % — EN "DISC %" başlığı 38pt'e sığmıyordu
                cols.ConstantColumn(82);    // tutar
            });

            table.Header(h =>
            {
                HeadCell(h, "#", TextAlign.Center);
                HeadCell(h, L.ColDesc, TextAlign.Left);
                HeadCell(h, L.ColQty, TextAlign.Right);
                HeadCell(h, L.ColUnit, TextAlign.Center);
                HeadCell(h, L.ColUnitPrice, TextAlign.Right);
                HeadCell(h, L.ColDisc, TextAlign.Right);
                HeadCell(h, L.ColAmount, TextAlign.Right);
            });

            int i = 1;
            foreach (var l in q.Lines)
            {
                string bg = (!Plain && i % 2 == 0) ? Tint : White;   // sade: zebra yok

                BodyCell(table, bg).AlignCenter().Text(i.ToString()).FontColor(Muted);

                BodyCell(table, bg).Column(cc =>
                {
                    cc.Item().Text(string.IsNullOrWhiteSpace(l.Name) ? "—" : l.Name)
                        .FontSize(Compact ? 8.8f : 9.5f).Bold().FontColor(Ink);
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
        // Sade: dolgu yok — beyaz zemin + kalın alt çizgi + mürekkep metin (yazıcı dostu).
        var cell = Plain
            ? h.Cell().Background(White).BorderBottom(1.4f).BorderColor(Ink)
                .PaddingVertical(Compact ? 4 : 6).PaddingHorizontal(Compact ? 4 : 6)
            : h.Cell().Background(Primary)
                .PaddingVertical(Compact ? 4 : 6).PaddingHorizontal(Compact ? 4 : 6);
        var t = align switch
        {
            TextAlign.Right => cell.AlignRight(),
            TextAlign.Center => cell.AlignCenter(),
            _ => cell.AlignLeft(),
        };
        t.Text(text).FontSize(8).Bold().FontColor(Plain ? Ink : White).LetterSpacing(0.03f);
    }

    private IContainer BodyCell(TableDescriptor table, string bg)
        => table.Cell().Background(bg).BorderBottom(1).BorderColor(Line)
            .PaddingVertical(Compact ? 4 : 6).PaddingHorizontal(Compact ? 4 : 6);

    private void TotalsBox(IContainer c, Quote q, QuoteTotals t)
    {
        c.Column(col =>
        {
            TotalRow(col, L.Subtotal, Money(t.LinesNet, q.Currency));
            if (t.GeneralDiscount > 0)
                TotalRow(col, L.Discount, "− " + Money(t.GeneralDiscount, q.Currency));
            TotalRow(col, L.Vat, Money(t.VatTotal, q.Currency));

            // Sade: dolgu yerine çerçeve — genel toplam yine en güçlü öğe kalır.
            var band = Plain
                ? col.Item().PaddingTop(4).Border(1.4f).BorderColor(Ink).Padding(8)
                : col.Item().PaddingTop(4).Background(Primary).Padding(8);
            band.Row(r =>
            {
                r.RelativeItem().Text(L.GrandTotal).FontSize(10).Bold().FontColor(Plain ? Ink : White);
                r.RelativeItem().AlignRight().Text(Money(t.GrandTotal, q.Currency))
                    .FontSize(12).Bold().FontColor(Plain ? Ink : White);
            });
        });
    }

    private void TotalRow(ColumnDescriptor col, string label, string value)
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
            TermRow(col, L.Payment, q.PaymentTerms);
            TermRow(col, L.DeliveryTime, q.DeliveryTime);
            TermRow(col, L.DeliveryPlace, q.DeliveryPlace);
            TermRow(col, L.FxNote, q.ExchangeRateNote);
            if (!string.IsNullOrWhiteSpace(q.Notes))
            {
                col.Item().PaddingTop(6).Text(L.Notes).FontSize(8.5f).Bold().FontColor(Ac);
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
                    t.Span(L.Page + " ").FontSize(7.5f).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                    t.Span(" / ").FontSize(7.5f).FontColor(Muted);
                    t.TotalPages().FontSize(7.5f).FontColor(Muted);
                });
            });
        });
    }

    // ---------- yardımcılar ----------
    private enum TextAlign { Left, Center, Right }

    /// <summary>TR: ad SONRASI "Bey/Hanım". EN'de Türkçe hitap basılmaz (önek/sonek uyuşmazlığı).</summary>
    private string Honorific(Salutation s) => ReferenceEquals(L, En10n) ? "" : s switch
    {
        Salutation.Bey => " Bey",
        Salutation.Hanim => " Hanım",
        _ => "",
    };

    // L1: decimal→long cast checked'tir → ~9.2e18 üstü tam-sayı tutarda OverflowException atıp metin/
    // önizleme üretimini kırardı. Tek format yeterli: tam sayıda ondalık basmaz, miktarda 4 haneye
    // kadar gösterir (L2: "#,##0.##" 3+ ondalıklı miktarı 2'ye yuvarlıyordu).
    private string Num(decimal v) => v.ToString("#,##0.####", Cul);

    private string Money(decimal v, string currency)
    {
        // EN proformada ₺ yerine ISO kodu "TRY" — ihracat alıcısının bankası/gümrüğü sembolü tanımayabilir.
        string sym = (currency ?? "TL").Trim().ToUpperInvariant() switch
        {
            "TL" or "TRY" or "₺" => ReferenceEquals(L, En10n) ? "TRY" : "₺",
            "USD" or "$" => "$",
            "EUR" or "€" => "€",
            _ => currency ?? "",
        };
        return v.ToString("#,##0.00", Cul) + " " + sym;
    }

    // ---------- etiket sözlükleri ----------
    private sealed record L10n(
        string Title, string Dear, string QuoteNo, string Date, string Validity, string DaysSuffix,
        string Currency, string ColDesc, string ColQty, string ColUnit, string ColUnitPrice,
        string ColDisc, string ColAmount, string Subtotal, string Discount, string Vat, string GrandTotal,
        string Payment, string DeliveryTime, string DeliveryPlace, string FxNote, string Notes,
        string Page, string TaxOffice, string TaxNo, string CompanyFallback);

    private static readonly L10n Tr10n = new(
        "FİYAT TEKLİFİ", "SAYIN", "Teklif No", "Tarih", "Geçerlilik", "gün",
        "Para Birimi", "AÇIKLAMA", "MİKTAR", "BİRİM", "BİRİM FİYAT",
        "İSK %", "TUTAR", "Ara Toplam", "İskonto", "KDV", "GENEL TOPLAM",
        "Ödeme", "Teslim Süresi", "Teslim Yeri", "Kur Notu", "Notlar",
        "Sayfa", "VD", "VKN", "FİRMA ADI");

    private static readonly L10n En10n = new(
        "PROFORMA INVOICE", "TO", "Quote No", "Date", "Validity", "days",
        "Currency", "DESCRIPTION", "QTY", "UNIT", "UNIT PRICE",
        "DISC %", "AMOUNT", "Subtotal", "Discount", "VAT", "GRAND TOTAL",
        "Payment", "Delivery Time", "Delivery Place", "FX Note", "Notes",
        "Page", "Tax Office", "Tax No", "COMPANY NAME");
}
