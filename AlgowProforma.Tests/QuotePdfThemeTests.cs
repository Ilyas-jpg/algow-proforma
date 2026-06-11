using System;
using System.IO;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Teklif PDF tema bağlama smoke'u: varsayılan (tema parametresiz = klasik) ve klasik-dışı bir tema
/// ile üretim geçerli PDF dosyası vermeli. Tema kaynağı: Ayarlar → QuoteThemeId (2026-06-12 kararı).
/// </summary>
public class QuotePdfThemeTests
{
    static QuotePdfThemeTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static Quote SampleQuote()
    {
        var q = new Quote { QuoteNo = "TST-2026-0001", CustomerCompany = "Tema Testi A.Ş." };
        q.Lines.Add(new QuoteLine { Name = "Küresel Vana DN25", Quantity = 4m, UnitPrice = 1250m, VatRate = 20m });
        return q;
    }

    [Theory]
    [InlineData(null)]        // parametresiz çağrı = klasik (geriye uyumluluk)
    [InlineData("kor")]       // canlı kırmızı — klasik'ten uzak bir palet
    [InlineData("petrol")]
    public void Generate_WithTheme_ProducesValidPdf(string? themeId)
    {
        var theme = themeId is null ? null : PdfTheme.GetById(themeId);
        var path = Path.Combine(Path.GetTempPath(), $"alg-qpdf-{Guid.NewGuid():N}.pdf");
        try
        {
            QuotePdfService.Generate(SampleQuote(), new BrandInfo { Name = "Tema Testi" }, path, theme);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000, $"Tema '{themeId ?? "default"}': PDF boş/çok küçük");
            // PDF sihirli baytları: %PDF
            Assert.Equal((byte)'%', bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'D', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
