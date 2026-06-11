using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlgowProforma;

public partial class App : Application
{
    // Lazy path — Türkçe karakterli username'de static field initializer'da crash etmemesi için method olarak
    private static string GetCrashLogPath()
    {
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AlgowProforma", "crash.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "AlgowProforma-crash.log");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0] == "--showcase")
        {
            RunShowcase();
            Shutdown(0);
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--stress-test")
        {
            RunStressTest();
            Shutdown(0);
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--test-covers")
        {
            RunCoverShowcase();
            Shutdown(0);
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--vana-stress")
        {
            RunVanaStressTest();
            Shutdown(0);
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--test-quote")
        {
            try
            {
                Settings.License = LicenseType.Community;
                var brand = AlgowProforma.Models.Catalog.CreateDefault().Brand;
                var quote = new AlgowProforma.Models.Quote
                {
                    QuoteNo = "ALG-2026-0001",
                    Date = DateTime.Today,
                    ValidityDays = 15,
                    Currency = "TL",
                    CustomerCompany = "Örnek Mühendislik San. Tic. Ltd. Şti.",
                    CustomerContact = "Ahmet Yılmaz",
                    CustomerSalutation = AlgowProforma.Models.Salutation.Bey,
                    CustomerAddress = "Organize Sanayi Bölgesi 5. Cadde No:12, Yenimahalle / Ankara",
                    CustomerTaxOffice = "Ostim",
                    CustomerTaxNumber = "1234567890",
                    PaymentTerms = "%50 peşin, %50 teslimde",
                    DeliveryTime = "Sipariş onayından sonra 10 iş günü",
                    DeliveryPlace = "Alıcı adresi (nakliye dahil)",
                    Notes = "Stok durumuna göre teyit gereklidir. Teklif geçerlilik süresi sonunda fiyatlar güncellenebilir.",
                    DiscountMode = AlgowProforma.Models.DiscountMode.Yuzde,
                    DiscountValue = 5m,
                };
                quote.Lines.Add(new AlgowProforma.Models.QuoteLine { Code = "CER0018", Name = "Fark Basınç Kontrol Vanası DN25", Description = "Pirinç gövde · 16 bar", Unit = "adet", Quantity = 10, UnitPrice = 1850m, VatRate = 20m });
                quote.Lines.Add(new AlgowProforma.Models.QuoteLine { Code = "CER0011", Name = "Termovil Grubu 100 mm", Unit = "adet", Quantity = 25, UnitPrice = 145m, VatRate = 20m, DiscountPct = 10m });
                quote.Lines.Add(new AlgowProforma.Models.QuoteLine { Code = "CER0001", Name = "Kör Tapa 1/2\"", Unit = "adet", Quantity = 200, UnitPrice = 18.5m, VatRate = 20m });
                quote.Lines.Add(new AlgowProforma.Models.QuoteLine { Code = "CER0054", Name = "Rakor Grubu 3/4\"", Description = "Çift taraflı · conta dahil", Unit = "takım", Quantity = 40, UnitPrice = 92m, VatRate = 20m });
                var outPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "algow-teklif-ornek.pdf");
                var quoteTheme = AlgowProforma.Models.PdfTheme.GetById(new AlgowProforma.Services.SettingsService().Load().QuoteThemeId);
                AlgowProforma.Services.QuotePdfService.Generate(quote, brand, outPath, quoteTheme);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "algow-teklif-HATA.txt"),
                    ex.ToString());
            }
            Shutdown(0);
            return;
        }

        // Global crash handler'lar — müşteri makinesinde silently ölmesin, log + MessageBox üretsin
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            LogCrash("AppDomain", ev.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, ev) =>
        {
            LogCrash("Dispatcher", ev.Exception);
            ShowCrashDialog(ev.Exception);
            ev.Handled = true; // graceful shutdown
            Shutdown(1);
        };

        TaskScheduler.UnobservedTaskException += (_, ev) =>
        {
            LogCrash("Task", ev.Exception);
            ev.SetObserved();
        };

        try
        {
            Settings.License = LicenseType.Community;
            RegisterFonts();
            Services.ThemeManager.Initialize(); // kayıtlı temayı (dark/light) uygula

            var tr = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = tr;
            Thread.CurrentThread.CurrentUICulture = tr;
            CultureInfo.DefaultThreadCurrentCulture = tr;
            CultureInfo.DefaultThreadCurrentUICulture = tr;

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            LogCrash("Startup", ex);
            ShowCrashDialog(ex);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Tüm kapak senaryolarını üreten showcase — Desktop\test kapakları altına PDF'ler bırakır.
    /// 4 alt klasör: design'ler (fotolu/fotosuz), temalar, toggle senaryoları.
    /// </summary>
    private static void RunCoverShowcase()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        RegisterFonts();

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var root = Path.Combine(desktop, "test kapakları");
        Directory.CreateDirectory(root);

        // Var olan akçay cover görselini bul (stress test'ten kalan); yoksa fotolu senaryolar foto'suz üretilir
        var coverImagePath = Path.Combine(Path.GetTempPath(), "ornek-covers", "cover-01.jpg");
        var hasCover = File.Exists(coverImagePath);

        var sb = new StringBuilder();
        sb.AppendLine($"=== Kapak Test Suite — {DateTime.Now:yyyy-MM-dd HH:mm} ===");
        sb.AppendLine($"Çıktı klasörü: {root}");
        sb.AppendLine($"Kapak görseli: {(hasCover ? coverImagePath : "YOK — fotolu senaryolar foto'suz üretilecek")}");
        sb.AppendLine();

        int produced = 0;
        int failed = 0;

        void Produce(string subFolder, string fileName, Action<AlgowProforma.Models.Catalog> setup)
        {
            try
            {
                var cat = AlgowProforma.Models.Catalog.CreateDefault();
                // 3 örnek ürün ekle (kapak sonrası içerik sayfası için)
                cat.Products.Add(new AlgowProforma.Models.Product { Name = "Topkapı Tam Geçişli Bilyalı Vana", Code = "ORN-V-001", Price = 449m, Currency = "TL" });
                cat.Products.Add(new AlgowProforma.Models.Product { Name = "Galata DN50 Küresel Vana", Code = "ORN-V-002", Price = 689m, Currency = "TL" });
                cat.Products.Add(new AlgowProforma.Models.Product { Name = "Boğaziçi Çek Valf 1/2\"", Code = "ORN-V-003", Price = 189m, Currency = "TL" });
                setup(cat);
                var dir = Path.Combine(root, subFolder);
                Directory.CreateDirectory(dir);
                new AlgowProforma.Services.PdfService().Generate(cat, Path.Combine(dir, $"{fileName}.pdf"));
                produced++;
                sb.AppendLine($"  OK  {subFolder}/{fileName}");
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"  FAIL  {subFolder}/{fileName} — {ex.Message}");
            }
        }

        // ========== 01-design-foto-yok ==========
        sb.AppendLine("[01] Design varyasyonları — kapak görseli YOK");
        foreach (var design in AlgowProforma.Models.PdfDesign.All)
            Produce("01-design-foto-yok", $"{design.Id}", c => c.DesignId = design.Id);

        // ========== 02-foto-modlar (3 mod × 3 design) ==========
        // NOT: Eski "02-design-fotolu" kaldırıldı — foto yüklendiğinde mod FullPage'de RenderCoverWithImageBackground
        // çağrılıyor, 10 tasarımdan bağımsız tek render. 02-design-fotolu klasörü ANLAMSIZ idi (10 PDF aynı çıkıyordu).
        // Onun yerine 02-foto-modlar 3 mod karşılaştırması yapıyor (gerçek çeşitlilik).

        // ========== 02b-foto-modlar (3 mod) ==========
        sb.AppendLine();
        sb.AppendLine("[02b] Foto modları — 3 mod × 3 design (Klasik, Sertifika, Minimalist)");
        if (hasCover)
        {
            foreach (var designId in new[] { "klasik", "sertifika", "minimalist" })
            {
                Produce("02b-foto-modlar", $"{designId}-FullPage", c =>
                {
                    c.DesignId = designId;
                    c.Cover.CustomCoverImagePath = coverImagePath;
                    c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.FullPage;
                });
                Produce("02b-foto-modlar", $"{designId}-UstYari", c =>
                {
                    c.DesignId = designId;
                    c.Cover.CustomCoverImagePath = coverImagePath;
                    c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.TopWithBrandBar;
                });
                Produce("02b-foto-modlar", $"{designId}-SagYari", c =>
                {
                    c.DesignId = designId;
                    c.Cover.CustomCoverImagePath = coverImagePath;
                    c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.BackgroundWithOverlay;
                });
            }
        }
        else
        {
            sb.AppendLine("  -- atlanıyor: kapak görseli bulunamadı");
        }

        // ========== 05-cesitli-fotolar ==========
        sb.AppendLine();
        sb.AppendLine("[05] Çeşitli fotoğraflar × 3 mod (farklı aspect ratio'lar)");
        var sampleDir = Path.Combine(Path.GetTempPath(), "ornek-urun-foto");
        var variedPhotos = new[]
        {
            ("landscape-parfum", coverImagePath),
            ("portrait-bidon-1000ml", Path.Combine(sampleDir, "1000ml-limon-bidon.jpg")),
            ("square-kutu-250ml", Path.Combine(sampleDir, "250ml-deep-aqua-cam-kutulu.jpg")),
            ("portrait-pet-400ml", Path.Combine(sampleDir, "400ml-incir-cicegi-pet.jpg")),
        };
        foreach (var (label, path) in variedPhotos)
        {
            if (!File.Exists(path)) { sb.AppendLine($"  -- atlanıyor: {label} dosya yok"); continue; }
            Produce("05-cesitli-fotolar", $"{label}-FullPage", c =>
            {
                c.Cover.CustomCoverImagePath = path;
                c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.FullPage;
            });
            Produce("05-cesitli-fotolar", $"{label}-UstYari", c =>
            {
                c.Cover.CustomCoverImagePath = path;
                c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.TopWithBrandBar;
            });
            Produce("05-cesitli-fotolar", $"{label}-SagYari", c =>
            {
                c.Cover.CustomCoverImagePath = path;
                c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.BackgroundWithOverlay;
            });
        }

        // ========== 06-freeform-ornekler (Kapak Tasarım Stüdyosu) ==========
        sb.AppendLine();
        sb.AppendLine("[06] Free-form Kapak Tasarım Stüdyosu — element kombinasyonları + farklı boyutlar");

        // Sample 1: Foto sol yarı + sağ başlık + tagline + footer çizgi
        Produce("06-freeform-ornekler", "01-foto-sol-baslik-sag", c =>
        {
            if (File.Exists(coverImagePath))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 0, YMm = 0, WidthMm = 100, HeightMm = 297, Content = coverImagePath, ZIndex = 1
                });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 110, YMm = 80, WidthMm = 90, HeightMm = 50, Content = "ÖRNEK MARKA",
                FontSize = 28, Bold = true, ForegroundHex = "#2E338F", ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 110, YMm = 135, WidthMm = 90, HeightMm = 20, Content = "Kalite ve Hassasiyet",
                FontSize = 12, Italic = true, ForegroundHex = "#666666", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Line,
                XMm = 110, YMm = 160, WidthMm = 60, HeightMm = 1, BackgroundHex = "#2E338F", ZIndex = 4
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 110, YMm = 170, WidthMm = 90, HeightMm = 15, Content = "ÜRÜN KATALOĞU 2026",
                FontSize = 10, Bold = true, ForegroundHex = "#999999", ZIndex = 5
            });
        });

        // Sample 2: Foto tüm sayfa arka plan + bronz şeffaf overlay kutu + başlık üst
        Produce("06-freeform-ornekler", "02-foto-arkaplan-bronz-overlay", c =>
        {
            if (File.Exists(coverImagePath))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 0, YMm = 0, WidthMm = 210, HeightMm = 297, Content = coverImagePath, ZIndex = 1
                });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Rectangle,
                XMm = 20, YMm = 90, WidthMm = 170, HeightMm = 110,
                BackgroundHex = "#2E338F", Opacity = 0.85, ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 25, YMm = 110, WidthMm = 160, HeightMm = 40, Content = "ÖRNEK MARKA VANA",
                FontSize = 26, Bold = true, ForegroundHex = "#FFFFFF", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 25, YMm = 155, WidthMm = 160, HeightMm = 15, Content = "Kalite ve Hassasiyet",
                FontSize = 13, Italic = true, ForegroundHex = "#FFFFFF", ZIndex = 4
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 25, YMm = 180, WidthMm = 160, HeightMm = 15, Content = "Ürün Kataloğu · 2026",
                FontSize = 11, ForegroundHex = "#FFFFFF", ZIndex = 5
            });
        });

        // Sample 3: 3 foto grid + tek başlık (collage tarzı)
        Produce("06-freeform-ornekler", "03-foto-grid-3lu", c =>
        {
            var photo1 = Path.Combine(sampleDir, "1000ml-limon-bidon.jpg");
            var photo2 = Path.Combine(sampleDir, "250ml-deep-aqua-cam-kutulu.jpg");
            var photo3 = Path.Combine(sampleDir, "400ml-incir-cicegi-pet.jpg");

            if (File.Exists(photo1))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 15, YMm = 50, WidthMm = 90, HeightMm = 100, Content = photo1, ZIndex = 1
                });
            if (File.Exists(photo2))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 110, YMm = 50, WidthMm = 85, HeightMm = 60, Content = photo2, ZIndex = 1
                });
            if (File.Exists(photo3))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 110, YMm = 120, WidthMm = 85, HeightMm = 70, Content = photo3, ZIndex = 1
                });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Rectangle,
                XMm = 0, YMm = 0, WidthMm = 210, HeightMm = 40, BackgroundHex = "#2E338F", ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 0, YMm = 8, WidthMm = 210, HeightMm = 25, Content = "ÜRÜN GALERİSİ",
                FontSize = 22, Bold = true, ForegroundHex = "#FFFFFF", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Rectangle,
                XMm = 0, YMm = 240, WidthMm = 210, HeightMm = 57, BackgroundHex = "#1A1A1F", ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 20, YMm = 250, WidthMm = 170, HeightMm = 20, Content = "ÖRNEK MARKA VANA LTD. ŞTİ.",
                FontSize = 14, Bold = true, ForegroundHex = "#FFFFFF", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 20, YMm = 275, WidthMm = 170, HeightMm = 15, Content = "www.ornekfirma.com · +90 318 000 00 00",
                FontSize = 9, ForegroundHex = "#2E338F", ZIndex = 3
            });
        });

        // Sample 4: Minimalist — tek küçük foto + büyük tipografi + bronz aksan
        Produce("06-freeform-ornekler", "04-minimalist-tek-foto", c =>
        {
            if (File.Exists(coverImagePath))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 75, YMm = 50, WidthMm = 60, HeightMm = 60, Content = coverImagePath, ZIndex = 1
                });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 20, YMm = 130, WidthMm = 170, HeightMm = 30, Content = "ÖRNEK MARKA",
                FontSize = 36, Bold = true, ForegroundHex = "#1A1A1F", ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Line,
                XMm = 90, YMm = 168, WidthMm = 30, HeightMm = 2, BackgroundHex = "#2E338F", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 20, YMm = 180, WidthMm = 170, HeightMm = 15, Content = "Kalite ve Hassasiyet",
                FontSize = 13, Italic = true, ForegroundHex = "#666666", ZIndex = 3
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 20, YMm = 270, WidthMm = 170, HeightMm = 12, Content = "ÜRÜN KATALOĞU · 2026",
                FontSize = 10, Bold = true, ForegroundHex = "#2E338F", ZIndex = 3
            });
        });

        // Sample 5: Farklı boyutlar — büyük foto + küçük başlık
        Produce("06-freeform-ornekler", "05-buyuk-foto-kucuk-baslik", c =>
        {
            if (File.Exists(coverImagePath))
                c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
                {
                    Type = AlgowProforma.Models.CoverElementType.Image,
                    XMm = 10, YMm = 10, WidthMm = 190, HeightMm = 230, Content = coverImagePath, ZIndex = 1
                });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.Title,
                XMm = 20, YMm = 250, WidthMm = 170, HeightMm = 20, Content = "ÖRNEK MARKA VANA",
                FontSize = 18, Bold = true, ForegroundHex = "#1A1A1F", ZIndex = 2
            });
            c.Cover.Elements.Add(new AlgowProforma.Models.CoverElement
            {
                Type = AlgowProforma.Models.CoverElementType.TextBox,
                XMm = 20, YMm = 275, WidthMm = 170, HeightMm = 12, Content = "2026 · Ürün Kataloğu",
                FontSize = 10, ForegroundHex = "#2E338F", ZIndex = 3
            });
        });

        // ========== 03-tema ==========
        sb.AppendLine();
        sb.AppendLine("[03] Tema varyasyonları — Klasik design × 23 tema");
        foreach (var theme in AlgowProforma.Models.PdfTheme.All)
            Produce("03-tema", $"{theme.Id}", c =>
            {
                c.DesignId = "klasik";
                c.ThemeId = theme.Id;
            });

        // ========== 04-toggle-senaryolar ==========
        sb.AppendLine();
        sb.AppendLine("[04] Toggle ve özel field senaryoları");

        Produce("04-toggle-senaryolar", "tum-elementler-acik", c => { /* default */ });

        Produce("04-toggle-senaryolar", "about-kapali", c => c.Cover.ShowAbout = false);
        Produce("04-toggle-senaryolar", "features-kapali", c => c.Cover.ShowFeatures = false);
        Produce("04-toggle-senaryolar", "contact-kapali", c => c.Cover.ShowContactBar = false);
        Produce("04-toggle-senaryolar", "hepsi-kapali", c =>
        {
            c.Cover.ShowAbout = false;
            c.Cover.ShowFeatures = false;
            c.Cover.ShowContactBar = false;
        });

        Produce("04-toggle-senaryolar", "watermark-TASLAK", c => c.Brand.WatermarkText = "TASLAK");
        Produce("04-toggle-senaryolar", "watermark-ORNEKTIR", c => c.Brand.WatermarkText = "ÖRNEKTİR");
        Produce("04-toggle-senaryolar", "watermark-ORN-2026", c => c.Brand.WatermarkText = "ÖRNEK MARKA 2026");

        Produce("04-toggle-senaryolar", "tarih-Mayis-2026", c => c.Cover.DateText = "Mayıs 2026");
        Produce("04-toggle-senaryolar", "tarih-Q2-Edisyonu", c => c.Cover.DateText = "Q2 2026 Edisyonu");
        Produce("04-toggle-senaryolar", "tarih-uzun-kasim", c => c.Cover.DateText = "Kasım 2026 Yıllık Kataloğu");

        Produce("04-toggle-senaryolar", "logo-yok", c => c.Brand.LogoPath = "");
        Produce("04-toggle-senaryolar", "ana-baslik-ozel", c => c.Cover.MainTitle = "ÖZEL ÜRÜN HATTI");
        Produce("04-toggle-senaryolar", "section-label-ozel", c => c.Cover.SectionLabel = "VANA SEÇIM KILAVUZU");

        sb.AppendLine();
        sb.AppendLine($"=== Üretilen: {produced}, Hatalı: {failed} ===");

        File.WriteAllText(Path.Combine(root, "_RAPOR.txt"), sb.ToString());
    }

    private static void RunStressTest()
    {
        var manifest = File.ReadAllLines(Path.Combine(Path.GetTempPath(), "ornek-stress-photos.txt"));
        string root = ""; string? cover = null;
        var photos = new System.Collections.Generic.List<string>();
        foreach (var l in manifest)
        {
            if (l.StartsWith("ROOT=")) root = l.Substring(5).Trim();
            else if (l.StartsWith("COVER=")) cover = l.Substring(6).Trim();
            else if (l.StartsWith("PRODUCT=")) photos.Add(l.Substring(8).Trim());
        }
        if (string.IsNullOrEmpty(root)) return;

        Settings.License = LicenseType.Community;
        RegisterFonts();

        var results = new System.Collections.Generic.List<(string folder, string name, bool ok, long size, long ms, string? error)>();

        void Run(string folder, string name, Action<AlgowProforma.Models.Catalog> setup, Func<AlgowProforma.Models.Catalog>? makeCat = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var cat = makeCat?.Invoke() ?? AlgowProforma.Models.Catalog.CreateDefault();
                setup(cat);
                // Her senaryoda cover image kullan — setup explicit "" set etmediyse default cover ekle.
                // "01-cover/01-empty-default-design" gibi senaryolarda setup cover'a dokunmaz → default eklenir (zenginlik için).
                if (cover != null && string.IsNullOrEmpty(cat.Cover.CustomCoverImagePath) && !folder.StartsWith("01-cover") && !folder.StartsWith("04-edges") && !folder.StartsWith("08-designs"))
                    cat.Cover.CustomCoverImagePath = cover;
                var path = Path.Combine(root, folder, $"{name}.pdf");
                new AlgowProforma.Services.PdfService().Generate(cat, path);
                sw.Stop();
                var size = File.Exists(path) ? new FileInfo(path).Length : 0;
                results.Add((folder, name, size > 1000, size, sw.ElapsedMilliseconds, null));
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add((folder, name, false, 0, sw.ElapsedMilliseconds, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        void RunSingle(string folder, string name, Func<(AlgowProforma.Models.Catalog cat, AlgowProforma.Models.Product prod)> setup)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var (cat, prod) = setup();
                var path = Path.Combine(root, folder, $"{name}.pdf");
                new AlgowProforma.Services.PdfService().GenerateSingleProduct(cat, prod, path);
                sw.Stop();
                var size = File.Exists(path) ? new FileInfo(path).Length : 0;
                results.Add((folder, name, size > 1000, size, sw.ElapsedMilliseconds, null));
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add((folder, name, false, 0, sw.ElapsedMilliseconds, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        // Gerçekçi vana ürün havuzu — her senaryoda kullanılır (slogan/firma/logo zaten Catalog.CreateDefault'tan dolu)
        var richProducts = new (string Name, string Code, decimal Price, bool HasTable, string TableTitle, string Spec1L, string Spec1V, string Spec2L, string Spec2V, string Spec3L, string Spec3V, string Spec4L, string Spec4V)[]
        {
            ("Topkapı Tam Geçişli Bilyalı Vana — Pirinç",       "ORN-V-001",  449m, false, "", "", "", "", "", "", "", "", ""),
            ("Galata Kelebek Vana DN50 — Bronz Gövde",           "ORN-V-002", 1299m, true,  "Galata DN50 Teknik Bilgiler", "Bağlantı Çapı", "DN50", "Çalışma Basıncı", "16 bar", "Sıcaklık", "-10°C / +120°C", "Gövde", "Bronz"),
            ("Boğaziçi Çek Valf 1/2\"",                          "ORN-V-003",  189m, false, "", "", "", "", "", "", "", "", ""),
            ("Anadolu Küresel Vana — NBR Conta",                 "ORN-V-004",  349m, false, "", "", "", "", "", "", "", "", ""),
            ("İstanbul Diyaframlı Vana — Endüstriyel",           "ORN-V-005",  899m, true,  "İstanbul Diyafram Spec", "Bağlantı Çapı", "DN80", "Çalışma Basıncı", "10 bar", "Akış Debisi", "120 L/dak", "Sertifika", "CE / TSE"),
            ("Kadıköy Mini Vana 15mm Pirinç",                    "ORN-V-006",   89m, false, "", "", "", "", "", "", "", "", ""),
            ("Beşiktaş Sürgülü Vana DN80 — Heavy Duty",          "ORN-V-007", 2499m, true,  "Beşiktaş Sürgülü", "Bağlantı Çapı", "DN80", "Çalışma Basıncı", "40 bar", "Sıcaklık", "-20°C / +180°C", "Gövde", "Paslanmaz Çelik"),
            ("Üsküdar Pislik Tutucu Y-Tip 1\"",                  "ORN-V-008",  599m, false, "", "", "", "", "", "", "", "", ""),
            ("Kırıkkale Pirinç Bilyalı Vana 3/4\"",              "ORN-V-009",  249m, false, "", "", "", "", "", "", "", "", ""),
            ("Keskin Tek Yönlü Çek Valf — Hassas",               "ORN-V-010",  379m, false, "", "", "", "", "", "", "", "", ""),
            ("Çelik Sphereworks Vana DN100 — Endüstriyel",       "ORN-V-011", 1890m, true,  "Sphereworks DN100", "Bağlantı Çapı", "DN100", "Çalışma Basıncı", "25 bar", "Akış", "350 L/dak", "Gövde", "Paslanmaz 316L"),
            ("Bronz Selenoid Vana 230V AC",                       "ORN-V-012", 1450m, false, "", "", "", "", "", "", "", "", ""),
            ("Eco Plastik Vana 16mm — Ekonomik",                 "ORN-V-013",   49m, false, "", "", "", "", "", "", "", "", ""),
            ("Pro Boru Tutucu — Çelik",                          "ORN-V-014",  159m, false, "", "", "", "", "", "", "", "", ""),
            ("Akış Kontrolü Vanası — Otomatik",                  "ORN-V-015",  879m, false, "", "", "", "", "", "", "", "", ""),
            ("Yüksek Basınç Vanası DN200 — Endüstriyel",         "ORN-V-016", 3499m, true,  "Yüksek Basınç DN200", "Bağlantı Çapı", "DN200", "Çalışma Basıncı", "60 bar", "Sıcaklık", "+200°C", "Gövde", "Karbon Çelik"),
        };

        void Add(AlgowProforma.Models.Catalog cat, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var d = richProducts[i % richProducts.Length];
                var nameSuffix = i >= richProducts.Length ? $" v.{i / richProducts.Length + 1}" : "";
                var codeSuffix = i >= richProducts.Length ? $"-{i + 1:000}" : "";
                var p = new AlgowProforma.Models.Product
                {
                    Name = d.Name + nameSuffix,
                    Code = d.Code + codeSuffix,
                    Price = d.Price,
                    Currency = "TL",
                    ImagePath = photos.Count > 0 ? photos[i % photos.Count] : ""
                };
                // HasTable sadece az ürünlü senaryolarda (multi-grid'te layout exception riski) + her zaman tek-ürünlü layout güvenli
                if (d.HasTable && n <= 3)
                {
                    p.HasTable = true;
                    p.Table = new AlgowProforma.Models.ProductTable
                    {
                        Title = d.TableTitle,
                        Specs =
                        {
                            new AlgowProforma.Models.ProductSpec { Label = d.Spec1L, Value = d.Spec1V },
                            new AlgowProforma.Models.ProductSpec { Label = d.Spec2L, Value = d.Spec2V },
                            new AlgowProforma.Models.ProductSpec { Label = d.Spec3L, Value = d.Spec3V },
                            new AlgowProforma.Models.ProductSpec { Label = d.Spec4L, Value = d.Spec4V },
                        }
                    };
                }
                cat.Products.Add(p);
            }
        }

        // === 01-cover (10) ===
        Run("01-cover", "01-empty-default-design", c => { Add(c, 3); });
        Run("01-cover", "02-fullpage-landscape", c => { if (cover != null) { c.Cover.CustomCoverImagePath = cover; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.FullPage; } Add(c, 3); });
        Run("01-cover", "03-topbar-mode", c => { if (cover != null) { c.Cover.CustomCoverImagePath = cover; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.TopWithBrandBar; } Add(c, 3); });
        Run("01-cover", "04-overlay-mode", c => { if (cover != null) { c.Cover.CustomCoverImagePath = cover; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.BackgroundWithOverlay; } Add(c, 3); });
        Run("01-cover", "05-no-about", c => { c.Cover.ShowAbout = false; Add(c, 3); });
        Run("01-cover", "06-no-features", c => { c.Cover.ShowFeatures = false; Add(c, 3); });
        Run("01-cover", "07-no-contact", c => { c.Cover.ShowContactBar = false; Add(c, 3); });
        Run("01-cover", "08-all-cover-hidden", c => { c.Cover.ShowAbout = false; c.Cover.ShowFeatures = false; c.Cover.ShowContactBar = false; Add(c, 3); });
        Run("01-cover", "09-long-main-title", c => { c.Cover.MainTitle = "ÇOK UZUN BİR KATALOG BAŞLIĞI BURAYA YAZILMIŞ TEST İÇİN"; Add(c, 3); });
        Run("01-cover", "10-custom-section-label", c => { c.Cover.SectionLabel = "ÖZEL KATALOG 2026"; Add(c, 3); });

        // === 02-layouts (12) ===
        var layoutCases = new[] { ("1x1", 1), ("1x1", 5), ("1x2", 6), ("2x2", 4), ("2x2", 8), ("2x3", 6), ("2x3", 12), ("3x3", 9), ("3x3", 18), ("4x3", 24), ("4x4", 16), ("4x4", 32) };
        foreach (var (lid, n) in layoutCases) { var l = lid; var nn = n; Run("02-layouts", $"layout-{l}-products-{nn:000}", c => { c.LayoutId = l; Add(c, nn); }); }

        // === 03-themes (23) ===
        foreach (var pt in AlgowProforma.Models.PdfTheme.All) { var tt = pt.Id; Run("03-themes", $"theme-{tt}", c => { c.ThemeId = tt; Add(c, 6); }); }

        // === 04-edges (11) ===
        Run("04-edges", "01-empty-everything", c => { }, () => new AlgowProforma.Models.Catalog());
        Run("04-edges", "02-turkish-chars", c => { c.Brand.Name = "ŞİĞÖÇÜ Şirketi"; c.Brand.About = "Çağdaş üretim, ölçülü işçilik, ığdırlı kalite"; c.Products.Add(new AlgowProforma.Models.Product { Name = "Türkçe ığİ Ürün", Code = "T-Ş-1", Price = 99.99m, Currency = "TL", ImagePath = photos[0] }); });
        Run("04-edges", "03-very-long-brand", c => { c.Brand.Name = new string('A', 300); Add(c, 3); });
        Run("04-edges", "04-very-long-about-5000", c => { c.Brand.About = string.Join(" ", System.Linq.Enumerable.Range(1, 700).Select(i => "kelime")); Add(c, 3); });
        Run("04-edges", "05-broken-logo", c => { c.Brand.LogoPath = "C:\\NOPE\\fake.png"; Add(c, 3); });
        Run("04-edges", "06-negative-price", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = "Neg", Code = "N1", Price = -100m, Currency = "TL", ImagePath = photos[0] }); });
        Run("04-edges", "07-zero-price", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = "Zero", Code = "Z1", Price = 0m, Currency = "TL", ImagePath = photos[0] }); });
        Run("04-edges", "08-very-low-price", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = "Mikro", Code = "M1", Price = 0.001m, Currency = "TL", ImagePath = photos[0] }); });
        Run("04-edges", "09-EUR-currency", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = "Euro", Code = "E1", Price = 50m, Currency = "EUR", ImagePath = photos[0] }); });
        Run("04-edges", "10-empty-name", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = "", Code = "EMPTY", Price = 100m, Currency = "TL", ImagePath = photos[0] }); });
        Run("04-edges", "11-very-long-product-name", c => { c.Products.Add(new AlgowProforma.Models.Product { Name = new string('X', 200), Code = "L1", Price = 100m, Currency = "TL", ImagePath = photos[0] }); });

        // === 05-real-world (5) ===
        Run("05-real-world", "01-sample-12-default-layout", c => { if (cover != null) c.Cover.CustomCoverImagePath = cover; c.LayoutId = "2x3"; Add(c, 12); });
        Run("05-real-world", "02-sample-12-custom-pages", c =>
        {
            if (cover != null) c.Cover.CustomCoverImagePath = cover;
            Add(c, 12);
            c.UseCustomPageLayouts = true;
            c.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "1x1", ProductIds = { c.Products[0].Id } });
            c.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "1x2", ProductIds = { c.Products[1].Id, c.Products[2].Id } });
            c.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "2x2", ProductIds = { c.Products[3].Id, c.Products[4].Id, c.Products[5].Id, c.Products[6].Id } });
            c.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "3x3", ProductIds = { c.Products[7].Id, c.Products[8].Id, c.Products[9].Id, c.Products[10].Id, c.Products[11].Id } });
        });
        Run("05-real-world", "03-references-10", c => { for (int i = 1; i <= 10; i++) c.References.Add(new AlgowProforma.Models.Reference { Name = $"Müşteri Firma {i}" }); Add(c, 6); });
        Run("05-real-world", "05-no-references-page", c => { c.SkipReferencesPage = true; for (int i = 1; i <= 10; i++) c.References.Add(new AlgowProforma.Models.Reference { Name = $"M{i}" }); Add(c, 6); });

        // === 06-stress (4) ===
        Run("06-stress", "01-products-100", c => { c.LayoutId = "3x3"; Add(c, 100); });
        Run("06-stress", "02-products-500", c => { c.LayoutId = "4x4"; Add(c, 500); });
        Run("06-stress", "03-references-200", c => { for (int i = 1; i <= 200; i++) c.References.Add(new AlgowProforma.Models.Reference { Name = $"Ref {i}" }); Add(c, 6); });
        Run("06-stress", "04-mixed-50-products-50-refs", c => { for (int i = 1; i <= 50; i++) c.References.Add(new AlgowProforma.Models.Reference { Name = $"Ref {i}" }); Add(c, 50); });

        // === 07-singleproduct (3) ===
        RunSingle("07-singleproduct", "01-with-image", () => { var cat = AlgowProforma.Models.Catalog.CreateDefault(); var p = new AlgowProforma.Models.Product { Name = "Tek Ürün", Code = "S1", Price = 999m, Currency = "TL", ImagePath = photos[0] }; cat.Products.Add(p); return (cat, p); });
        RunSingle("07-singleproduct", "02-no-image", () => { var cat = AlgowProforma.Models.Catalog.CreateDefault(); var p = new AlgowProforma.Models.Product { Name = "Görselsiz", Code = "S2", Price = 199m, Currency = "TL" }; cat.Products.Add(p); return (cat, p); });
        RunSingle("07-singleproduct", "03-with-table", () =>
        {
            var cat = AlgowProforma.Models.Catalog.CreateDefault();
            var p = new AlgowProforma.Models.Product
            {
                Name = "Tablolu Ürün", Code = "S3", Price = 1499m, Currency = "TL", ImagePath = photos[0],
                HasTable = true,
                Table = new AlgowProforma.Models.ProductTable { Title = "Teknik", Specs = { new AlgowProforma.Models.ProductSpec { Label = "Çap", Value = "32mm" }, new AlgowProforma.Models.ProductSpec { Label = "Basınç", Value = "16 bar" } } }
            };
            cat.Products.Add(p);
            return (cat, p);
        });

        // === 08-designs (8) ===
        foreach (var d in AlgowProforma.Models.PdfDesign.All) { var did = d.Id; Run("08-designs", $"design-{did}", c => { c.DesignId = did; Add(c, 3); }); }

        // === Report ===
        var reportPath = Path.Combine(root, "_STRESS_REPORT.txt");
        var sb = new StringBuilder();
        sb.AppendLine($"=== STRESS TEST REPORT ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
        sb.AppendLine($"Total: {results.Count}");
        sb.AppendLine($"Pass:  {results.Count(r => r.ok)} ({(100.0 * results.Count(r => r.ok) / results.Count):F1}%)");
        sb.AppendLine($"Fail:  {results.Count(r => !r.ok)}");
        sb.AppendLine();
        foreach (var grp in results.GroupBy(r => r.folder))
        {
            sb.AppendLine($"--- {grp.Key} ---");
            foreach (var r in grp)
            {
                var s = r.ok ? "OK " : "FAIL";
                sb.AppendLine($"  {s} {r.name,-50} {r.size,12:N0} bytes ({r.ms,5} ms)" + (r.error != null ? $"  -> {r.error}" : ""));
            }
            sb.AppendLine();
        }
        if (results.Any(r => !r.ok))
        {
            sb.AppendLine("=== FAILURES ===");
            foreach (var r in results.Where(r => !r.ok)) sb.AppendLine($"  {r.folder}/{r.name}: {r.error}");
        }
        File.WriteAllText(reportPath, sb.ToString());
    }

    private static void RunShowcase()
    {
        var logFile = Path.Combine(Path.GetTempPath(), "ornek-showcase-result.txt");
        try
        {
            Settings.License = LicenseType.Community;
            RegisterFonts();

            var manifest = File.ReadAllLines(Path.Combine(Path.GetTempPath(), "ornek-showcase-photos.txt"));
            string? cover = null;
            var photos = new System.Collections.Generic.List<string>();
            foreach (var line in manifest)
            {
                if (line.StartsWith("COVER=")) cover = line.Substring(6).Trim();
                else if (line.StartsWith("PRODUCT=")) photos.Add(line.Substring(8).Trim());
            }

            var cat = AlgowProforma.Models.Catalog.CreateDefault();
            if (cover != null && File.Exists(cover))
                cat.Cover.CustomCoverImagePath = cover;
            cat.Cover.MainTitle = "ÖRNEK MARKA";
            cat.Cover.Subtitle = "Ürün Kataloğu";
            cat.Cover.YearText = "2026";

            // NOT: IsFeatured + multi-grid layout (2x2, 3x3) QuestPDF layout conflict yaratıyor (FeaturedCard ColumnSpan(2) overflow).
            // Featured ürünler sadece tek-ürün sayfada (1x1) güvenli. Showcase için hepsi false.
            var productData = new (string Name, string Code, decimal Price, bool Featured)[]
            {
                ("Premium Vana A — Tam Geçişli",    "ORN-001", 1299m, false),
                ("Standart Vana B — Ekonomik",      "ORN-002",  599m, false),
                ("Pro Vana C — Yüksek Basınç",      "ORN-003", 1899m, false),
                ("Industrial D — Endüstriyel",       "ORN-004", 2499m, false),
                ("Compact E — Kompakt Tip",          "ORN-005",  349m, false),
                ("Heavy Duty F — Ağır Hizmet",       "ORN-006", 3299m, false),
                ("Mini G — Mini Hat",                "ORN-007",  199m, false),
                ("Bronze H — Bronz Gövde",           "ORN-008",  899m, false),
                ("Çelik I — Paslanmaz Çelik",        "ORN-009", 1099m, false),
                ("Bakır J — Bakır Kombi Vanası",     "ORN-010", 1599m, false),
                ("Hibrid K — Hibrid Sistem",         "ORN-011", 2199m, false),
                ("Eco L — Eco Serisi",               "ORN-012",  449m, false),
            };

            for (int i = 0; i < productData.Length && i < photos.Count; i++)
            {
                var (name, code, price, featured) = productData[i];
                cat.Products.Add(new AlgowProforma.Models.Product
                {
                    Name = name, Code = code, Price = price, Currency = "TL",
                    ImagePath = photos[i], IsFeatured = featured
                });
            }

            // Custom pages — her sayfa farklı layout (1, 2, 4, 5 ürün)
            cat.UseCustomPageLayouts = true;
            var prods = cat.Products;
            cat.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "1x1", ProductIds = { prods[2].Id } });
            cat.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "1x2", ProductIds = { prods[0].Id, prods[1].Id } });
            cat.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "2x2", ProductIds = { prods[3].Id, prods[4].Id, prods[5].Id, prods[6].Id } });
            cat.CustomPages.Add(new AlgowProforma.Models.CustomPageEntry { LayoutId = "3x3", ProductIds = { prods[7].Id, prods[8].Id, prods[9].Id, prods[10].Id, prods[11].Id } });

            var output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ornek-showcase.pdf");
            new AlgowProforma.Services.PdfService().Generate(cat, output);

            File.WriteAllText(logFile, $"OK\nOutput: {output}\nSize: {new FileInfo(output).Length} bytes\nProducts: {cat.Products.Count}\nPages: {cat.CustomPages.Count} custom + 1 cover\n");
        }
        catch (Exception ex)
        {
            File.WriteAllText(logFile, $"FAIL: {ex.GetType().FullName}\n{ex.Message}\n{ex.StackTrace}\n");
        }
    }

    // Debug test runner — production'da çağrılmaz (--test-all arg gerekli, çıkartıldı OnStartup'tan).
    // İleride dev/QA için tekrar etkinleştirilebilir.
    private static void RunComprehensiveTests()
    {
        var resultFile = Path.Combine(Path.GetTempPath(), "ornek-comprehensive-test.txt");
        var outDir = Path.Combine(Path.GetTempPath(), "ornek-test-pdfs");
        Directory.CreateDirectory(outDir);
        try { File.Delete(resultFile); } catch { }

        Settings.License = LicenseType.Community;
        RegisterFonts();

        int passed = 0, failed = 0;
        var failures = new System.Collections.Generic.List<string>();

        void Test(string name, Action<AlgowProforma.Models.Catalog> setup)
        {
            try
            {
                var cat = AlgowProforma.Models.Catalog.CreateDefault();
                setup(cat);
                var path = Path.Combine(outDir, $"{name.Replace(" ", "_").Replace("/", "_")}.pdf");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                new AlgowProforma.Services.PdfService().Generate(cat, path);
                sw.Stop();
                var size = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (size > 1000)
                {
                    passed++;
                    File.AppendAllText(resultFile, $"✓ {name,-55} {size,10:N0} bytes ({sw.ElapsedMilliseconds} ms)\n");
                }
                else
                {
                    failed++;
                    var msg = $"⚠ {name,-55} EMPTY OUTPUT ({size} bytes)";
                    File.AppendAllText(resultFile, msg + "\n");
                    failures.Add(msg);
                }
            }
            catch (Exception ex)
            {
                failed++;
                var msg = $"✗ {name,-55} {ex.GetType().Name}: {ex.Message}";
                File.AppendAllText(resultFile, msg + "\n");
                failures.Add(msg);
            }
        }

        File.AppendAllText(resultFile, $"=== KAPSAMLI TEST SUITE ({DateTime.Now:HH:mm:ss}) ===\n\n");
        File.AppendAllText(resultFile, "[P0] Kritik kullanım senaryoları\n");

        Test("P0-01 EmptyCatalog", c => { });
        Test("P0-02 CreateDefault (Örnek)", c => { });
        Test("P0-03 1 product", c => c.Products.Add(MakeProduct("Test 1", "T1", 10)));
        Test("P0-04 5 products", c => { for (int i = 1; i <= 5; i++) c.Products.Add(MakeProduct($"Ürün {i}", $"P{i:00}", 100 * i)); });
        Test("P0-05 20 products", c => { for (int i = 1; i <= 20; i++) c.Products.Add(MakeProduct($"Ürün {i}", $"P{i:00}", 50 * i)); });
        Test("P0-06 9 products (Standart 3x3)", c => { c.LayoutId = "3x3"; for (int i = 1; i <= 9; i++) c.Products.Add(MakeProduct($"Ürün {i}", $"P{i:00}", 100)); });
        Test("P0-07 1 product with image", c => {
            var prod = MakeProduct("Logo Ürün", "L1", 100);
            prod.ImagePath = c.Brand.LogoPath;
            c.Products.Add(prod);
        });
        Test("P0-08 1 featured product", c => {
            var prod = MakeProduct("Featured", "F1", 999);
            prod.IsFeatured = true;
            c.Products.Add(prod);
        });
        Test("P0-09 product with table", c => {
            var prod = MakeProduct("Tablolu", "TB1", 500);
            prod.HasTable = true;
            prod.Table = new AlgowProforma.Models.ProductTable
            {
                Title = "Teknik Özellikler",
                Specs = { new AlgowProforma.Models.ProductSpec { Label = "Güç", Value = "2000W" } }
            };
            c.Products.Add(prod);
        });

        File.AppendAllText(resultFile, "\n[P1] Layout varyasyonları\n");
        var layoutIds = new[] { "1x1", "1x2", "2x1", "2x2", "2x3", "2x4", "3x3", "3x4", "4x3", "4x4" };
        foreach (var lid in layoutIds)
        {
            var l = lid;
            Test($"P1 Layout={l} (10 product)", c => {
                c.LayoutId = l;
                for (int i = 1; i <= 10; i++) c.Products.Add(MakeProduct($"Ürün {i}", $"P{i:00}", 50));
            });
        }

        File.AppendAllText(resultFile, "\n[P2] Tema varyasyonları\n");
        var themeIds = new[] { "klasik", "klasik", "petrol", "bordo", "yesil", "antrasit", "bakir", "siyah", "okyanus", "sampanya" };
        foreach (var tid in themeIds)
        {
            var t = tid;
            Test($"P2 Theme={t}", c => {
                c.ThemeId = t;
                c.Products.Add(MakeProduct("Tema Test", "TT", 100));
            });
        }

        File.AppendAllText(resultFile, "\n[P2b] Cover varyasyonları\n");
        Test("P2b ShowAbout=false", c => { c.Cover.ShowAbout = false; });
        Test("P2b ShowFeatures=false", c => { c.Cover.ShowFeatures = false; });
        Test("P2b ShowContactBar=false", c => { c.Cover.ShowContactBar = false; });
        Test("P2b All cover sections off", c => { c.Cover.ShowAbout = false; c.Cover.ShowFeatures = false; c.Cover.ShowContactBar = false; });
        Test("P2b CustomCoverImagePath set", c => { c.Cover.CustomCoverImagePath = c.Brand.LogoPath; });
        Test("P2b CoverMode=FullPage", c => { c.Cover.CustomCoverImagePath = c.Brand.LogoPath; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.FullPage; });
        Test("P2b CoverMode=TopWithBrandBar", c => { c.Cover.CustomCoverImagePath = c.Brand.LogoPath; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.TopWithBrandBar; });
        Test("P2b CoverMode=BackgroundWithOverlay", c => { c.Cover.CustomCoverImagePath = c.Brand.LogoPath; c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.BackgroundWithOverlay; });

        File.AppendAllText(resultFile, "\n[P3] Edge case'ler\n");
        Test("P3 Empty brand all blank", c => {
            c.Brand = new AlgowProforma.Models.BrandInfo();
        });
        Test("P3 Brand only LogoPath set", c => {
            c.Brand = new AlgowProforma.Models.BrandInfo { LogoPath = c.Brand.LogoPath };
        });
        Test("P3 Turkish chars in everything", c => {
            c.Brand.Name = "ŞİĞÖÇÜ Şirketi";
            c.Brand.About = "Çağdaş üretim, ölçülü yaklaşım, ığdırlı işçilik";
            c.Products.Add(MakeProduct("Türkçe Ürün ığİ", "T-Ş-1", 99.99m));
        });
        Test("P3 Very long brand name (300 char)", c => {
            c.Brand.Name = new string('A', 300);
        });
        Test("P3 Very long product name (200 char)", c => {
            c.Products.Add(MakeProduct(new string('X', 200), "L1", 100));
        });
        Test("P3 Very long about (5000 char)", c => {
            c.Brand.About = string.Join(" ", System.Linq.Enumerable.Range(1, 700).Select(i => "test"));
        });
        Test("P3 Broken LogoPath", c => {
            c.Brand.LogoPath = "C:\\nonexistent\\fake.png";
        });
        Test("P3 Product Price=0", c => { c.Products.Add(MakeProduct("Free", "F", 0)); });
        Test("P3 Product Price=very low", c => { c.Products.Add(MakeProduct("Micro", "M", 0.001m)); });
        Test("P3 Product Price=negative", c => { c.Products.Add(MakeProduct("Neg", "N", -100)); });
        Test("P3 Product Currency=EUR", c => {
            var p = MakeProduct("Euro", "E", 50);
            p.Currency = "EUR";
            c.Products.Add(p);
        });
        Test("P3 Empty product name", c => { c.Products.Add(MakeProduct("", "EMPTY", 100)); });
        Test("P3 100 products (stress)", c => {
            for (int i = 1; i <= 100; i++) c.Products.Add(MakeProduct($"Ürün {i}", $"P{i:000}", 100 + i));
        });
        Test("P3 50 references", c => {
            for (int i = 1; i <= 50; i++) c.References.Add(new AlgowProforma.Models.Reference { Name = $"Ref {i}" });
        });

        File.AppendAllText(resultFile, "\n[P4] Sample + alternate paths\n");
        try
        {
            var single = AlgowProforma.Models.Catalog.CreateDefault();
            var prod = MakeProduct("Single Product PDF", "S1", 250);
            single.Products.Add(prod);
            new AlgowProforma.Services.PdfService().GenerateSingleProduct(single, prod, Path.Combine(outDir, "P4_SingleProduct.pdf"));
            passed++;
            File.AppendAllText(resultFile, $"✓ P4 GenerateSingleProduct\n");
        }
        catch (Exception ex) { failed++; failures.Add($"P4 GenerateSingleProduct: {ex.Message}"); File.AppendAllText(resultFile, $"✗ P4 GenerateSingleProduct: {ex.Message}\n"); }

        File.AppendAllText(resultFile, $"\n=== ÖZET ===\n");
        File.AppendAllText(resultFile, $"Toplam:  {passed + failed}\n");
        File.AppendAllText(resultFile, $"PASS:    {passed} ({(100.0 * passed / (passed + failed)):F1}%)\n");
        File.AppendAllText(resultFile, $"FAIL:    {failed}\n");
        File.AppendAllText(resultFile, $"\nPDF çıktıları: {outDir}\n");

        if (failures.Count > 0)
        {
            File.AppendAllText(resultFile, $"\n=== FAIL DETAYLARI ===\n");
            foreach (var f in failures) File.AppendAllText(resultFile, $"  {f}\n");
        }
    }

    private static AlgowProforma.Models.Product MakeProduct(string name, string code, decimal price)
    {
        return new AlgowProforma.Models.Product { Name = name, Code = code, Price = price, Currency = "TL" };
    }

    private static void RegisterFonts()
    {
        var fontDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts");
        if (!Directory.Exists(fontDir)) return;

        foreach (var ttf in Directory.GetFiles(fontDir, "*.ttf"))
        {
            try
            {
                using var stream = File.OpenRead(ttf);
                FontManager.RegisterFont(stream);
            }
            catch
            {
            }
        }
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = GetCrashLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Log rotation — dosya 5MB üstüne çıkmışsa .old olarak rename, yeni dosya başlat
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024)
                {
                    var oldPath = path + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(path, oldPath);
                }
            }
            catch { /* rotation hata verirse normal logging devam etsin */ }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} | source: {source} ===");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"OS 64-bit: {Environment.Is64BitOperatingSystem}, Process 64-bit: {Environment.Is64BitProcess}");
            sb.AppendLine($".NET: {Environment.Version}");
            sb.AppendLine($"Culture: {CultureInfo.CurrentCulture} / UI: {CultureInfo.CurrentUICulture}");
            sb.AppendLine($"AppBase: {AppContext.BaseDirectory}");
            sb.AppendLine($"User: {Environment.UserName}, Machine: {Environment.MachineName}");
            sb.AppendLine($"WorkingSet: {Environment.WorkingSet / 1024 / 1024} MB");
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace:");
                sb.AppendLine(ex.ToString());
            }
            else
            {
                sb.AppendLine("Exception object null (terminating: see source)");
            }
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // log yazma başarısız olursa sessiz geç — zaten crash diyaloğu gözükecek
        }
    }

    private static void ShowCrashDialog(Exception? ex)
    {
        try
        {
            // Popup öncelikli — kullanıcı Ctrl+C ile mesajı kopyalayıp yetkiliye gönderir.
            // Log dosyası background'da yedek olarak yazılır ama mention edilmez.
            var sb = new StringBuilder();
            sb.AppendLine("Algow Proforma beklenmedik bir hata aldı ve kapatılacak.");
            sb.AppendLine();
            if (ex != null)
            {
                sb.AppendLine($"Hata türü: {ex.GetType().FullName}");
                sb.AppendLine($"Mesaj: {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("Detay (stack trace):");
                var stackLines = (ex.StackTrace ?? "").Split('\n');
                foreach (var line in stackLines)
                {
                    var clean = line.TrimEnd();
                    if (!string.IsNullOrWhiteSpace(clean)) sb.AppendLine(clean);
                    if (sb.Length > 4000) { sb.AppendLine("..."); break; }
                }
                if (ex.InnerException != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }
            else
            {
                sb.AppendLine("Hata detayı alınamadı (native crash).");
            }
            sb.AppendLine();
            sb.AppendLine("Bu pencereyi Ctrl+C ile kopyalayıp WhatsApp/mail yoluyla yetkilinize gönderin.");

            MessageBox.Show(sb.ToString(), "Algow Proforma — Hata Raporu",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // MessageBox bile gösterilemiyorsa sessiz geç
        }
    }

    /// <summary>
    /// Vana fotografları ile stres testi — Desktop\ornek-vana-stres-test\ altına ~80 PDF üretir.
    /// 5 kategori: cover-modes (18×3), designs (2×5), products (3 grid), themes (5), edge-cases (6).
    /// Photo source: %TEMP%\ornek-vana-stress\vana-XX.jpg (PowerShell ile pre-stage edilmiş).
    /// </summary>
    private static void RunVanaStressTest()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        RegisterFonts();

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var root = Path.Combine(desktop, "ornek-vana-stres-test");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);

        var photoDir = Path.Combine(Path.GetTempPath(), "ornek-vana-stress");
        var vanaPhotos = Directory.Exists(photoDir)
            ? Directory.GetFiles(photoDir, "vana-*.jpg").OrderBy(p => p).ToArray()
            : Array.Empty<string>();

        var sb = new StringBuilder();
        sb.AppendLine($"=== Örnek Vana Stres Test — {DateTime.Now:yyyy-MM-dd HH:mm} ===");
        sb.AppendLine($"Cikti klasoru: {root}");
        sb.AppendLine($"Vana fotograflar: {vanaPhotos.Length} adet");
        sb.AppendLine();

        if (vanaPhotos.Length == 0)
        {
            sb.AppendLine("HATA: vana foto bulunamadi. %TEMP%\\ornek-vana-stress\\ klasoru bos.");
            File.WriteAllText(Path.Combine(root, "_RAPOR.txt"), sb.ToString());
            return;
        }

        int produced = 0;
        int failed = 0;

        void Produce(string subFolder, string fileName, Action<AlgowProforma.Models.Catalog> setup)
        {
            try
            {
                var cat = AlgowProforma.Models.Catalog.CreateDefault();
                setup(cat);
                var dir = Path.Combine(root, subFolder);
                Directory.CreateDirectory(dir);
                new AlgowProforma.Services.PdfService().Generate(cat, Path.Combine(dir, $"{fileName}.pdf"));
                produced++;
                sb.AppendLine($"  OK  {subFolder}/{fileName}");
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"  FAIL  {subFolder}/{fileName} — {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Vana ürünleri seed (kataloglarda kullanılacak)
        void AddVanaProducts(AlgowProforma.Models.Catalog cat, int count)
        {
            var names = new[] {
                ("Topkapı Tam Geçişli Bilyalı Vana 1/2\"", "ORN-TG-12"),
                ("Topkapı Tam Geçişli Bilyalı Vana 3/4\"", "ORN-TG-34"),
                ("Topkapı Tam Geçişli Bilyalı Vana 1\"",   "ORN-TG-10"),
                ("Galata Küresel Vana DN20",               "ORN-KV-20"),
                ("Galata Küresel Vana DN25",               "ORN-KV-25"),
                ("Galata Küresel Vana DN50",               "ORN-KV-50"),
                ("Boğaziçi Çek Valf 1/2\"",                "ORN-CV-12"),
                ("Boğaziçi Çek Valf 3/4\"",                "ORN-CV-34"),
                ("Üsküdar Endüstriyel Valf Gövdesi",       "ORN-EV-01"),
            };
            decimal[] prices = { 189m, 249m, 449m, 329m, 489m, 689m, 169m, 219m, 1290m };
            for (int i = 0; i < Math.Min(count, names.Length); i++)
            {
                var (nm, code) = names[i];
                cat.Products.Add(new AlgowProforma.Models.Product
                {
                    Name = nm, Code = code, Price = prices[i], Currency = "TL",
                    ImagePath = vanaPhotos.Length > i ? vanaPhotos[i] : ""
                });
            }
        }

        // ========== 01-cover-modes — her vana x 3 mod ==========
        sb.AppendLine("[01] Cover modes — 18 vana × 3 mod (FullPage / UstYari / SagYari)");
        var modes = new[]
        {
            ("FullPage", AlgowProforma.Models.CoverImageMode.FullPage),
            ("UstYari", AlgowProforma.Models.CoverImageMode.TopWithBrandBar),
            ("SagYari", AlgowProforma.Models.CoverImageMode.BackgroundWithOverlay),
        };
        for (int i = 0; i < vanaPhotos.Length; i++)
        {
            var photo = vanaPhotos[i];
            var stem = Path.GetFileNameWithoutExtension(photo);
            foreach (var (modeLabel, mode) in modes)
            {
                Produce("01-cover-modes", $"{stem}-{modeLabel}", c =>
                {
                    c.Cover.CustomCoverImagePath = photo;
                    c.Cover.CustomCoverImageMode = mode;
                    AddVanaProducts(c, 3);
                });
            }
        }

        // ========== 02-designs — 2 öne çıkan vana × 5 design ==========
        sb.AppendLine();
        sb.AppendLine("[02] Designs — vana-07 (boy sıralı) + vana-13 (valf) × 5 design × FullPage");
        var featuredPhotos = new[] {
            vanaPhotos.Length > 6 ? vanaPhotos[6] : vanaPhotos[0],     // vana-07
            vanaPhotos.Length > 12 ? vanaPhotos[12] : vanaPhotos[0],   // vana-13
        };
        var designs = new[] { "klasik", "sertifika", "minimalist", "mozaik", "madalya" };
        foreach (var photo in featuredPhotos)
        {
            var stem = Path.GetFileNameWithoutExtension(photo);
            foreach (var design in designs)
            {
                Produce("02-designs", $"{stem}-{design}", c =>
                {
                    c.DesignId = design;
                    c.Cover.CustomCoverImagePath = photo;
                    c.Cover.CustomCoverImageMode = AlgowProforma.Models.CoverImageMode.FullPage;
                    AddVanaProducts(c, 3);
                });
            }
        }

        // ========== 03-products-grid — 1/4/9 ürün layout ==========
        sb.AppendLine();
        sb.AppendLine("[03] Products grid — 1/4/9 ürün × vana-07 FullPage cover");
        var gridPhoto = vanaPhotos.Length > 6 ? vanaPhotos[6] : vanaPhotos[0];
        var layouts = new[] { ("tek", "1x1", 1), ("4lu", "2x2", 4), ("9lu", "3x3", 9) };
        foreach (var (label, layoutId, count) in layouts)
        {
            Produce("03-products-grid", $"vana-{count}urun-{label}", c =>
            {
                c.LayoutId = layoutId;
                c.Cover.CustomCoverImagePath = gridPhoto;
                AddVanaProducts(c, count);
            });
        }

        // ========== 04-themes — 5 farklı tema ==========
        sb.AppendLine();
        sb.AppendLine("[04] Themes — 5 tema × vana-13 FullPage cover");
        var themePhoto = vanaPhotos.Length > 12 ? vanaPhotos[12] : vanaPhotos[0];
        foreach (var themeId in new[] { "klasik", "klasik", "petrol", "bordo", "bakir" })
        {
            Produce("04-themes", $"theme-{themeId}", c =>
            {
                c.ThemeId = themeId;
                c.Cover.CustomCoverImagePath = themePhoto;
                AddVanaProducts(c, 4);
            });
        }

        // ========== 05-edge-cases — edge senaryolar ==========
        sb.AppendLine();
        sb.AppendLine("[05] Edge cases — watermark/logoBg/year/noFeatures/longAbout/noContact");
        var edgePhoto = vanaPhotos.Length > 6 ? vanaPhotos[6] : vanaPhotos[0];

        Produce("05-edge-cases", "watermark-TASLAK", c =>
        {
            c.Brand.WatermarkText = "TASLAK";
            c.Cover.CustomCoverImagePath = edgePhoto;
            AddVanaProducts(c, 4);
        });
        Produce("05-edge-cases", "logoBackground-ON", c =>
        {
            c.Brand.ShowLogoBackground = true;
            c.Cover.CustomCoverImagePath = edgePhoto;
            AddVanaProducts(c, 4);
        });
        Produce("05-edge-cases", "year-uzun-tarih", c =>
        {
            c.Cover.CustomCoverImagePath = edgePhoto;
            c.Cover.YearText = "Kasım 2026 Yıllık Kataloğu";
            AddVanaProducts(c, 4);
        });
        Produce("05-edge-cases", "no-features", c =>
        {
            c.Cover.Feature1 = c.Cover.Feature2 = c.Cover.Feature3 = c.Cover.Feature4 = "";
            c.Cover.CustomCoverImagePath = edgePhoto;
            AddVanaProducts(c, 4);
        });
        Produce("05-edge-cases", "long-about", c =>
        {
            c.Brand.About = "Örnek Vana Ltd. Şti. 2012'den bu yana Kırıkkale Keskin OSB'de CNC hassasiyetle ısıtma-soğutma sektörüne yönelik bilyalı vana, küresel vana, çek valf ve endüstriyel valf üretimi yapmaktadır. Mühendislik standartlarında, dayanıklılık ve verimlilik odaklı güvenilir çözümler sunarız. ISO 9001:2015 sertifikalı tesisimizde her bir ürün öncesi-sonrası kalite kontrolden geçer. Topkapı, Galata, Boğaziçi, Üsküdar serileri başta olmak üzere 200+ ürün portföyümüz Türkiye'nin 14 ilinde ana bayi ağıyla servis edilmektedir.";
            c.Cover.CustomCoverImagePath = edgePhoto;
            AddVanaProducts(c, 4);
        });
        Produce("05-edge-cases", "no-contact", c =>
        {
            c.Brand.Phone = ""; c.Brand.Email = ""; c.Brand.Web = ""; c.Brand.Address = "";
            c.Cover.CustomCoverImagePath = edgePhoto;
            AddVanaProducts(c, 4);
        });

        sb.AppendLine();
        sb.AppendLine($"=== TOPLAM: {produced} OK, {failed} FAIL ===");
        File.WriteAllText(Path.Combine(root, "_RAPOR.txt"), sb.ToString(), Encoding.UTF8);
    }
}
