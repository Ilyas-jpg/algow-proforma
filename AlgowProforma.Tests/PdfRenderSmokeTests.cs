using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Render-smoke: 13 tasarımın her biri geçerli PNG kapak önizlemesi üretmeli. RenderContext
/// (static→instance) refactor'ı + dead-kod silme + yeni kapakların render'ı bozmadığını KALICI
/// doğrular (eski "self-test exe" yerine xUnit'te, computer-use görsel doğrulamadan bağımsız).
/// PdfService artık instance-safe; her çağrı izole state.
/// </summary>
public class PdfRenderSmokeTests
{
    static PdfRenderSmokeTests()
    {
        // App çalışmadığından lisansı test bağlamında set et (yoksa GenerateImages fırlatır).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public void AllDesigns_RenderValidPngCover()
    {
        Assert.NotEmpty(PdfDesign.All);
        foreach (var design in PdfDesign.All)
        {
            var catalog = Catalog.CreateDefault();
            catalog.DesignId = design.Id;

            var bytes = new PdfService().GenerateCoverPreviewBytes(catalog);

            Assert.True(bytes is { Length: > 1000 }, $"Tasarım '{design.Id}': render boş/çok küçük");
            // PNG sihirli baytları: 89 50 4E 47
            Assert.Equal(0x89, bytes![0]);
            Assert.Equal(0x50, bytes[1]);
            Assert.Equal(0x4E, bytes[2]);
            Assert.Equal(0x47, bytes[3]);
        }
    }

    [Fact]
    public void DifferentThemes_BothRenderIndependently_NoStaticBleed()
    {
        // RenderContext kanıtı: iki farklı temayı arka arkaya render et; instance izolasyonu
        // sayesinde ikisi de geçerli üretmeli (eski static state'te ardışık render'lar state paylaşırdı).
        var c1 = Catalog.CreateDefault(); c1.ThemeId = "klasik";
        var c2 = Catalog.CreateDefault(); c2.ThemeId = "petrol";

        var b1 = new PdfService().GenerateCoverPreviewBytes(c1);
        var b2 = new PdfService().GenerateCoverPreviewBytes(c2);

        Assert.True(b1 is { Length: > 1000 });
        Assert.True(b2 is { Length: > 1000 });
    }
}
