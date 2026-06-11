using System;
using System.IO;
using QuestPDF.Fluent;

namespace AlgowProforma.Services;

// QuestPDF'in ürettiği PDF default'ta NON-LINEARIZED — xref tablosu dosyanın sonunda.
// Viewer ilk sayfayı çizebilmek için tüm dosyayı parse etmek zorunda, bu da
// "başta beyaz ekran sonra içerik" gecikmesinin ana sebebi. Linearize ile xref
// öne taşınır, viewer progressive render edebilir.
//
// QuestPDF 2024.12.0+ DocumentOperation.Linearize() arka planda qpdf (Apache-2.0)
// wrap ediyor — harici binary'e gerek yok, lisans temiz.
public static class PdfPostProcessor
{
    // Generate sonrası çağrılır. ShouldLinearize false dönerse no-op (CPU/zaman tasarrufu).
    // Linearize fail olursa orijinal PDF korunur (silent fallback + log).
    public static void LinearizeIfNeeded(string pdfPath, bool hasCoverImage)
    {
        try
        {
            var info = new FileInfo(pdfPath);
            if (!info.Exists) return;

            if (!ShouldLinearize(info.Length, hasCoverImage))
                return;

            var tempPath = pdfPath + ".linearizing.tmp";

            DocumentOperation
                .LoadFile(pdfPath)
                .Linearize()
                .Save(tempPath);

            File.Delete(pdfPath);
            File.Move(tempPath, pdfPath);
        }
        catch (Exception ex)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AlgowProforma");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "post-process.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Linearize failed for {pdfPath}: {ex.Message}{Environment.NewLine}");
            }
            catch { /* logging best-effort */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DOMAIN KARAR — buraya senin policy gelecek
    //
    // Trade-off'lar:
    //   • Linearize ~0.3–2 sn CPU ekler (dosya büyüdükçe uzar)
    //   • Sonuç dosya boyutu ~%5–10 artar (xref ayrı yazıldığı için)
    //   • Açılış hızı 2-3× iyileşir (özellikle 5+ sayfa, foto-yoğun, Türkçe font'lu PDF'de)
    //
    // Müşteri kullanım profili:
    //   • "Ürün bilgi formu" (1 sayfa, ~200 KB) → zaten anında açılır, linearize CPU yer
    //   • Ana "ürün kataloğu" 20–50 sayfa, cover image'lı, 1–5 MB → linearize değerli
    //   • Müşteri batch üretirse (78 vana stres testi) → 78×0.5s = 39s gecikme;
    //     sadece nihai teslim PDF'lerini linearize etmek mantıklı, test runner'da skip
    //
    // Örnek policy yaklaşımları:
    //   a) Boyut-temelli:   fileSize >= 500_000           (500 KB altı skip)
    //   b) Cover-temelli:   hasCoverImage                  (sadece foto'lu kapaklı PDF)
    //   c) Kombine:         fileSize >= 300_000 || hasCoverImage
    //   d) Her zaman:       true                           (basit, %5 dosya şişmesini önemseme)
    // ─────────────────────────────────────────────────────────────────────────
    private static bool ShouldLinearize(long fileSizeBytes, bool hasCoverImage)
    {
        // Policy (İlyas, 2026-05-22): Her zaman linearize.
        // Müşteriye hangi PDF gideceği üretim anında belirsiz — batch test çıktıları da
        // örnek olarak gönderilebilir, hepsi web-friendly olsun. ~0.3-2sn CPU + ~%5 boyut
        // artışı kabul edildi.
        return true;
    }
}
