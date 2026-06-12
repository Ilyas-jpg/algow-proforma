using System;
using System.IO;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// EN proforma + şablon varyantları render smoke'u (önizleme PNG yolu — dosya/önizleme aynı
/// Compose'dan beslenir) ve durum/dil alanlarının yaşam döngüsü kuralları.
/// </summary>
public class QuoteLanguageTemplateTests
{
    static QuoteLanguageTemplateTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static Quote SampleQuote(string lang = "tr", string template = "modern")
    {
        var q = new Quote
        {
            QuoteNo = "TST-2026-0007",
            CustomerCompany = "İhracat Müşterisi Ltd.",
            Language = lang,
            TemplateId = template,
            ExchangeRateNote = "1 USD = 41,17 TL",
        };
        q.Lines.Add(new QuoteLine { Name = "Küresel Vana DN25", Quantity = 4m, UnitPrice = 1250m, VatRate = 20m });
        q.Lines.Add(new QuoteLine { Name = "Çek Valf 1/2\"", Quantity = 10m, UnitPrice = 189.5m, VatRate = 20m, DiscountPct = 5m });
        return q;
    }

    [Theory]
    [InlineData("tr", "modern")]
    [InlineData("en", "modern")]   // PROFORMA INVOICE — etiketler + en-US sayı biçimi
    [InlineData("tr", "kompakt")]
    [InlineData("tr", "sade")]
    [InlineData("en", "sade")]
    public void PreviewPng_RendersForEveryLanguageTemplateCombo(string lang, string template)
    {
        var bytes = QuotePdfService.GeneratePreviewPngBytes(SampleQuote(lang, template), new BrandInfo { Name = "Test Marka" });

        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 1000, $"{lang}/{template}: PNG boş/çok küçük");
        Assert.Equal(0x89, bytes[0]);   // PNG sihirli baytları
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void Generate_EnglishQuote_ProducesValidPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alg-en-{Guid.NewGuid():N}.pdf");
        try
        {
            QuotePdfService.Generate(SampleQuote("en", "kompakt"), new BrandInfo { Name = "Export Co." }, path);
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000);
            Assert.Equal((byte)'%', bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Clone_CopiesLanguageTemplateAndStatus()
    {
        var q = SampleQuote("en", "sade");
        q.Status = QuoteStatus.Onaylandi;

        var c = q.Clone();

        Assert.Equal("en", c.Language);
        Assert.Equal("sade", c.TemplateId);
        Assert.Equal(QuoteStatus.Onaylandi, c.Status);
    }

    [Fact]
    public void CreateRevision_ResetsStatusToTaslak()
    {
        var q = SampleQuote();
        q.Status = QuoteStatus.Gonderildi;

        var rev = q.CreateRevision();

        Assert.Equal(QuoteStatus.Taslak, rev.Status);   // yeni müzakere turu — henüz gönderilmedi
        Assert.Equal(q.QuoteNo, rev.QuoteNo);
        Assert.Equal(q.Revision + 1, rev.Revision);
    }

    [Fact]
    public void PdfFileName_UsesRevisionSuffix()
    {
        var q = SampleQuote();
        Assert.Equal("TST-2026-0007.pdf", QuoteService.PdfFileName(q));
        var rev = q.CreateRevision();
        Assert.Equal("TST-2026-0007-rev1.pdf", QuoteService.PdfFileName(rev));
    }

    [Fact]
    public void PdfFileName_SanitizesInvalidChars()
    {
        // Kullanıcı ön eki "/" taşıyabilir ("TKF/2026-0001") — eskiden PDF hiç üretilemiyordu.
        var q = SampleQuote();
        q.QuoteNo = "TKF/2026-0001";
        Assert.Equal("TKF_2026-0001.pdf", QuoteService.PdfFileName(q));
    }
}
