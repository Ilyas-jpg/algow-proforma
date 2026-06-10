using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AyTeknikKatalog.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AyTeknikKatalog.Services;

public class PdfService
{
    private static string PrimaryHex = "#2E338F";
    private static string SecondaryHex = "#5368A6";
    private static string AccentHex = "#7C84C7";
    private static string MutedHex = "#818285";
    private static string SurfaceHex = "#F5F5F7";
    private static string BorderHex = "#E5E5E7";
    private static string TextHex = "#1A1A1F";
    private const string White = "#FFFFFF";

    private static void ApplyTheme(string? themeId)
    {
        var theme = PdfTheme.GetById(themeId);
        PrimaryHex = theme.PrimaryHex;
        SecondaryHex = theme.SecondaryHex;
        AccentHex = theme.AccentHex;
        MutedHex = theme.MutedHex;
        SurfaceHex = theme.SurfaceHex;
        BorderHex = theme.BorderHex;
        TextHex = theme.TextHex;
    }

    // Plus Jakarta Sans + Sora bundled. Variable font kerning hafif aggressive bazı yerlerde
    // boşluk sıkışıyor ("Yüksek Basınç" → "YüksekBasınç") ama "Şti." gibi resmi şirket adlar TAM gözüküyor.
    // Inter alternative test edildi: kerning OK ama brand name truncation ("Şti." → "Ş.") yaratıyor — daha kötü.
    // Trade-off Plus Jakarta lehine. Bu PdfService brand-width bug (font değil) → sonraki sprint'te header width fix.
    private const string DisplayFont = "Sora";
    private const string BodyFont = "Plus Jakarta Sans";

    private static int ProductsPerPage = 9;
    private static int ProductsPerRow = 3;
    private static int MaxRowsPerPage = 3;
    private static int CardHeight = 200;
    private static int CardImageHeight = 112;
    private static int CardMetaHeight = 58;
    private static PdfDesign ActiveDesign = PdfDesign.Default;
    private static PageLayout ActiveLayout = PageLayout.Default;

    private const int ReferencesPerRow = 5;
    private const int ReferenceRowsPerPage = 5;
    private const int ReferencesPerPage = ReferencesPerRow * ReferenceRowsPerPage;

    private static readonly CultureInfo TrCulture = new("tr-TR");

    public void GenerateSingleProduct(Catalog catalog, Product product, string outputPath)
    {
        ApplyTheme(catalog.ThemeId);
        ActiveDesign = PdfDesign.GetById(catalog.DesignId);
        var doc = Document.Create(d =>
        {
            d.Page(p => RenderSingleProductPage(p, catalog.Brand, product));
        });

        var settings = new QuestPDF.Infrastructure.ImageGenerationSettings
        {
            ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
            RasterDpi = 200,
        };
        var images = QuestPDF.Fluent.GenerateExtensions.GenerateImages(doc, settings).ToList();
        if (images.Count == 0) throw new InvalidOperationException("Sayfa üretilemedi.");
        File.WriteAllBytes(outputPath, images[0]);
    }

    public void Generate(Catalog catalog, string outputPath)
    {
        ApplyTheme(catalog.ThemeId);
        ActiveDesign = PdfDesign.GetById(catalog.DesignId);
        ApplyLayout(PageLayout.GetById(catalog.LayoutId));

        Document.Create(doc =>
        {
            doc.Page(p => RenderCover(p, catalog.Brand, catalog.Cover));

            if (catalog.References.Count > 0 && !catalog.SkipReferencesPage)
            {
                var totalRefPages = (catalog.References.Count + ReferencesPerPage - 1) / ReferencesPerPage;
                for (int rp = 0; rp < totalRefPages; rp++)
                {
                    var chunk = catalog.References
                        .Skip(rp * ReferencesPerPage)
                        .Take(ReferencesPerPage)
                        .ToList();
                    var page = rp + 1;
                    doc.Page(p => RenderReferences(p, catalog, chunk, page, totalRefPages));
                }
            }

            // Manual modda Grid-tabanlı render: her sayfa kendi pozisyonlarıyla render edilir.
            // Otomatik modda eski Column-tabanlı yığma.
            if (catalog.UseCustomPageLayouts && catalog.CustomPages.Count > 0)
            {
                var byId = catalog.Products.ToDictionary(p => p.Id, p => p);
                var assignedIds = new HashSet<string>();

                int totalPages = catalog.CustomPages.Count;
                // Atanmamış ürünler için auto-flow sayfa sayısını da ekle
                var unassigned = catalog.Products.Where(p =>
                    !catalog.CustomPages.Any(pg => pg.ProductIds.Contains(p.Id))).ToList();
                if (unassigned.Count > 0)
                {
                    // Default layout ile kaç sayfa eklenir hesap
                    var fallback = PageLayout.GetById(catalog.CustomPages[catalog.CustomPages.Count - 1].LayoutId);
                    int perPage = Math.Max(1, fallback.Total);
                    totalPages += (int)Math.Ceiling(unassigned.Count / (double)perPage);
                }

                int pageNum = 0;
                foreach (var planned in catalog.CustomPages)
                {
                    pageNum++;
                    var layout = PageLayout.GetById(planned.LayoutId);
                    ApplyLayout(layout);
                    var placements = ComputeManualPlacements(planned, layout, byId);
                    int currentPage = pageNum;
                    int totalP = totalPages;
                    doc.Page(p => RenderManualGridPage(p, catalog.Brand, placements, layout, currentPage, totalP));
                    foreach (var id in planned.ProductIds) if (!string.IsNullOrEmpty(id)) assignedIds.Add(id);
                }

                // Atanmamış ürünler: son layout ile auto-flow
                if (unassigned.Count > 0)
                {
                    var fallbackLayout = PageLayout.GetById(catalog.CustomPages[catalog.CustomPages.Count - 1].LayoutId);
                    ApplyLayout(fallbackLayout);
                    var elements = BuildPageElements(unassigned);
                    var autoPages = PackElementsIntoPages(elements);
                    foreach (var slice in autoPages)
                    {
                        pageNum++;
                        int currentPage = pageNum;
                        int totalP = totalPages;
                        doc.Page(p => RenderMixedProductPage(p, catalog.Brand, slice, currentPage, totalP, showIntro: false));
                    }
                }
            }
            else
            {
                ApplyLayout(PageLayout.GetById(catalog.LayoutId));
                var elements = BuildPageElements(catalog.Products);
                var pages = PackElementsIntoPages(elements);
                for (int i = 0; i < pages.Count; i++)
                {
                    var slice = pages[i];
                    int pageNumber = i + 1;
                    int totalPages = pages.Count;
                    doc.Page(p => RenderMixedProductPage(p, catalog.Brand, slice, pageNumber, totalPages, showIntro: false));
                }
            }
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = $"{catalog.Brand.Name} — Ürün Kataloğu",
            Author = catalog.Brand.Name,
            Subject = catalog.Brand.Tagline ?? "Ürün Kataloğu",
            Keywords = "katalog, ürün, teklif, proforma, fiyat listesi",
            Creator = "Algow Proforma PDF",
            Producer = "Algow Proforma PDF (QuestPDF)",
            CreationDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
        })
        .WithSettings(new DocumentSettings
        {
            // Stream-level deflate compression — content stream'i sıkıştırır,
            // dosya boyutunu ~%30-50 küçültür (özellikle text-heavy katalogda).
            CompressDocument = true,
            // High = JPEG quality ~90, görsel kayıp insansı gözle fark edilmez,
            // VeryHigh'a göre dosya %20 daha küçük.
            ImageCompressionQuality = ImageCompressionQuality.High,
            // Default 288 DPI overkill (baskı için 150-200 yeter). 200 = print-friendly
            // + ekran net + dosya ~%40 daha küçük + viewer decode süresi azalır.
            ImageRasterDpi = 200,
        })
        .GeneratePdf(outputPath);

        // Post-process: viewer'ın "başta beyaz ekran" davranışını ortadan kaldırmak için
        // PDF'i linearized (Fast Web View) hale getir. ShouldLinearize policy karar verir.
        bool hasCoverImage = !string.IsNullOrEmpty(catalog.Cover?.CustomCoverImagePath);
        PdfPostProcessor.LinearizeIfNeeded(outputPath, hasCoverImage);
    }

    private static void ApplyLayout(PageLayout layout)
    {
        ActiveLayout = layout;
        ProductsPerRow = layout.Columns;
        ProductsPerPage = layout.Total;
        MaxRowsPerPage = layout.Rows;

        // Per-layout hand-tuned dimensions: her düzen ~720pt yüksekliğindeki içerik
        // alanını dengeli doldurur. Görsel oranı genelde 0.85-1.2 arası (kare-ish);
        // 2-col uzun layoutlarda (2x4) görsel zorunlu olarak wide kalır.
        // Toplam yükseklik bütçesi 760pt (slim header/footer sonrası).
        // Her satıra hücre padding olarak +14pt eklenir.
        // Hesap: rows × cardH + rows × 14 ≤ 760.
        (int cardH, int imageH, int metaH) = (layout.Columns, layout.Rows) switch
        {
            (1, 1) => (700, 600, 100),  // 1 ürün — büyük görsel; +14 = 714 ≤ 760
            (1, 2) => (340, 270, 70),   // 2 ürün alt alta; 2×354 = 708
            (2, 2) => (340, 230, 110),  // 4 ürün; 2×354 = 708
            (2, 3) => (230, 160, 70),   // 6 ürün; 3×244 = 732
            (2, 4) => (170, 115, 55),   // 8 ürün; 4×184 = 736
            (3, 3) => (230, 150, 80),   // 9 ürün; 3×244 = 732
            (4, 3) => (230, 130, 100),  // 12 ürün (4×3); 3×244 = 732
            (3, 4) => (170, 105, 65),   // 12 ürün (3×4); 4×184 = 736
            (4, 4) => (170, 95, 75),    // 16 ürün; 4×184 = 736
            _ => (200, 110, 90),
        };

        CardHeight = cardH;
        CardImageHeight = imageH;
        CardMetaHeight = metaH;
    }

    // ============================================================
    //   Manuel mod: Grid-tabanlı yerleştirme (Sayfa Planlayıcı ile birebir)
    // ============================================================

    private sealed record ManualPlacement(Product? Product, int Row, int Col, int RowSpan, int ColSpan);

    private static List<ManualPlacement> ComputeManualPlacements(
        CustomPageEntry entry, PageLayout layout, IReadOnlyDictionary<string, Product> byId)
    {
        int cols = Math.Max(1, layout.Columns);
        int rows = Math.Max(1, layout.Rows);
        int total = cols * rows;

        // ProductIds gerekirse pad/truncate (rendering güvenliği)
        var ids = new List<string>(entry.ProductIds);
        while (ids.Count < total) ids.Add(string.Empty);
        if (ids.Count > total) ids = ids.Take(total).ToList();

        var placements = new List<ManualPlacement>();
        var covered = new HashSet<int>();

        for (int i = 0; i < total; i++)
        {
            if (covered.Contains(i)) continue;
            int row = i / cols;
            int col = i % cols;

            string pid = ids[i];
            Product? product = !string.IsNullOrEmpty(pid) && byId.TryGetValue(pid, out var p) ? p : null;

            int rowSpan = 1, colSpan = 1;
            if (product is not null)
            {
                if (product.HasTable && product.Table is not null)
                {
                    rowSpan = Math.Min(2, rows);
                    colSpan = cols;
                }
                else if (product.IsFeatured && cols >= 2 && col + 2 <= cols)
                {
                    colSpan = 2;
                }
            }

            for (int dr = 0; dr < rowSpan; dr++)
                for (int dc = 0; dc < colSpan; dc++)
                    covered.Add((row + dr) * cols + (col + dc));

            placements.Add(new ManualPlacement(product, row, col, rowSpan, colSpan));
        }

        return placements;
    }

    private static void RenderManualGridPage(
        PageDescriptor page, BrandInfo brand,
        List<ManualPlacement> placements, PageLayout layout,
        int pageNumber, int totalPages)
    {
        ApplyInnerPageDefaults(page);
        ApplyWatermark(page, brand);
        page.Header().Element(e => RenderInnerHeader(e, brand, "ÜRÜN KATALOĞU"));
        page.Footer().Element(e => RenderInnerFooter(e, $"{pageNumber} / {totalPages}", brand));

        // Vertical padding 4 (eskiden 16 idi). Slim header/footer zaten görsel
        // ayrım veriyor; ekstra padding 3-satır manuel sayfayı taşırıyordu.
        page.Content().PaddingHorizontal(36).PaddingVertical(4).Column(col =>
        {
            col.Item().Element(e => RenderManualGridContent(e, placements, layout));
        });
    }

    private static void RenderManualGridContent(IContainer container, List<ManualPlacement> placements, PageLayout layout)
    {
        const float padding = 7f;
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (int i = 0; i < layout.Columns; i++)
                    c.RelativeColumn();
            });

            foreach (var p in placements)
            {
                var cell = t.Cell();
                if (p.ColSpan > 1) cell.ColumnSpan((uint)p.ColSpan);
                if (p.RowSpan > 1) cell.RowSpan((uint)p.RowSpan);

                // Hücre yüksekliği = rowSpan × cardHeight
                int cellHeight = p.RowSpan * CardHeight;

                cell.Padding(padding).Element(e =>
                {
                    if (p.Product is null)
                    {
                        // Boş slot — şeffaf placeholder (görsel olarak boşluk)
                        e.MinHeight(cellHeight).Background(SurfaceHex);
                    }
                    else if (p.Product.HasTable && p.Product.Table is not null)
                    {
                        e.Height(cellHeight).Element(c => RenderProductTableBlock(c, p.Product));
                    }
                    else if (p.Product.IsFeatured && layout.Columns >= 2)
                    {
                        e.Height(cellHeight).Element(c => RenderFeaturedProductCard(c, p.Product));
                    }
                    else
                    {
                        e.Height(cellHeight).Element(c => RenderProductCard(c, p.Product, 0));
                    }
                });
            }
        });
    }

    private abstract record PageElement;
    private sealed record ProductRowElement(List<Product> Products) : PageElement;
    private sealed record TableElement(Product Product) : PageElement;

    // Walks products in user order, emitting full rows of regular cards and
    // full-width table blocks. Featured (2-col) products that don't fit the
    // remaining row are reordered with the next 1-col regular ahead, but the
    // reordering never crosses a table block.
    private static List<PageElement> BuildPageElements(IList<Product> products)
    {
        var queue = new LinkedList<Product>(products);
        var elements = new List<PageElement>();
        var currentRow = new List<Product>();
        int colInRow = 0;

        void FlushRow()
        {
            if (currentRow.Count > 0)
            {
                elements.Add(new ProductRowElement(currentRow));
                currentRow = new List<Product>();
            }
            colInRow = 0;
        }

        while (queue.Count > 0)
        {
            var head = queue.First!;
            var headProduct = head.Value;

            if (headProduct.HasTable && headProduct.Table is not null)
            {
                queue.Remove(head);
                FlushRow();
                elements.Add(new TableElement(headProduct));
                continue;
            }

            int available = ProductsPerRow - colInRow;
            int headSpan = headProduct.IsFeatured ? 2 : 1;
            LinkedListNode<Product>? picked;

            if (headSpan <= available)
            {
                picked = head;
            }
            else
            {
                picked = null;
                for (var node = head.Next; node != null; node = node.Next)
                {
                    if (node.Value.HasTable) break;
                    int span = node.Value.IsFeatured ? 2 : 1;
                    if (span <= available)
                    {
                        picked = node;
                        break;
                    }
                }
            }

            if (picked is null)
            {
                FlushRow();
                continue;
            }

            var product = picked.Value;
            queue.Remove(picked);
            currentRow.Add(product);
            colInRow += product.IsFeatured ? 2 : 1;
            if (colInRow >= ProductsPerRow) FlushRow();
        }

        FlushRow();
        return elements;
    }

    // Slim header (~42pt) + Slim footer (~28pt) sonrası A4'te kalan kullanılabilir
    // yükseklik ≈ 760pt. Per-layout dimension hesapları 720pt'lik bir alana göre
    // ayarlandı (40pt güvenlik payı).
    private const int PageContentHeightApprox = 760;
    private const int IntroBlockHeightApprox = 70;
    private const int TableBlockBaseHeightApprox = 210;
    private const int TableRowHeightApprox = 22;
    private const int RowGapApprox = 14;

    private static int EstimateTableBlockHeight(Product product)
    {
        var table = product.Table;
        if (table is null) return TableBlockBaseHeightApprox;
        var rowCount = table.Rows?.Count ?? 0;
        var hasHeader = (table.Columns?.Count ?? 0) > 0;
        return TableBlockBaseHeightApprox + (hasHeader ? TableRowHeightApprox : 0) + rowCount * TableRowHeightApprox;
    }

    private static List<List<PageElement>> PackElementsIntoPages(IList<PageElement> elements)
    {
        var pages = new List<List<PageElement>>();
        var current = new List<PageElement>();
        int currentHeight = 0;
        int rowHeight = CardHeight + RowGapApprox;
        bool introUsed = false;

        foreach (var element in elements)
        {
            int h = element switch
            {
                ProductRowElement => rowHeight,
                TableElement t => EstimateTableBlockHeight(t.Product),
                _ => 0,
            };
            int introCost = (!introUsed && pages.Count == 0 && current.All(e => e is not ProductRowElement)
                && element is ProductRowElement) ? IntroBlockHeightApprox : 0;
            int budget = PageContentHeightApprox;
            if (current.Count > 0 && currentHeight + h + introCost > budget)
            {
                pages.Add(current);
                current = new List<PageElement>();
                currentHeight = 0;
                introCost = 0;
            }
            if (introCost > 0)
            {
                introUsed = true;
                currentHeight += introCost;
            }
            current.Add(element);
            currentHeight += h;
        }
        if (current.Count > 0) pages.Add(current);
        return pages;
    }

    private static void RenderCover(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        page.Size(PageSizes.A4);
        page.Margin(0);
        page.PageColor(White);
        page.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(10).FontColor(TextHex));
        ApplyWatermark(page, brand);

        // FREE-FORM MODE — kullanıcı Kapak Tasarım Stüdyosu'nda element listesi oluşturduysa onları render et
        if (cover.IsFreeFormMode)
        {
            RenderFreeFormCover(page, brand, cover);
            return;
        }

        if (!string.IsNullOrWhiteSpace(cover.CustomCoverImagePath) && File.Exists(cover.CustomCoverImagePath))
        {
            // Foto yüklendiğinde kullanıcı 3 mod arasından seçer:
            // FullPage = arka plan görseli + overlay panel (mevcut)
            // TopWithBrandBar = üst yarı foto + alt yarı tasarımın klasik render'ı (yeni)
            // BackgroundWithOverlay = sağ yarı foto + sol yarı tipografi (yeni)
            switch (cover.CustomCoverImageMode)
            {
                case CoverImageMode.TopWithBrandBar:
                    RenderCoverImageTopHalf(page, brand, cover, cover.CustomCoverImagePath);
                    return;
                case CoverImageMode.BackgroundWithOverlay:
                    RenderCoverImageRightHalf(page, brand, cover, cover.CustomCoverImagePath);
                    return;
                default:
                    RenderCoverWithImageBackground(page, brand, cover, cover.CustomCoverImagePath);
                    return;
            }
        }

        var designBgImage = ImageStorage.FindCoverImage(ActiveDesign.Id);
        if (designBgImage != null)
        {
            RenderCoverWithImageBackground(page, brand, cover, designBgImage);
            return;
        }

        switch (ActiveDesign.CoverLayout)
        {
            case CoverLayout.Egri:
                RenderCoverEgri(page, brand, cover);
                break;
            case CoverLayout.Cerceve:
                RenderCoverCerceve(page, brand, cover);
                break;
            case CoverLayout.Geometri:
                RenderCoverGeometri(page, brand, cover);
                break;
            case CoverLayout.Madalya:
                RenderCoverMadalya(page, brand, cover);
                break;
            case CoverLayout.Katman:
                RenderCoverKatman(page, brand, cover);
                break;
            case CoverLayout.Mozaik:
                RenderCoverMozaik(page, brand, cover);
                break;
            case CoverLayout.Diyagonal:
                RenderCoverDiyagonal(page, brand, cover);
                break;
            case CoverLayout.Minimalist:
                RenderCoverMinimalist(page, brand, cover);
                break;
            case CoverLayout.Sertifika:
                RenderCoverSertifika(page, brand, cover);
                break;
            default:
                RenderCoverKlasik(page, brand, cover);
                break;
        }
    }

    private static (string bigTitle, string subtitle, string tagline, string editionText, string sectionLabel, string year)
        BuildCoverStrings(BrandInfo brand, CoverPageInfo cover)
    {
        var (autoBig, autoSub) = SplitBrandTitle(brand.Name);
        var bigTitle = !string.IsNullOrWhiteSpace(cover.MainTitle)
            ? cover.MainTitle.ToUpper(TrCulture)
            : (autoBig ?? string.Empty).ToUpper(TrCulture);
        var year = string.IsNullOrWhiteSpace(cover.YearText) ? DateTime.Now.Year.ToString(TrCulture) : cover.YearText.Trim();
        // Tarih textbox doluysa tüm renderer'larda yıl yerine bu gözükür (tek-nokta override; "Mayıs 2026" gibi)
        if (!string.IsNullOrWhiteSpace(cover.DateText))
            year = cover.DateText.Trim();
        var subtitle = !string.IsNullOrWhiteSpace(cover.Subtitle)
            ? cover.Subtitle
            : (autoSub ?? string.Empty);
        var tagline = brand.Tagline ?? string.Empty;
        // edition stamp kaldırıldı (kullanıcı kararı 2026-05-19) — sadece manuel EditionLabel set ise gösterilir
        var editionText = cover.EditionLabel ?? "";
        var sectionLabel = string.IsNullOrWhiteSpace(cover.SectionLabel) ? "ÜRÜN KATALOĞU" : cover.SectionLabel;
        return (bigTitle, subtitle, tagline, editionText, sectionLabel, year);
    }

    // KLASIK — endüstriyel blueprint (mevcut tasarım)
    private static void RenderCoverKlasik(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Column(col =>
        {
            col.Item().PaddingTop(26).PaddingHorizontal(48).Row(row =>
            {
                row.AutoItem().Element(c =>
                {
                    if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                    {
                        if (brand.ShowLogoBackground)
                            c.Width(38).Height(38).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                        else
                            c.Width(38).Height(38).Image(brand.LogoPath).FitArea();
                    }
                    else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                });
                row.AutoItem().PaddingLeft(14).AlignMiddle()
                    .Text("KATALOG").FontFamily(DisplayFont).FontSize(13).FontColor(MutedHex).LetterSpacing(0.4f);
                row.RelativeItem();
            });

            col.Item().PaddingTop(18).Background(PrimaryHex).Height(310).Layers(layers =>
            {
                layers.Layer().AlignRight().AlignBottom().Element(e =>
                    e.Width(310).Height(310).Svg(BuildGearBlueprintSvg(cx: 380, cy: 380)));

                layers.PrimaryLayer().PaddingLeft(48).PaddingRight(220).PaddingVertical(58).Column(c =>
                {
                    c.Item().Text(bigTitle)
                        .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 50f, 13, 22f)).Bold().FontColor(White).LetterSpacing(-0.015f);
                    c.Item().PaddingTop(4).Text(subtitle)
                        .FontFamily(DisplayFont).FontSize(22).Light().FontColor(AccentHex);
                    c.Item().PaddingTop(22).Width(60).LineHorizontal(1.4f).LineColor(AccentHex);
                    c.Item().PaddingTop(14).Text(tagline)
                        .FontFamily(BodyFont).FontSize(11.5f).FontColor(AccentHex);
                });
            });

            col.Item().PaddingTop(22).PaddingHorizontal(48).Row(row =>
            {
                row.AutoItem().Width(4).Height(72).Background(PrimaryHex);
                row.ConstantItem(300).PaddingLeft(20).AlignMiddle()
                    .Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 54f, 6, 14f)).Bold().FontColor(PrimaryHex);
                row.AutoItem().PaddingLeft(20).AlignMiddle().Width(120).Height(54).Svg(BuildAccentDecorationSvg());
                row.RelativeItem();
            });

            col.Item().PaddingTop(20).AlignCenter().Row(r =>
            {
                r.AutoItem().AlignMiddle().Width(36).Height(2).Background(AccentHex);
                r.AutoItem().AlignMiddle().PaddingLeft(6).Width(8).Height(8).Svg(BuildSmallDiamondSvg());
                r.AutoItem().AlignMiddle().PaddingHorizontal(18).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(14).Bold().FontColor(PrimaryHex).LetterSpacing(0.5f);
                r.AutoItem().AlignMiddle().PaddingRight(6).Width(8).Height(8).Svg(BuildSmallDiamondSvg());
                r.AutoItem().AlignMiddle().Width(36).Height(2).Background(AccentHex);
            });

            if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
            {
                col.Item().PaddingTop(18).PaddingHorizontal(80).AlignCenter().Text(brand.About)
                    .FontFamily(BodyFont).FontSize(11).LineHeight(1.6f).FontColor(TextHex);
            }

            if (cover.ShowFeatures)
            {
                col.Item().PaddingTop(22).PaddingHorizontal(60).Row(row =>
                {
                    BuildFeatureItem(row, BuildFactoryIconSvg(), Fallback(cover.Feature1, "KALİTE"));
                    BuildFeatureItem(row, BuildGearIconSvg(), Fallback(cover.Feature2, "UZMANLIK"));
                    BuildFeatureItem(row, BuildTruckIconSvg(), Fallback(cover.Feature3, "HIZLI TESLİMAT"));
                    BuildFeatureItem(row, BuildShieldIconSvg(), Fallback(cover.Feature4, "GÜVEN"));
                });
            }

            if (cover.ShowContactBar)
            {
                col.Item().PaddingTop(22).Background(PrimaryHex).PaddingHorizontal(36).PaddingVertical(18).Row(row =>
                {
                    BuildContactItem(row, BuildPhoneIconSvg(), brand.Phone);
                    BuildContactDivider(row);
                    BuildContactItem(row, BuildEmailIconSvg(), brand.Email);
                    BuildContactDivider(row);
                    BuildContactItem(row, BuildWebIconSvg(), brand.Web);
                    BuildContactDivider(row);
                    BuildContactItem(row, BuildPinIconSvg(), brand.Address);
                });
            }
        });
    }

    // Yüklenmiş arka plan görseli olan tasarım için tek tip metin overlay'i
    private static void RenderCoverWithImageBackground(PageDescriptor page, BrandInfo brand, CoverPageInfo cover, string imagePath)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Image(imagePath).FitArea();

            layers.PrimaryLayer().PaddingHorizontal(48).Column(col =>
            {
                col.Item().PaddingTop(48).Row(row =>
                {
                    row.AutoItem().Element(c =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        {
                            if (brand.ShowLogoBackground)
                                c.Width(40).Height(40).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                            else
                                c.Width(40).Height(40).Image(brand.LogoPath).FitArea();
                        }
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                    row.RelativeItem();
                });

                col.Item().PaddingTop(220).PaddingHorizontal(0).Element(e =>
                    e.Background(White).Padding(20).Column(c =>
                    {
                        c.Item().Text(sectionLabel)
                            .FontFamily(DisplayFont).FontSize(11).Bold().FontColor(MutedHex).LetterSpacing(0.5f);
                        c.Item().PaddingTop(8).Text(bigTitle)
                            .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 40f, 14, 20f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.02f);
                        c.Item().PaddingTop(4).Text(subtitle)
                            .FontFamily(DisplayFont).FontSize(18).Light().FontColor(SecondaryHex);
                        c.Item().PaddingTop(14).Width(60).LineHorizontal(1.6f).LineColor(PrimaryHex);
                        c.Item().PaddingTop(10).Text(tagline)
                            .FontFamily(BodyFont).FontSize(11).Italic().FontColor(TextHex);

                        if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                        {
                            c.Item().PaddingTop(16).Text(brand.About)
                                .FontFamily(BodyFont).FontSize(10).LineHeight(1.55f).FontColor(TextHex);
                        }

                        c.Item().PaddingTop(16).Row(r =>
                        {
                            r.AutoItem().AlignMiddle().Text(year)
                                .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 28f, 6, 12f)).Bold().FontColor(PrimaryHex);
                            r.AutoItem().PaddingLeft(16).AlignBottom().PaddingBottom(8).Width(48).LineHorizontal(1.4f).LineColor(AccentHex);
                        });
                    }));

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(16).Element(e =>
                        e.Background(PrimaryHex).PaddingHorizontal(20).PaddingVertical(12).Row(row =>
                        {
                            BuildContactItem(row, BuildPhoneIconSvg(), brand.Phone);
                            BuildContactDivider(row);
                            BuildContactItem(row, BuildEmailIconSvg(), brand.Email);
                            BuildContactDivider(row);
                            BuildContactItem(row, BuildWebIconSvg(), brand.Web);
                            BuildContactDivider(row);
                            BuildContactItem(row, BuildPinIconSvg(), brand.Address);
                        }));
                }
            });
        });
    }

    // EĞRİ — iç içe iki ark, sağ üstte şerit deseni
    private static void RenderCoverEgri(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{SecondaryHex}""/>
  <g stroke=""{PrimaryHex}"" stroke-width=""1.4"" opacity=""0.55"">
    <line x1=""420"" y1=""26"" x2=""595"" y2=""26""/>
    <line x1=""420"" y1=""46"" x2=""595"" y2=""46""/>
    <line x1=""420"" y1=""66"" x2=""595"" y2=""66""/>
    <line x1=""420"" y1=""86"" x2=""595"" y2=""86""/>
    <line x1=""420"" y1=""106"" x2=""595"" y2=""106""/>
    <line x1=""420"" y1=""126"" x2=""595"" y2=""126""/>
    <line x1=""420"" y1=""146"" x2=""595"" y2=""146""/>
    <line x1=""420"" y1=""166"" x2=""595"" y2=""166""/>
    <line x1=""420"" y1=""186"" x2=""595"" y2=""186""/>
    <line x1=""420"" y1=""206"" x2=""595"" y2=""206""/>
    <line x1=""420"" y1=""226"" x2=""595"" y2=""226""/>
    <line x1=""420"" y1=""246"" x2=""595"" y2=""246""/>
  </g>
  <circle cx=""440"" cy=""-120"" r=""780"" fill=""{White}""/>
  <circle cx=""440"" cy=""-120"" r=""780"" fill=""none"" stroke=""{AccentHex}"" stroke-width=""1.5""/>
  <circle cx=""90"" cy=""900"" r=""360"" fill=""{SurfaceHex}""/>
  <circle cx=""90"" cy=""900"" r=""360"" fill=""none"" stroke=""{AccentHex}"" stroke-width=""1.5""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(60).Column(col =>
            {
                col.Item().PaddingTop(160).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(8).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 48f, 13, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.02f);
                col.Item().PaddingTop(4).Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(20).Light().FontColor(SecondaryHex);
                col.Item().PaddingTop(18).Width(60).LineHorizontal(2).LineColor(PrimaryHex);
                col.Item().PaddingTop(12).Text(tagline)
                    .FontFamily(BodyFont).FontSize(12).Italic().FontColor(TextHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(24).Width(360).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10).LineHeight(1.6f).FontColor(TextHex);
                }

                col.Item().PaddingTop(36).Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 34f, 6, 14f)).Bold().FontColor(PrimaryHex);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(120).Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(TextHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    // ÇERÇEVE — köşe üçgenleri + iç çift çizgili kesik köşeli çerçeve
    private static void RenderCoverCerceve(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{White}""/>
  <polygon points=""0,0 230,0 0,260"" fill=""{PrimaryHex}""/>
  <polygon points=""595,842 595,580 360,842"" fill=""{PrimaryHex}""/>
  <polygon points=""60,0 220,0 60,160"" fill=""{SecondaryHex}""/>
  <polygon points=""595,790 535,842 380,842"" fill=""{SecondaryHex}""/>
  <polygon points=""90,46 526,46 549,72 549,756 506,792 70,792 50,766 50,82"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""1.4""/>
  <polygon points=""98,58 518,58 540,80 540,748 502,780 78,780 60,758 60,90"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""0.6"" opacity=""0.6""/>
  <g fill=""{MutedHex}"" opacity=""0.7"">
    <circle cx=""440"" cy=""80"" r=""1.2""/><circle cx=""450"" cy=""80"" r=""1.2""/><circle cx=""460"" cy=""80"" r=""1.2""/><circle cx=""470"" cy=""80"" r=""1.2""/><circle cx=""480"" cy=""80"" r=""1.2""/>
    <circle cx=""440"" cy=""90"" r=""1.2""/><circle cx=""450"" cy=""90"" r=""1.2""/><circle cx=""460"" cy=""90"" r=""1.2""/><circle cx=""470"" cy=""90"" r=""1.2""/><circle cx=""480"" cy=""90"" r=""1.2""/>
    <circle cx=""440"" cy=""100"" r=""1.2""/><circle cx=""450"" cy=""100"" r=""1.2""/><circle cx=""460"" cy=""100"" r=""1.2""/><circle cx=""470"" cy=""100"" r=""1.2""/><circle cx=""480"" cy=""100"" r=""1.2""/>
    <circle cx=""115"" cy=""742"" r=""1.2""/><circle cx=""125"" cy=""742"" r=""1.2""/><circle cx=""135"" cy=""742"" r=""1.2""/><circle cx=""145"" cy=""742"" r=""1.2""/><circle cx=""155"" cy=""742"" r=""1.2""/>
    <circle cx=""115"" cy=""752"" r=""1.2""/><circle cx=""125"" cy=""752"" r=""1.2""/><circle cx=""135"" cy=""752"" r=""1.2""/><circle cx=""145"" cy=""752"" r=""1.2""/><circle cx=""155"" cy=""752"" r=""1.2""/>
    <circle cx=""115"" cy=""762"" r=""1.2""/><circle cx=""125"" cy=""762"" r=""1.2""/><circle cx=""135"" cy=""762"" r=""1.2""/><circle cx=""145"" cy=""762"" r=""1.2""/><circle cx=""155"" cy=""762"" r=""1.2""/>
  </g>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(90).PaddingVertical(150).Column(col =>
            {
                col.Item().PaddingTop(70).AlignCenter().Width(80).LineHorizontal(1.4f).LineColor(PrimaryHex);
                col.Item().PaddingTop(28).AlignCenter().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(11).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(16).AlignCenter().Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 50f, 13, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.02f);
                col.Item().PaddingTop(8).AlignCenter().Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(18).Light().Italic().FontColor(SecondaryHex);
                col.Item().PaddingTop(28).AlignCenter().Width(80).LineHorizontal(1.4f).LineColor(PrimaryHex);
                col.Item().PaddingTop(18).AlignCenter().Text(tagline)
                    .FontFamily(BodyFont).FontSize(12).Italic().FontColor(TextHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(24).AlignCenter().Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10).LineHeight(1.65f).FontColor(TextHex);
                }

                col.Item().PaddingTop(40).AlignCenter().Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 28f, 6, 12f)).Bold().FontColor(PrimaryHex);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(28).AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(TextHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    // GEOMETRİ — keskin geometrik poligonlar, dik kontrast
    private static void RenderCoverGeometri(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{White}""/>
  <polygon points=""440,0 595,0 595,260 380,180"" fill=""{SurfaceHex}""/>
  <polygon points=""595,260 595,440 380,360 380,180"" fill=""{SecondaryHex}""/>
  <polygon points=""595,440 595,640 320,540 380,360"" fill=""{PrimaryHex}""/>
  <g stroke=""{White}"" stroke-width=""0.8"" opacity=""0.55"">
    <line x1=""400"" y1=""450"" x2=""590"" y2=""500""/>
    <line x1=""395"" y1=""470"" x2=""585"" y2=""520""/>
    <line x1=""390"" y1=""490"" x2=""580"" y2=""540""/>
    <line x1=""385"" y1=""510"" x2=""575"" y2=""560""/>
    <line x1=""380"" y1=""530"" x2=""570"" y2=""580""/>
    <line x1=""375"" y1=""550"" x2=""565"" y2=""600""/>
    <line x1=""370"" y1=""570"" x2=""560"" y2=""620""/>
  </g>
  <polygon points=""595,640 595,842 280,842 320,540"" fill=""{SurfaceHex}""/>
  <polygon points=""0,720 0,842 200,842"" fill=""{TextHex}""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(48).Column(col =>
            {
                col.Item().PaddingTop(60).Row(row =>
                {
                    row.AutoItem().Element(c =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        {
                            if (brand.ShowLogoBackground)
                                c.Width(38).Height(38).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                            else
                                c.Width(38).Height(38).Image(brand.LogoPath).FitArea();
                        }
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                });

                col.Item().PaddingTop(160).Width(380).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(10).Width(380).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 56f, 11, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.025f).LineHeight(0.95f);
                col.Item().PaddingTop(8).Width(380).Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(22).Light().FontColor(SecondaryHex);
                col.Item().PaddingTop(20).Width(60).LineHorizontal(2).LineColor(PrimaryHex);
                col.Item().PaddingTop(14).Width(380).Text(tagline)
                    .FontFamily(BodyFont).FontSize(12).Italic().FontColor(TextHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(24).Width(380).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10).LineHeight(1.65f).FontColor(TextHex);
                }

                col.Item().PaddingTop(36).Text(year)
                    .FontFamily(DisplayFont).FontSize(36).Bold().FontColor(PrimaryHex);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(40).Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(TextHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    // MADALYA — ortada beyaz panel + simetrik üçgen kanatlar
    private static void RenderCoverMadalya(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{SurfaceHex}""/>
  <polygon points=""0,180 0,540 200,440 200,280"" fill=""{SecondaryHex}""/>
  <polygon points=""595,180 595,540 395,440 395,280"" fill=""{SecondaryHex}""/>
  <polygon points=""0,300 0,460 110,420 110,340"" fill=""{PrimaryHex}""/>
  <polygon points=""595,300 595,460 485,420 485,340"" fill=""{PrimaryHex}""/>
  <g stroke=""{White}"" stroke-width=""0.6"" opacity=""0.5"">
    <line x1=""20"" y1=""360"" x2=""95"" y2=""360""/>
    <line x1=""20"" y1=""370"" x2=""95"" y2=""370""/>
    <line x1=""20"" y1=""380"" x2=""95"" y2=""380""/>
    <line x1=""20"" y1=""390"" x2=""95"" y2=""390""/>
    <line x1=""20"" y1=""400"" x2=""95"" y2=""400""/>
    <line x1=""500"" y1=""360"" x2=""575"" y2=""360""/>
    <line x1=""500"" y1=""370"" x2=""575"" y2=""370""/>
    <line x1=""500"" y1=""380"" x2=""575"" y2=""380""/>
    <line x1=""500"" y1=""390"" x2=""575"" y2=""390""/>
    <line x1=""500"" y1=""400"" x2=""575"" y2=""400""/>
  </g>
  <polygon points=""180,80 415,80 415,720 297,800 180,720"" fill=""{White}""/>
  <polygon points=""180,720 415,720 297,800"" fill=""{PrimaryHex}""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(208).PaddingVertical(110).Column(col =>
            {
                col.Item().PaddingTop(60).AlignCenter().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(14).AlignCenter().Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 28f, 20, 16f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.01f).LineHeight(1.0f);
                col.Item().PaddingTop(6).AlignCenter().Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(13).Light().Italic().FontColor(SecondaryHex);
                col.Item().PaddingTop(16).AlignCenter().Width(36).LineHorizontal(1).LineColor(PrimaryHex);
                col.Item().PaddingTop(12).AlignCenter().Text(tagline)
                    .FontFamily(BodyFont).FontSize(9.5f).Italic().FontColor(TextHex).LineHeight(1.5f);
                col.Item().PaddingTop(24).AlignCenter().Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 34f, 6, 14f)).Bold().FontColor(PrimaryHex);
            });
        });
    }

    // KATMAN — diyagonal katmanlı düzlemler (mimari/malzeme)
    private static void RenderCoverKatman(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{White}""/>
  <polygon points=""350,0 595,0 595,420 360,360"" fill=""{SurfaceHex}""/>
  <polygon points=""360,360 595,420 595,580 280,560"" fill=""{SecondaryHex}""/>
  <polygon points=""280,560 595,580 595,842 220,842"" fill=""{PrimaryHex}""/>
  <g stroke=""{MutedHex}"" stroke-width=""0.5"" opacity=""0.4"">
    <line x1=""20"" y1=""730"" x2=""275"" y2=""580""/>
    <line x1=""20"" y1=""740"" x2=""275"" y2=""590""/>
    <line x1=""20"" y1=""750"" x2=""275"" y2=""600""/>
    <line x1=""20"" y1=""760"" x2=""275"" y2=""610""/>
    <line x1=""20"" y1=""770"" x2=""275"" y2=""620""/>
    <line x1=""20"" y1=""780"" x2=""275"" y2=""630""/>
  </g>
  <polygon points=""460,710 540,710 540,790 460,790"" fill=""none"" stroke=""{AccentHex}"" stroke-width=""1""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(48).Column(col =>
            {
                col.Item().PaddingTop(60).Row(row =>
                {
                    row.RelativeItem();
                    row.AutoItem().AlignMiddle().Text(year)
                        .FontFamily(DisplayFont).FontSize(11).Bold().FontColor(MutedHex).LetterSpacing(0.5f);
                });
                col.Item().PaddingTop(14).Height(1).Background(BorderHex);

                col.Item().PaddingTop(110).Width(360).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(10).Width(360).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 54f, 12, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.025f).LineHeight(0.96f);
                col.Item().PaddingTop(6).Width(360).Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(20).Light().FontColor(SecondaryHex);
                col.Item().PaddingTop(18).Width(50).LineHorizontal(1.6f).LineColor(PrimaryHex);
                col.Item().PaddingTop(12).Width(360).Text(tagline)
                    .FontFamily(BodyFont).FontSize(12).Italic().FontColor(TextHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(20).Width(360).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10).LineHeight(1.6f).FontColor(TextHex);
                }

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(24).Width(360).Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(TextHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    // MOZAİK — sol koyu blok + sağda kesişen geometrik parçalar
    private static void RenderCoverMozaik(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{White}""/>
  <rect x=""0"" y=""0"" width=""350"" height=""842"" fill=""{PrimaryHex}""/>
  <polygon points=""350,0 460,0 460,90 410,160 350,180"" fill=""{SecondaryHex}""/>
  <polygon points=""460,0 595,0 595,210 530,170 460,90"" fill=""{White}""/>
  <polygon points=""460,90 530,170 595,210 595,260 470,250 410,160"" fill=""{SurfaceHex}""/>
  <polygon points=""350,180 410,160 470,250 470,460 380,520 350,420"" fill=""{SecondaryHex}""/>
  <polygon points=""470,250 595,260 595,520 470,460"" fill=""{TextHex}""/>
  <g stroke=""{MutedHex}"" stroke-width=""0.8"" opacity=""0.7"">
    <line x1=""490"" y1=""280"" x2=""490"" y2=""500""/>
    <line x1=""505"" y1=""285"" x2=""505"" y2=""505""/>
    <line x1=""520"" y1=""290"" x2=""520"" y2=""510""/>
    <line x1=""535"" y1=""285"" x2=""535"" y2=""505""/>
    <line x1=""550"" y1=""290"" x2=""550"" y2=""512""/>
    <line x1=""565"" y1=""285"" x2=""565"" y2=""508""/>
    <line x1=""580"" y1=""290"" x2=""580"" y2=""512""/>
  </g>
  <polygon points=""350,420 380,520 470,460 470,520 410,640 350,540"" fill=""{SurfaceHex}""/>
  <polygon points=""350,540 410,640 350,720"" fill=""{SecondaryHex}""/>
  <polygon points=""470,520 595,520 595,842 350,842 350,720 410,640"" fill=""{SurfaceHex}""/>
  <polygon points=""350,720 410,640 470,520 470,650 410,842 350,842"" fill=""{SecondaryHex}""/>
  <polygon points=""410,842 470,650 530,750 510,842"" fill=""{TextHex}""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(48).Column(col =>
            {
                col.Item().PaddingTop(60).Row(row =>
                {
                    row.AutoItem().Element(c =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        {
                            if (brand.ShowLogoBackground)
                                c.Width(38).Height(38).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                            else
                                c.Width(38).Height(38).Image(brand.LogoPath).FitArea();
                        }
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                });

                col.Item().PaddingTop(180).Width(280).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(AccentHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(10).Width(280).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 48f, 9, 20f)).Bold().FontColor(White).LetterSpacing(-0.025f).LineHeight(0.95f);
                col.Item().PaddingTop(8).Width(280).Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(20).Light().FontColor(AccentHex);
                col.Item().PaddingTop(20).Width(60).LineHorizontal(2).LineColor(AccentHex);
                col.Item().PaddingTop(14).Width(280).Text(tagline)
                    .FontFamily(BodyFont).FontSize(11).Italic().FontColor(White);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(20).Width(280).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(9.5f).LineHeight(1.6f).FontColor(AccentHex);
                }

                col.Item().PaddingTop(40).Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 38f, 6, 16f)).Bold().FontColor(White);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(28).Width(280).Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(AccentHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    // DİYAGONAL — sağ üstte eğik şerit + arc, sağda dikey çizgili bant, alt blok
    private static void RenderCoverDiyagonal(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            layers.Layer().Svg($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 595 842"" preserveAspectRatio=""none"">
  <rect width=""595"" height=""842"" fill=""{SurfaceHex}""/>
  <polygon points=""360,0 480,0 480,260 360,140"" fill=""{PrimaryHex}""/>
  <polygon points=""480,0 595,0 595,210 530,170 480,90"" fill=""{SecondaryHex}""/>
  <path d=""M 595 200 A 250 250 0 0 1 470 460 L 595 460 Z"" fill=""{SurfaceHex}""/>
  <path d=""M 595 200 A 250 250 0 0 1 470 460"" fill=""none"" stroke=""{MutedHex}"" stroke-width=""1.5"" opacity=""0.7""/>
  <rect x=""490"" y=""460"" width=""105"" height=""300"" fill=""{TextHex}""/>
  <g stroke=""{MutedHex}"" stroke-width=""1"" opacity=""0.6"">
    <line x1=""500"" y1=""475"" x2=""500"" y2=""750""/>
    <line x1=""513"" y1=""475"" x2=""513"" y2=""750""/>
    <line x1=""526"" y1=""475"" x2=""526"" y2=""750""/>
    <line x1=""539"" y1=""475"" x2=""539"" y2=""750""/>
    <line x1=""552"" y1=""475"" x2=""552"" y2=""750""/>
    <line x1=""565"" y1=""475"" x2=""565"" y2=""750""/>
    <line x1=""578"" y1=""475"" x2=""578"" y2=""750""/>
  </g>
  <rect x=""360"" y=""700"" width=""130"" height=""142"" fill=""{PrimaryHex}""/>
  <rect x=""490"" y=""760"" width=""105"" height=""82"" fill=""{SecondaryHex}""/>
  <path d=""M 0 760 A 280 280 0 0 1 280 842 L 0 842 Z"" fill=""{MutedHex}"" opacity=""0.35""/>
  <path d=""M 0 760 A 280 280 0 0 1 280 842"" fill=""none"" stroke=""{MutedHex}"" stroke-width=""1.2"" opacity=""0.6""/>
</svg>");

            layers.PrimaryLayer().PaddingHorizontal(60).Column(col =>
            {
                col.Item().PaddingTop(60).Row(row =>
                {
                    row.AutoItem().Element(c =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        {
                            if (brand.ShowLogoBackground)
                                c.Width(38).Height(38).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                            else
                                c.Width(38).Height(38).Image(brand.LogoPath).FitArea();
                        }
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                });

                col.Item().PaddingTop(180).Width(320).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(MutedHex).LetterSpacing(0.6f);
                col.Item().PaddingTop(8).Width(320).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 50f, 10, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.025f).LineHeight(0.96f);
                col.Item().PaddingTop(6).Width(320).Text(subtitle)
                    .FontFamily(DisplayFont).FontSize(20).Light().FontColor(SecondaryHex);
                col.Item().PaddingTop(18).Width(56).LineHorizontal(2).LineColor(PrimaryHex);
                col.Item().PaddingTop(12).Width(320).Text(tagline)
                    .FontFamily(BodyFont).FontSize(11.5f).Italic().FontColor(TextHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(20).Width(320).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10).LineHeight(1.6f).FontColor(TextHex);
                }

                col.Item().PaddingTop(36).Text(year)
                    .FontFamily(DisplayFont).FontSize(36).Bold().FontColor(PrimaryHex);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(28).Width(320).Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(9).FontColor(TextHex));
                        var first = true;
                        void Append(string? v) { if (string.IsNullOrWhiteSpace(v)) return; if (!first) text.Span("  ·  "); text.Span(v); first = false; }
                        Append(brand.Phone);
                        Append(brand.Email);
                        Append(brand.Web);
                    });
                }
            });
        });
    }

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!.ToUpper(TrCulture);

    private static void BuildFeatureItem(RowDescriptor row, string svg, string label)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().AlignCenter().Width(46).Height(46).Svg(svg);
            col.Item().PaddingTop(10).AlignCenter().Text(label)
                .FontFamily(DisplayFont).FontSize(9).Bold().FontColor(PrimaryHex).LetterSpacing(0.4f);
            col.Item().PaddingTop(6).AlignCenter().Width(36).LineHorizontal(0.7f).LineColor(AccentHex);
        });
    }

    private static void BuildContactItem(RowDescriptor row, string svg, string text)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().AlignCenter().Width(34).Height(34).Svg(svg);
            col.Item().PaddingTop(8).AlignCenter().Text(string.IsNullOrWhiteSpace(text) ? "—" : text)
                .FontFamily(BodyFont).FontSize(9).FontColor(White).LetterSpacing(0.05f);
        });
    }

    private static void BuildContactDivider(RowDescriptor row)
    {
        row.AutoItem().AlignMiddle().Width(1).Height(38).Background(SecondaryHex);
    }

    // Uzun marka adlarında punto otomatik küçülür; kelime aralarındaki boşluklarda
    // metin doğal olarak alt satıra geçer (QuestPDF varsayılan). Tek-kelimelik
    // çok uzun isimde bile font yeterince küçülerek tek satırda kalır.
    private static float AutoFitFontSize(string? text, float baseSize, int baseCharLimit, float minSize)
    {
        if (string.IsNullOrWhiteSpace(text)) return baseSize;
        var len = text.Length;
        if (len <= baseCharLimit) return baseSize;
        var ratio = Math.Sqrt((double)baseCharLimit / len);
        return Math.Max(minSize, (float)(baseSize * ratio));
    }

    private static (string Big, string Sub) SplitBrandTitle(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ("", "");

        string[] keywords = { "Makina", "Makine", "Sanayi", "Ticaret", "Hırdavat", "Endüstri" };
        foreach (var kw in keywords)
        {
            var idx = name.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return (name[..idx].Trim(), name[idx..].Trim());
        }
        return (name, "");
    }

    private static string BuildGearBlueprintSvg(double cx = 200, double cy = 200)
    {
        var teethPath = BuildGearTeethPath(teeth: 22, outerR: 188, innerR: 168);
        var inv = CultureInfo.InvariantCulture;
        var cxs = cx.ToString("F2", inv);
        var cys = cy.ToString("F2", inv);
        var grid = BuildFadingGrid(cx, cy, halfRange: 380, spacing: 12, fadeRadius: 360);
        var blueprintColor = AccentHex;
        var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 400 400"">
  <defs>
    <clipPath id=""gearBound""><rect x=""0"" y=""0"" width=""400"" height=""400""/></clipPath>
  </defs>
  <g clip-path=""url(#gearBound)"">
    <g stroke=""{blueprintColor}"" stroke-linecap=""butt"">
      {grid}
    </g>
    <g transform=""translate({cxs},{cys})"" fill=""none"" stroke=""{blueprintColor}"" stroke-linecap=""round"" stroke-linejoin=""round"">
      <path d=""{teethPath}"" stroke-width=""2.4"" opacity=""0.95""/>
      <circle r=""158"" stroke-width=""1.1"" opacity=""0.6""/>
      <circle r=""135"" stroke-width=""1.7"" opacity=""0.75""/>
      <circle r=""105"" stroke-width=""0.95"" opacity=""0.55""/>
      <circle r=""78"" stroke-width=""1.4"" opacity=""0.7""/>
      <circle r=""50"" stroke-width=""1.7"" opacity=""0.75""/>
      <circle r=""22"" stroke-width=""1.5""/>
      <circle r=""6"" fill=""{blueprintColor}"" stroke=""none""/>
      <line x1=""-188"" y1=""0"" x2=""188"" y2=""0"" stroke-width=""0.95"" stroke-dasharray=""4,3"" opacity=""0.6""/>
      <line x1=""0"" y1=""-188"" x2=""0"" y2=""188"" stroke-width=""0.95"" stroke-dasharray=""4,3"" opacity=""0.6""/>
      <line x1=""-133"" y1=""-133"" x2=""133"" y2=""133"" stroke-width=""0.8"" stroke-dasharray=""3,3"" opacity=""0.45""/>
      <line x1=""-133"" y1=""133"" x2=""133"" y2=""-133"" stroke-width=""0.8"" stroke-dasharray=""3,3"" opacity=""0.45""/>
      <line x1=""0"" y1=""-50"" x2=""0"" y2=""-78"" stroke-width=""1.5""/>
      <line x1=""0"" y1=""50"" x2=""0"" y2=""78"" stroke-width=""1.5""/>
      <line x1=""-50"" y1=""0"" x2=""-78"" y2=""0"" stroke-width=""1.5""/>
      <line x1=""50"" y1=""0"" x2=""78"" y2=""0"" stroke-width=""1.5""/>
    </g>
  </g>
</svg>";
        return svg;
    }

    // Builds a horizontal+vertical grid centered at (cx, cy). Each grid line is
    // sliced into short segments whose opacity falls off radially from (cx, cy),
    // producing a blueprint feel that fades toward the gear's outer edge.
    private static string BuildFadingGrid(double cx, double cy, double halfRange, double spacing, double fadeRadius)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        const double segmentLen = 10;
        const double viewBoxMax = 400;

        double Opacity(double dx, double dy)
        {
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= fadeRadius) return 0;
            var t = 1 - d / fadeRadius;
            return Math.Round(0.5 * t, 3);
        }

        void Emit(double x1, double y1, double x2, double y2, double opacity)
        {
            sb.Append("<line x1=\"").Append(x1.ToString("F2", inv))
              .Append("\" y1=\"").Append(y1.ToString("F2", inv))
              .Append("\" x2=\"").Append(x2.ToString("F2", inv))
              .Append("\" y2=\"").Append(y2.ToString("F2", inv))
              .Append("\" stroke=\"#A3AAD3\" stroke-width=\"0.9\" opacity=\"")
              .Append(opacity.ToString(inv))
              .Append("\"/>");
        }

        // Vertical lines
        for (double off = -halfRange; off <= halfRange + 0.001; off += spacing)
        {
            var x = cx + off;
            if (x < -1 || x > viewBoxMax + 1) continue;
            for (double seg = -halfRange; seg < halfRange - 0.001; seg += segmentLen)
            {
                var y1 = cy + seg;
                var y2 = cy + Math.Min(seg + segmentLen, halfRange);
                var midY = (y1 + y2) / 2;
                if (midY < 0 || midY > viewBoxMax) continue;
                var op = Opacity(off, midY - cy);
                if (op < 0.05) continue;
                Emit(x, y1, x, y2, op);
            }
        }

        // Horizontal lines
        for (double off = -halfRange; off <= halfRange + 0.001; off += spacing)
        {
            var y = cy + off;
            if (y < -1 || y > viewBoxMax + 1) continue;
            for (double seg = -halfRange; seg < halfRange - 0.001; seg += segmentLen)
            {
                var x1 = cx + seg;
                var x2 = cx + Math.Min(seg + segmentLen, halfRange);
                var midX = (x1 + x2) / 2;
                if (midX < 0 || midX > viewBoxMax) continue;
                var op = Opacity(midX - cx, off);
                if (op < 0.05) continue;
                Emit(x1, y, x2, y, op);
            }
        }
        return sb.ToString();
    }

    private static string BuildGearTeethPath(int teeth, double outerR, double innerR)
    {
        var sb = new StringBuilder();
        var step = 360.0 / teeth;
        var halfTooth = step / 4.0;
        var inv = CultureInfo.InvariantCulture;

        for (int i = 0; i < teeth; i++)
        {
            var center = i * step;
            var angles = new[]
            {
                center - halfTooth,
                center + halfTooth,
                center + halfTooth,
                center + step - halfTooth,
            };
            var radii = new[] { outerR, outerR, innerR, innerR };
            for (int j = 0; j < 4; j++)
            {
                var rad = angles[j] * Math.PI / 180.0;
                var x = radii[j] * Math.Cos(rad);
                var y = radii[j] * Math.Sin(rad);
                sb.Append(i == 0 && j == 0 ? "M " : "L ");
                sb.Append(x.ToString("F2", inv)).Append(' ');
                sb.Append(y.ToString("F2", inv)).Append(' ');
            }
        }
        sb.Append('Z');
        return sb.ToString();
    }

    private static string BuildAccentDecorationSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 150 54"">
  <g fill=""{AccentHex}"">
    <circle cx=""4"" cy=""6"" r=""1.2""/><circle cx=""4"" cy=""16"" r=""1.2""/><circle cx=""4"" cy=""26"" r=""1.2""/><circle cx=""4"" cy=""36"" r=""1.2""/><circle cx=""4"" cy=""46"" r=""1.2""/>
    <circle cx=""14"" cy=""6"" r=""1.2""/><circle cx=""14"" cy=""16"" r=""1.2""/><circle cx=""14"" cy=""26"" r=""1.2""/><circle cx=""14"" cy=""36"" r=""1.2""/><circle cx=""14"" cy=""46"" r=""1.2""/>
    <circle cx=""24"" cy=""6"" r=""1.2""/><circle cx=""24"" cy=""16"" r=""1.2""/><circle cx=""24"" cy=""26"" r=""1.2""/><circle cx=""24"" cy=""36"" r=""1.2""/><circle cx=""24"" cy=""46"" r=""1.2""/>
    <circle cx=""34"" cy=""6"" r=""1.2""/><circle cx=""34"" cy=""16"" r=""1.2""/><circle cx=""34"" cy=""26"" r=""1.2""/><circle cx=""34"" cy=""36"" r=""1.2""/><circle cx=""34"" cy=""46"" r=""1.2""/>
  </g>
  <polygon points=""52,16 132,16 124,28 44,28"" fill=""{AccentHex}"" opacity=""0.5""/>
  <polygon points=""60,28 140,28 132,40 52,40"" fill=""{SecondaryHex}"" opacity=""0.35""/>
</svg>";

    private static string BuildSmallDiamondSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""-5 -5 10 10""><polygon points=""0,-4 4,0 0,4 -4,0"" fill=""{AccentHex}""/></svg>";

    private static string BuildFactoryIconSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 64 64"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""2.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <path d=""M8 52 V32 L22 38 V32 L36 38 V32 L50 38 V52 Z""/>
  <path d=""M14 50 V44 H18 V50""/>
  <path d=""M26 50 V44 H30 V50""/>
  <path d=""M38 50 V44 H42 V50""/>
  <path d=""M16 32 V18 H22 V26""/>
  <line x1=""6"" y1=""52"" x2=""58"" y2=""52""/>
</svg>";

    private static string BuildGearIconSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 64 64"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""2.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <circle cx=""32"" cy=""32"" r=""9""/>
  <path d=""M32 8 v6 M32 50 v6 M8 32 h6 M50 32 h6""/>
  <path d=""M14.5 14.5 l4.5 4.5 M45 45 l4.5 4.5 M14.5 49.5 l4.5 -4.5 M45 19 l4.5 -4.5""/>
  <circle cx=""32"" cy=""32"" r=""2.6"" fill=""{PrimaryHex}""/>
</svg>";

    private static string BuildTruckIconSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 64 64"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""2.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <rect x=""6"" y=""20"" width=""30"" height=""22""/>
  <path d=""M36 26 H48 L56 33 V42 H36 Z""/>
  <circle cx=""18"" cy=""46"" r=""4.5""/>
  <circle cx=""46"" cy=""46"" r=""4.5""/>
  <line x1=""38"" y1=""34"" x2=""52"" y2=""34""/>
</svg>";

    private static string BuildShieldIconSvg() =>
        $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 64 64"" fill=""none"" stroke=""{PrimaryHex}"" stroke-width=""2.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <path d=""M32 6 L52 13 V32 C52 44 32 56 32 56 C32 56 12 44 12 32 V13 Z""/>
  <path d=""M22 32 L29 39 L42 25""/>
</svg>";

    private static string BuildPhoneIconSvg() =>
        @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 36 36"" fill=""none"" stroke=""#FFFFFF"" stroke-width=""1.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <circle cx=""18"" cy=""18"" r=""16.5"" stroke-width=""1.2""/>
  <path d=""M12.5 11 H15 L17 15.5 L15 17.5 C16 20.2 17.8 22 20.5 23 L22.5 21 L27 23 V25.5 C27 26.3 26.3 27 25.5 27 C18.5 27 11 19.5 11 12.5 C11 11.7 11.7 11 12.5 11 Z""/>
</svg>";

    private static string BuildEmailIconSvg() =>
        @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 36 36"" fill=""none"" stroke=""#FFFFFF"" stroke-width=""1.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <circle cx=""18"" cy=""18"" r=""16.5"" stroke-width=""1.2""/>
  <rect x=""10"" y=""13"" width=""16"" height=""11"" rx=""1.2""/>
  <path d=""M10.5 14 L18 19.2 L25.5 14""/>
</svg>";

    private static string BuildWebIconSvg() =>
        @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 36 36"" fill=""none"" stroke=""#FFFFFF"" stroke-width=""1.3"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <circle cx=""18"" cy=""18"" r=""16.5"" stroke-width=""1.2""/>
  <circle cx=""18"" cy=""18"" r=""8.5""/>
  <ellipse cx=""18"" cy=""18"" rx=""3.4"" ry=""8.5""/>
  <line x1=""9.5"" y1=""18"" x2=""26.5"" y2=""18""/>
</svg>";

    private static string BuildPinIconSvg() =>
        @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 36 36"" fill=""none"" stroke=""#FFFFFF"" stroke-width=""1.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
  <circle cx=""18"" cy=""18"" r=""16.5"" stroke-width=""1.2""/>
  <path d=""M18 9 C15 9 12 11 12 14.5 C12 19.5 18 27 18 27 C18 27 24 19.5 24 14.5 C24 11 21 9 18 9 Z""/>
  <circle cx=""18"" cy=""14.5"" r=""2.2""/>
</svg>";

    private static void RenderReferences(PageDescriptor page, Catalog catalog,
        IReadOnlyList<Reference> chunk, int pageNumber, int totalPages)
    {
        ApplyInnerPageDefaults(page);
        ApplyWatermark(page, catalog.Brand);
        page.Header().Element(e => RenderInnerHeader(e, catalog.Brand, "REFERANSLAR"));

        page.Content().PaddingHorizontal(36).Column(col =>
        {
            col.Item().PaddingTop(22).Column(c =>
            {
                c.Item().Text("Referanslarımız")
                    .FontFamily(DisplayFont).FontSize(22).Bold().FontColor(TextHex);
                c.Item().PaddingTop(4).Text("İş ortaklarımız ve değerli müşterilerimiz")
                    .FontFamily(BodyFont).FontSize(10.5f).FontColor(MutedHex);
            });

            col.Item().PaddingTop(14).Column(grid =>
            {
                for (int r = 0; r < ReferenceRowsPerPage; r++)
                {
                    grid.Item().Row(row =>
                    {
                        for (int c = 0; c < ReferencesPerRow; c++)
                        {
                            var idx = r * ReferencesPerRow + c;
                            if (idx < chunk.Count)
                                row.RelativeItem().Padding(3).Element(e => RenderReferenceCard(e, chunk[idx]));
                            else
                                row.RelativeItem().Padding(3);
                        }
                    });
                }
            });
        });

        page.Footer().Element(e => RenderInnerFooter(e, $"{pageNumber} / {totalPages}", catalog.Brand));
    }

    private static void RenderReferenceCard(IContainer container, Reference reference)
    {
        var hasName = !string.IsNullOrWhiteSpace(reference.Name);
        container
            .ShowEntire()
            .Column(col =>
            {
                col.Item().Height(80).AlignCenter().AlignMiddle().Element(e =>
                {
                    if (File.Exists(reference.LogoPath))
                        e.Padding(8).MaxHeight(64).Image(reference.LogoPath).FitArea();
                    else if (hasName)
                        e.Text(InitialsOf(reference.Name))
                            .FontFamily(DisplayFont).FontSize(22).Bold().FontColor(PrimaryHex);
                });
                if (hasName)
                {
                    col.Item().PaddingTop(6).PaddingHorizontal(6).AlignCenter().Text(reference.Name)
                        .FontFamily(BodyFont).FontSize(9).Medium().FontColor(MutedHex);
                }
            });
    }

    private static void RenderMixedProductPage(PageDescriptor page, BrandInfo brand,
        List<PageElement> elements, int pageNumber, int totalPages, bool showIntro)
    {
        ApplyInnerPageDefaults(page);
        ApplyWatermark(page, brand);
        page.Header().Element(e => RenderInnerHeader(e, brand, "ÜRÜN KATALOĞU"));

        int rowHeight = CardHeight + RowGapApprox;
        int rowsCount = elements.Count(e => e is ProductRowElement);
        int tablesCount = elements.Count(e => e is TableElement);
        int introHeight = showIntro ? IntroBlockHeightApprox : 16;
        int tablesHeight = elements.OfType<TableElement>().Sum(t => EstimateTableBlockHeight(t.Product) + 12);
        int rowsHeight = rowsCount * rowHeight;
        int totalUsed = introHeight + tablesHeight + rowsHeight;
        int leftover = Math.Max(0, PageContentHeightApprox - totalUsed);
        bool centerRows = tablesCount > 0 && rowsCount > 0 && leftover > 40;
        int spacerBeforeRows = centerRows ? leftover / 2 : 0;
        int spacerAfterRows = centerRows ? leftover - spacerBeforeRows : 0;

        page.Content().PaddingHorizontal(36).Column(col =>
        {
            if (showIntro)
            {
                col.Item().PaddingTop(20).Column(c =>
                {
                    c.Item().Text("Ürünler")
                        .FontFamily(DisplayFont).FontSize(22).Bold().FontColor(TextHex);
                    c.Item().PaddingTop(4).Text("Tüm fiyatlara KDV dahildir · Stok durumuna göre fiyat değişebilir")
                        .FontFamily(BodyFont).FontSize(9.5f).FontColor(MutedHex);
                });
                col.Item().PaddingTop(8);
            }
            else
            {
                col.Item().PaddingTop(16);
            }

            int cardIndex = 0;
            bool firstRowSeen = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                switch (element)
                {
                    case ProductRowElement row:
                        if (!firstRowSeen && centerRows)
                            col.Item().Height(spacerBeforeRows);
                        firstRowSeen = true;
                        int startIdx = cardIndex;
                        col.Item().Element(e => RenderProductRowGrid(e, row.Products, startIdx));
                        cardIndex += row.Products.Count;
                        break;
                    case TableElement t:
                        col.Item().PaddingVertical(6).Element(e => RenderProductTableBlock(e, t.Product));
                        break;
                }
            }
            if (firstRowSeen && centerRows)
                col.Item().Height(spacerAfterRows);
        });

        page.Footer().Element(e => RenderInnerFooter(e, $"{pageNumber} / {totalPages}", brand));
    }

    private static void RenderProductRowGrid(IContainer container, List<Product> products, int startIndex)
    {
        const float padding = 7f;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (int c = 0; c < ProductsPerRow; c++)
                    columns.RelativeColumn();
            });
            int idx = startIndex;
            foreach (var product in products)
            {
                idx++;
                // Featured kart sadece sayfada ≥ 2 sütun varken anlamlı (ColumnSpan 2)
                if (product.IsFeatured && ProductsPerRow >= 2)
                    table.Cell().ColumnSpan(2).Padding(padding).Element(e => RenderFeaturedProductCard(e, product));
                else
                    table.Cell().Padding(padding).Element(e => RenderProductCard(e, product, idx));
            }
        });
    }

    private static void RenderSingleProductPage(PageDescriptor page, BrandInfo brand, Product product)
    {
        ApplyInnerPageDefaults(page);
        ApplyWatermark(page, brand);
        page.Header().Element(e => RenderInnerHeader(e, brand, "ÜRÜN BİLGİ FORMU"));

        page.Content().PaddingHorizontal(36).PaddingVertical(20).Column(col =>
        {
            col.Item().Element(e => RenderSingleProductBlock(e, product));
        });

        page.Footer().Element(e => RenderInnerFooter(e, product.Code ?? string.Empty, brand));
    }

    private static void RenderSingleProductBlock(IContainer container, Product product)
    {
        var table = product.Table ?? new ProductTable();
        var title = !string.IsNullOrWhiteSpace(table.Title) ? table.Title :
                    !string.IsNullOrWhiteSpace(product.Name) ? product.Name : "Ürün";
        var specs = (product.HasTable && product.Table is not null)
            ? table.Specs.Where(s => !string.IsNullOrWhiteSpace(s.Label) || !string.IsNullOrWhiteSpace(s.Value)).ToList()
            : new List<ProductSpec>();
        var columns = table.Columns.ToList();
        var rows = table.Rows.ToList();
        bool hasImage = !string.IsNullOrWhiteSpace(product.ImagePath) && File.Exists(product.ImagePath);
        bool hasTable = product.HasTable && product.Table is not null && columns.Count > 0;
        bool hasRightColumnContent = specs.Count > 0;

        container.Border(0.6f).BorderColor(BorderHex).Column(block =>
        {
            block.Item().Background(PrimaryHex).PaddingHorizontal(20).PaddingVertical(14)
                .Text(title.ToUpper(TrCulture))
                .FontFamily(DisplayFont).FontSize(18).Bold().FontColor(White).LetterSpacing(0.5f);

            block.Item().Height(3).Background(AccentHex);

            if (!string.IsNullOrWhiteSpace(product.Code))
            {
                block.Item().Background(SurfaceHex).PaddingHorizontal(20).PaddingVertical(8)
                    .Text(product.Code)
                    .FontFamily(BodyFont).FontSize(12).SemiBold().FontColor(SecondaryHex).LetterSpacing(0.2f);
            }

            if (hasRightColumnContent)
            {
                block.Item().PaddingHorizontal(20).PaddingTop(16).PaddingBottom(12).Row(row =>
                {
                    row.RelativeItem(1).Height(285).Background(SurfaceHex).Padding(14)
                        .AlignCenter().AlignMiddle().Element(e =>
                        {
                            if (hasImage)
                                e.Image(product.ImagePath).FitArea();
                            else
                                e.Text("Görsel yok")
                                    .FontFamily(BodyFont).FontSize(11).FontColor(MutedHex);
                        });
                    row.ConstantItem(20);
                    row.RelativeItem(1).Column(right =>
                    {
                        right.Item().Column(specCol =>
                        {
                            foreach (var spec in specs)
                            {
                                specCol.Item().PaddingBottom(8).Row(r =>
                                {
                                    r.ConstantItem(135).Text(spec.Label ?? string.Empty)
                                        .FontFamily(BodyFont).FontSize(11).SemiBold().FontColor(MutedHex);
                                    r.RelativeItem().Text(spec.Value ?? string.Empty)
                                        .FontFamily(BodyFont).FontSize(11.5f).FontColor(TextHex);
                                });
                            }
                        });
                        right.Item().PaddingTop(8).Height(1).Background(BorderHex);

                        right.Item().PaddingTop(12).Background(PrimaryHex)
                            .PaddingHorizontal(14).PaddingVertical(12).Row(price =>
                            {
                                price.RelativeItem().AlignMiddle().Text("FİYAT")
                                    .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(AccentHex).LetterSpacing(0.3f);
                                price.AutoItem().AlignMiddle().Text(FormatPrice(product))
                                    .FontFamily(DisplayFont).FontSize(18).Bold().FontColor(White);
                            });
                    });
                });
            }
            else
            {
                int imageHeight = hasTable ? 320 : 460;
                block.Item().PaddingHorizontal(20).PaddingTop(16).PaddingBottom(12)
                    .Height(imageHeight).Background(SurfaceHex).Padding(18)
                    .AlignCenter().AlignMiddle().Element(e =>
                    {
                        if (hasImage)
                            e.Image(product.ImagePath).FitArea();
                        else
                            e.Text("Görsel yok")
                                .FontFamily(BodyFont).FontSize(13).FontColor(MutedHex);
                    });

                block.Item().PaddingHorizontal(20).PaddingBottom(hasTable ? 12 : 20).Element(priceBox =>
                {
                    priceBox.Background(PrimaryHex).PaddingHorizontal(18).PaddingVertical(14).Row(price =>
                    {
                        price.RelativeItem().AlignMiddle().Text("FİYAT")
                            .FontFamily(DisplayFont).FontSize(11).Bold().FontColor(AccentHex).LetterSpacing(0.3f);
                        price.AutoItem().AlignMiddle().Text(FormatPrice(product))
                            .FontFamily(DisplayFont).FontSize(20).Bold().FontColor(White);
                    });
                });
            }

            if (hasTable)
            {
                block.Item().PaddingHorizontal(20).PaddingTop(4).PaddingBottom(20)
                    .Border(0.6f).BorderColor(BorderHex)
                    .Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            for (int i = 0; i < columns.Count; i++)
                                c.RelativeColumn();
                        });

                        foreach (var colDef in columns)
                        {
                            t.Cell().Background(PrimaryHex).PaddingHorizontal(6).PaddingVertical(9)
                                .AlignCenter().AlignMiddle()
                                .Text(colDef.Header ?? string.Empty)
                                .FontFamily(BodyFont).FontSize(10).Bold().FontColor(White);
                        }

                        for (int r = 0; r < rows.Count; r++)
                        {
                            var row = rows[r];
                            var bg = r % 2 == 0 ? White : SurfaceHex;
                            for (int c = 0; c < columns.Count; c++)
                            {
                                var value = c < row.Cells.Count ? (row.Cells[c].Value ?? string.Empty) : string.Empty;
                                t.Cell().Background(bg)
                                    .Border(0.4f).BorderColor(BorderHex)
                                    .PaddingHorizontal(6).PaddingVertical(8)
                                    .AlignCenter().AlignMiddle()
                                    .Text(value)
                                    .FontFamily(BodyFont).FontSize(10).FontColor(TextHex);
                            }
                        }
                    });
            }
        });
    }

    private static void RenderProductTableBlock(IContainer container, Product product)
    {
        var table = product.Table ?? new ProductTable();
        var title = string.IsNullOrWhiteSpace(table.Title) ? product.Name : table.Title;
        var specs = table.Specs.Where(s => !string.IsNullOrWhiteSpace(s.Label) || !string.IsNullOrWhiteSpace(s.Value)).ToList();
        var columns = table.Columns.ToList();
        var rows = table.Rows.ToList();
        bool hasImage = !string.IsNullOrWhiteSpace(product.ImagePath) && File.Exists(product.ImagePath);

        container.ShowEntire().Border(0.6f).BorderColor(BorderHex).Column(block =>
        {
            block.Item().Background(PrimaryHex).PaddingHorizontal(14).PaddingVertical(8)
                .Text(title?.ToUpper(TrCulture) ?? string.Empty)
                .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(White).LetterSpacing(0.4f);

            block.Item().Height(2).Background(AccentHex);

            if (!string.IsNullOrWhiteSpace(product.Code))
            {
                block.Item().Background(SurfaceHex).PaddingHorizontal(14).PaddingVertical(5)
                    .Text(product.Code)
                    .FontFamily(BodyFont).FontSize(9.5f).SemiBold().FontColor(SecondaryHex).LetterSpacing(0.15f);
            }

            block.Item().PaddingHorizontal(14).PaddingTop(10).PaddingBottom(6).Row(row =>
            {
                row.ConstantItem(195).Height(135).Background(SurfaceHex).Padding(8)
                    .AlignCenter().AlignMiddle().Element(e =>
                    {
                        if (hasImage)
                            e.Image(product.ImagePath).FitArea();
                        else
                            e.Text("Görsel yok")
                                .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex);
                    });
                row.ConstantItem(16);
                row.RelativeItem().Column(specCol =>
                {
                    foreach (var spec in specs)
                    {
                        specCol.Item().PaddingBottom(3).Row(r =>
                        {
                            r.ConstantItem(120).Text(spec.Label ?? string.Empty)
                                .FontFamily(BodyFont).FontSize(9).SemiBold().FontColor(MutedHex);
                            r.RelativeItem().Text(spec.Value ?? string.Empty)
                                .FontFamily(BodyFont).FontSize(9.5f).FontColor(TextHex);
                        });
                    }
                });
            });

            if (columns.Count > 0)
            {
                block.Item().PaddingHorizontal(14).PaddingTop(6).PaddingBottom(10)
                    .Border(0.6f).BorderColor(BorderHex)
                    .Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            for (int i = 0; i < columns.Count; i++)
                                c.RelativeColumn();
                        });

                        foreach (var colDef in columns)
                        {
                            t.Cell().Background(PrimaryHex).PaddingHorizontal(4).PaddingVertical(5)
                                .AlignCenter().AlignMiddle()
                                .Text(colDef.Header ?? string.Empty)
                                .FontFamily(BodyFont).FontSize(8.5f).Bold().FontColor(White);
                        }

                        for (int r = 0; r < rows.Count; r++)
                        {
                            var row = rows[r];
                            var bg = r % 2 == 0 ? White : SurfaceHex;
                            for (int c = 0; c < columns.Count; c++)
                            {
                                var value = c < row.Cells.Count ? (row.Cells[c].Value ?? string.Empty) : string.Empty;
                                t.Cell().Background(bg)
                                    .Border(0.35f).BorderColor(BorderHex)
                                    .PaddingHorizontal(4).PaddingVertical(5)
                                    .AlignMiddle()
                                    .Text(value)
                                    .FontFamily(BodyFont).FontSize(8.5f).FontColor(TextHex);
                            }
                        }
                    });
            }
        });
    }

    private static void RenderFeaturedProductCard(IContainer container, Product product)
    {
        container
            .ShowEntire()
            .Column(card =>
            {
                card.Item().Height(6).Background(PrimaryHex);

                // MinHeight yerine Height — featured kartı satır yüksekliğini tam doldursun,
                // alt boşluk kalmasın.
                card.Item().Height(CardHeight - 6).Row(row =>
                {
                    row.RelativeItem(1).Background(SurfaceHex).AlignCenter().AlignMiddle().Element(e =>
                    {
                        if (File.Exists(product.ImagePath))
                            e.Padding(14).Image(product.ImagePath).FitArea();
                        else
                            e.Text("Görsel yok")
                                .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex);
                    });

                    row.RelativeItem(1).Background(PrimaryHex).Padding(16).Column(col =>
                    {
                        col.Item().Text("ÖNE ÇIKAN")
                            .FontFamily(DisplayFont).FontSize(8).Bold().FontColor(AccentHex).LetterSpacing(0.25f);

                        col.Item().PaddingTop(8).Text(TruncateForLines(product.Name, FeaturedCharsPerLine, 3))
                            .FontFamily(DisplayFont).FontSize(13.5f).Bold().FontColor(White);

                        if (!string.IsNullOrWhiteSpace(product.Code))
                        {
                            col.Item().PaddingTop(4).Text(product.Code)
                                .FontFamily(BodyFont).FontSize(10).FontColor(AccentHex).LetterSpacing(0.05f);
                        }

                        col.Item().PaddingTop(14).Row(badge =>
                        {
                            badge.RelativeItem().Background(White).PaddingVertical(7).PaddingHorizontal(12)
                                .AlignCenter().AlignMiddle()
                                .Text(FormatPrice(product))
                                .FontFamily(BodyFont).FontSize(13).Bold().FontColor(PrimaryHex);
                        });
                    });
                });
            });
    }

    private static void RenderProductCard(IContainer container, Product product, int index = 0)
    {
        RenderProductCardStandard(container, product);
    }

    // KLASIK: beyaz kart + lacivert fiyat şeridi (mevcut endüstriyel görünüm)
    private static void RenderProductCardStandard(IContainer container, Product product)
    {
        // Layers ile fiyat + çizgi her zaman kartın ALTINA hizalanır — yanına
        // featured ürün geldiğinde alt sınırlar (featured paneli ile) aynı seviyede
        // görünür, sayfa simetrisi bozulmaz.
        container
            .ShowEntire()
            .Background(White)
            .MinHeight(CardHeight)
            .Layers(layers =>
            {
                // Üst katman: görsel + ad/kod (yukarıdan başlar)
                layers.PrimaryLayer().Column(col =>
                {
                    col.Item().Height(CardImageHeight).Element(e =>
                    {
                        if (File.Exists(product.ImagePath))
                            e.Padding(10).AlignCenter().AlignMiddle().Image(product.ImagePath).FitArea();
                        else
                            e.Background(SurfaceHex).AlignCenter().AlignMiddle().Text("Görsel yok")
                                .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex);
                    });

                    col.Item().PaddingHorizontal(2).PaddingTop(10).Column(meta =>
                    {
                        meta.Item().Text(TruncateForLines(product.Name, CardCharsPerLine, 2))
                            .FontFamily(BodyFont).FontSize(11.5f).SemiBold().FontColor(TextHex);

                        if (!string.IsNullOrWhiteSpace(product.Code))
                        {
                            meta.Item().PaddingTop(3).Text(TruncateForLines(product.Code, CardCodeCharsPerLine, 1))
                                .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex).LetterSpacing(0.05f);
                        }
                    });
                });

                // Alt katman: fiyat + tema-rengi çizgi, kartın en altına dayanır
                layers.Layer().PaddingHorizontal(2).AlignBottom().Column(bottom =>
                {
                    bottom.Item().Text(FormatPrice(product))
                        .FontFamily(BodyFont).FontSize(13).Bold().FontColor(TextHex);
                    bottom.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(PrimaryHex);
                });
            });
    }

    private static void ApplyInnerPageDefaults(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(0);
        page.PageColor(White);
        page.DefaultTextStyle(t => t.FontFamily(BodyFont).FontSize(10).FontColor(TextHex));
    }

    private static void RenderInnerHeader(IContainer container, BrandInfo brand, string sectionLabel)
    {
        // Tek tip slim header: beyaz arka plan, sol marka (logo + isim), sağ section, altta ince çizgi.
        container.Column(col =>
        {
            col.Item().Background(White).PaddingHorizontal(36).PaddingVertical(12).Row(row =>
            {
                row.AutoItem().AlignMiddle().Element(c => MonogramBadgeLight(c, brand.LogoPath, brand.Name, brand.ShowLogoBackground));
                var brandText = brand.Name ?? string.Empty;
                // PRD-01 fix: brand text 2 satıra düşebilsin (uzun şirket adlarında "Şti." truncation engellenir)
                row.RelativeItem().PaddingLeft(6).AlignMiddle().Text(brandText)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(brandText, 12f, 20, 7.5f)).SemiBold().FontColor(TextHex);
                row.AutoItem().AlignMiddle().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(PrimaryHex).LetterSpacing(0.3f);
            });
            col.Item().PaddingHorizontal(36).Height(1).Background(BorderHex);
        });
    }

    private static void RenderInnerFooter(IContainer container, string trailing, BrandInfo? brand = null)
    {
        // Tek tip slim footer: üstte ince çizgi, ortada marka adı, sağda sayfa numarası.
        container.Column(col =>
        {
            col.Item().PaddingHorizontal(36).Height(1).Background(BorderHex);
            col.Item().Background(White).PaddingHorizontal(36).PaddingVertical(8).Row(row =>
            {
                var footerText = string.IsNullOrWhiteSpace(brand?.Name)
                    ? string.Empty
                    : (string.IsNullOrWhiteSpace(brand.Tagline) ? brand.Name : $"{brand.Name} · {brand.Tagline}");
                row.RelativeItem().AlignMiddle().Text(footerText)
                    .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex);
                row.AutoItem().AlignMiddle().Text(trailing)
                    .FontFamily(BodyFont).FontSize(9.5f).SemiBold().FontColor(TextHex);
            });
        });
    }

    private static void MonogramBadgeLight(IContainer container, string? logoPath, string? brandName = null, bool showBackground = false)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            if (showBackground)
                container.Width(36).Height(36).Background(SurfaceHex).Padding(2)
                    .AlignCenter().AlignMiddle()
                    .Image(logoPath).FitArea();
            else
                container.Width(36).Height(36).AlignCenter().AlignMiddle()
                    .Image(logoPath).FitArea();
        }
        else
        {
            container.Width(36).Height(36).Background(PrimaryHex).AlignCenter().AlignMiddle()
                .Text(InitialsOf(brandName ?? string.Empty))
                .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(White);
        }
    }

    private static void MonogramBadgeOnDark(IContainer container, string? logoPath, string? brandName = null)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            container.Width(34).Height(34).Background(White).Padding(2)
                .AlignCenter().AlignMiddle()
                .Image(logoPath).FitArea();
        }
        else
        {
            container.Width(34).Height(34).Background(White)
                .AlignCenter().AlignMiddle()
                .Text(InitialsOf(brandName ?? string.Empty)).FontFamily(DisplayFont).FontSize(13).Bold().FontColor(PrimaryHex);
        }
    }

    private static void SmallMonogramOnDark(IContainer container, string? logoPath, string? brandName = null)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            container.Width(18).Height(18).Background(White).Padding(1)
                .AlignCenter().AlignMiddle()
                .Image(logoPath).FitArea();
        }
        else
        {
            container.Width(18).Height(18).Background(AccentHex)
                .AlignCenter().AlignMiddle()
                .Text(InitialsOf(brandName ?? string.Empty)).FontFamily(DisplayFont).FontSize(8).Bold().FontColor(White);
        }
    }

    private static string TruncateForLines(string text, int charsPerLine, int maxLines)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        var max = charsPerLine * maxLines;
        if (text.Length <= max) return text;
        var cut = max - 1;
        if (cut < 0) cut = 0;
        var sub = text[..cut].TrimEnd();
        return sub + "…";
    }

    private static int CardCharsPerLine => ProductsPerRow switch
    {
        2 => 56,
        3 => 42,
        4 => 26,
        _ => 42,
    };

    private static int CardCodeCharsPerLine => ProductsPerRow switch
    {
        2 => 30,
        3 => 22,
        4 => 14,
        _ => 22,
    };

    private static int FeaturedCharsPerLine => ProductsPerRow switch
    {
        2 => 36,
        3 => 26,
        4 => 18,
        _ => 26,
    };

    private static string InitialsOf(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var parts = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][..1]}{parts[1][..1]}".ToUpperInvariant();
    }

    private static string FormatPrice(Product product)
    {
        if (product.Price <= 0) return "Fiyat sorunuz";
        return $"{product.Price.ToString("N2", TrCulture)} {product.Currency}";
    }

    /// <summary>
    /// Sayfa arka planına çapraz watermark (şeffaf "DRAFT" gibi) ekler. brand.WatermarkText boş ise hiçbir şey yapmaz.
    /// SVG kullanılır çünkü QuestPDF native Rotate API açı parametresi desteklemez.
    /// </summary>
    private static void ApplyWatermark(PageDescriptor page, BrandInfo brand)
    {
        if (string.IsNullOrWhiteSpace(brand.WatermarkText)) return;
        var safeText = System.Net.WebUtility.HtmlEncode(brand.WatermarkText);
        // Bronz/tema PrimaryHex rengi, hafif opacity (içeriği kapatmaz, filigran hissi).
        // SVG fill-opacity kullanılır — fill color override için ayrı parametre.
        var fillColor = PrimaryHex.StartsWith("#") ? PrimaryHex : "#" + PrimaryHex;
        var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 595 842'>
            <text x='297' y='421' text-anchor='middle' transform='rotate(-30 297 421)'
                  font-family='sans-serif' font-size='90' fill='{fillColor}' fill-opacity='0.10' font-weight='600'
                  letter-spacing='8'>
                {safeText}
            </text>
        </svg>";
        page.Background().Svg(svg);
    }

    private static float MmToPt(double mm) => (float)(mm * 595.0 / 210.0);

    /// <summary>
    /// Kullanıcının Kapak Tasarım Stüdyosu'nda yerleştirdiği elementleri render eder.
    /// Her element z-index sırasıyla bir Layer olarak, mm cinsinden pozisyon/boyut → pt'a çevrilir.
    /// </summary>
    private static void RenderFreeFormCover(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        page.Content().Layers(layers =>
        {
            // PrimaryLayer zorunlu (QuestPDF kuralı): krem yüzey arka plan
            layers.PrimaryLayer().Background(SurfaceHex);

            foreach (var el in cover.Elements.OrderBy(e => e.ZIndex))
            {
                layers.Layer().AlignLeft().AlignTop()
                    .TranslateX(MmToPt(el.XMm)).TranslateY(MmToPt(el.YMm))
                    .Width(MmToPt(el.WidthMm)).Height(MmToPt(el.HeightMm))
                    .Element(c => RenderCoverElementContent(c, el));
            }
        });
    }

    private static void RenderCoverElementContent(IContainer c, CoverElement el)
    {
        // Background (Rectangle, Line vs için zorunlu; diğerleri için optional)
        if (!string.IsNullOrEmpty(el.BackgroundHex))
            c = c.Background(el.BackgroundHex);

        switch (el.Type)
        {
            case CoverElementType.Image when File.Exists(el.Content):
                // FitUnproportionally — kullanıcının spec ettiği element alanını TAM doldurur.
                // Aspect ratio bozulabilir (kullanıcı boyut seçti, alan dolması beklenen davranış — Canva benzeri "stretch to fit").
                // Aspect korumak isterse element boyutunu görsele uydurmalı.
                c.Image(el.Content).FitUnproportionally();
                break;
            case CoverElementType.Title:
            case CoverElementType.TextBox:
                var fg = string.IsNullOrEmpty(el.ForegroundHex) ? TextHex : el.ForegroundHex;
                var textBuilder = c.AlignCenter().AlignMiddle().Text(el.Content)
                    .FontFamily(DisplayFont).FontSize((float)el.FontSize).FontColor(fg);
                if (el.Bold) textBuilder = textBuilder.Bold();
                if (el.Italic) textBuilder = textBuilder.Italic();
                break;
            case CoverElementType.Rectangle:
            case CoverElementType.Line:
                // Background zaten uygulandı yukarıda. Görsel ek yok.
                break;
        }
    }

    /// <summary>
    /// Mod: Üst yarı foto + alt yarı tasarım render. Foto sayfanın üst %55'i, alt %45'inde marka bilgileri + iletişim bandı.
    /// </summary>
    private static void RenderCoverImageTopHalf(PageDescriptor page, BrandInfo brand, CoverPageInfo cover, string imagePath)
    {
        var (bigTitle, subtitle, tagline, _, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Column(col =>
        {
            // ÜST: foto sabit Height(540) — FitUnproportionally ile alanı tam doldurur, alt panel için yer kalır
            col.Item().Height(540).Image(imagePath).FitUnproportionally();

            // ALT: bronz panel dikey Column (yatay overflow yok)
            col.Item().Background(PrimaryHex).Padding(24).Column(c =>
            {
                c.Item().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(9).Bold().FontColor(AccentHex).LetterSpacing(2.4f);
                c.Item().PaddingTop(6).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 22f, 22, 11f)).Bold().FontColor(White);
                if (!string.IsNullOrWhiteSpace(tagline))
                    c.Item().PaddingTop(6).Text(tagline)
                        .FontFamily(BodyFont).FontSize(10).Italic().FontColor(AccentHex);
                c.Item().PaddingTop(8).Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 18f, 8, 10f)).Light().FontColor(AccentHex);
            });
        });
    }

    /// <summary>
    /// Mod: Sağ yarı foto + sol yarı tipografi. Foto sayfanın sağ yarısı, sol yarısında büyük başlık + slogan + year + contact.
    /// </summary>
    private static void RenderCoverImageRightHalf(PageDescriptor page, BrandInfo brand, CoverPageInfo cover, string imagePath)
    {
        var (bigTitle, subtitle, tagline, _, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Row(row =>
        {
            // SOL: tipografi alanı (~297pt = A4 width'in yarısı)
            row.RelativeItem().Background(SurfaceHex).PaddingHorizontal(40).PaddingVertical(60).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.AutoItem().Element(e =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        {
                            if (brand.ShowLogoBackground)
                                e.Width(46).Height(46).Background(White).Padding(2).Image(brand.LogoPath).FitArea();
                            else
                                e.Width(46).Height(46).Image(brand.LogoPath).FitArea();
                        }
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                });

                col.Item().PaddingTop(60).Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(MutedHex).LetterSpacing(1.4f);

                col.Item().PaddingTop(20).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 28f, 18, 14f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.02f);

                if (!string.IsNullOrWhiteSpace(subtitle))
                    col.Item().PaddingTop(6).Text(subtitle)
                        .FontFamily(DisplayFont).FontSize(16).Light().FontColor(AccentHex);

                col.Item().PaddingTop(20).Width(60).LineHorizontal(1.6f).LineColor(AccentHex);

                if (!string.IsNullOrWhiteSpace(tagline))
                    col.Item().PaddingTop(14).Text(tagline)
                        .FontFamily(BodyFont).FontSize(11.5f).Italic().FontColor(PrimaryHex);

                col.Item().PaddingTop(36).Text(year)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 34f, 8, 14f)).Light().FontColor(PrimaryHex);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                    col.Item().PaddingTop(28).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(9.5f).LineHeight(1.55f).FontColor(TextHex);

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(28).LineHorizontal(0.8f).LineColor(BorderHex);
                    col.Item().PaddingTop(12).Column(cc =>
                    {
                        if (!string.IsNullOrWhiteSpace(brand.Phone))
                            cc.Item().Text(brand.Phone).FontFamily(BodyFont).FontSize(9.5f).FontColor(TextHex);
                        if (!string.IsNullOrWhiteSpace(brand.Email))
                            cc.Item().PaddingTop(3).Text(brand.Email).FontFamily(BodyFont).FontSize(9.5f).FontColor(TextHex);
                        if (!string.IsNullOrWhiteSpace(brand.Web))
                            cc.Item().PaddingTop(3).Text(brand.Web).FontFamily(BodyFont).FontSize(9.5f).FontColor(TextHex);
                    });
                }
            });

            // SAĞ: foto tam yükseklik
            row.RelativeItem().Image(imagePath).FitArea();
        });
    }

    /// <summary>
    /// Sadece kapak sayfasını PNG byte array olarak render eder. Canlı önizleme için kullanılır.
    /// Düşük DPI (96) hız için. Hata durumunda null döner (UI çökmemeli).
    /// </summary>
    // Önizleme render'larını serileştirir. ApplyTheme/ActiveDesign PAYLAŞILAN STATIK olduğundan,
    // tasarım thumbnail'leri toplu render'ı + canlı kapak önizlemesi aynı anda dönerse renkler
    // birbirine karışır (race). Lock ile tek-seferde bir render → güvenli. (Tam çözüm: RenderContext.)
    private static readonly object _previewRenderLock = new();

    public static byte[]? GenerateCoverPreviewBytes(Catalog catalog)
    {
        lock (_previewRenderLock)
        {
            try
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
                ApplyTheme(catalog.ThemeId);
                ActiveDesign = PdfDesign.GetById(catalog.DesignId);

                var doc = QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(p => RenderCover(p, catalog.Brand, catalog.Cover));
                });

                var images = doc.GenerateImages(new QuestPDF.Infrastructure.ImageGenerationSettings
                {
                    ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
                    RasterDpi = 96
                });
                foreach (var img in images) return img;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    // ============================================================
    // YENİ KAPAK TASARIMLARI (2026-05-19 Faz 16)
    // Mevcut Klasik/Egri/Cerceve/.../Diyagonal metodlarına dokunmadan eklendi.
    // ============================================================

    // MINIMALIST — sade premium, whitespace dominant, ince hat aksanı
    private static void RenderCoverMinimalist(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Column(col =>
        {
            // üst bant — ince accent çizgi
            col.Item().Height(6).Background(PrimaryHex);

            // üst alan: küçük section label + edition
            col.Item().PaddingTop(40).PaddingHorizontal(60).Row(row =>
            {
                row.AutoItem().Element(c =>
                {
                    if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        c.Width(34).Height(34).Image(brand.LogoPath).FitArea();
                    else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                });
                row.RelativeItem();
            });

            // orta blok: çok büyük, ortalı tipografi (whitespace dominant)
            col.Item().PaddingTop(100).PaddingHorizontal(60).Column(c =>
            {
                c.Item().AlignCenter().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(10).FontColor(MutedHex).LetterSpacing(2.8f);

                c.Item().PaddingTop(32).AlignCenter().Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 52f, 14, 26f)).Light().FontColor(TextHex).LetterSpacing(-0.02f);

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    c.Item().PaddingTop(6).AlignCenter().Text(subtitle)
                        .FontFamily(DisplayFont).FontSize(18).Light().FontColor(PrimaryHex);
                }

                c.Item().PaddingTop(36).AlignCenter().Width(80).LineHorizontal(1.2f).LineColor(AccentHex);

                if (!string.IsNullOrWhiteSpace(tagline))
                {
                    c.Item().PaddingTop(18).AlignCenter().Text(tagline)
                        .FontFamily(BodyFont).FontSize(11).Italic().FontColor(MutedHex).LetterSpacing(0.3f);
                }
            });

            if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
            {
                col.Item().PaddingTop(48).PaddingHorizontal(110).AlignCenter().Text(brand.About)
                    .FontFamily(BodyFont).FontSize(10.5f).LineHeight(1.7f).FontColor(TextHex);
            }

            col.Item().PaddingTop(36).AlignCenter().Width(2).Height(40).Background(PrimaryHex);

            col.Item().PaddingTop(14).AlignCenter().Text(year)
                .FontFamily(DisplayFont).FontSize(AutoFitFontSize(year, 36f, 6, 14f)).Light().FontColor(PrimaryHex).LetterSpacing(1.5f);

            // alt: ince contact bar
            col.Item().PaddingTop(50).PaddingHorizontal(60).Column(c =>
            {
                c.Item().LineHorizontal(0.6f).LineColor(BorderHex);
                if (cover.ShowContactBar)
                {
                    c.Item().PaddingTop(14).PaddingBottom(14).Row(row =>
                    {
                        BuildMinimalContactItem(row, brand.Phone);
                        BuildMinimalContactItem(row, brand.Email);
                        BuildMinimalContactItem(row, brand.Web);
                    });
                    c.Item().LineHorizontal(0.6f).LineColor(BorderHex);
                }
            });
        });
    }

    private static void BuildMinimalContactItem(RowDescriptor row, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        row.RelativeItem().AlignCenter().Text(text)
            .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex).LetterSpacing(0.4f);
    }

    // SERTIFIKA — çift çizgi çerçeve + köşe motifleri, ortalı klasik panel
    private static void RenderCoverSertifika(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Padding(28).Layers(layers =>
        {
            // dış çift çerçeve
            layers.Layer().Border(1.6f).BorderColor(PrimaryHex);
            layers.Layer().Padding(6).Border(0.4f).BorderColor(PrimaryHex);

            // köşe diamondları (4 köşeye küçük süs)
            layers.Layer().AlignLeft().AlignTop().Padding(2).Width(14).Height(14).Svg(BuildSmallDiamondSvg());
            layers.Layer().AlignRight().AlignTop().Padding(2).Width(14).Height(14).Svg(BuildSmallDiamondSvg());
            layers.Layer().AlignLeft().AlignBottom().Padding(2).Width(14).Height(14).Svg(BuildSmallDiamondSvg());
            layers.Layer().AlignRight().AlignBottom().Padding(2).Width(14).Height(14).Svg(BuildSmallDiamondSvg());

            // içerik
            layers.PrimaryLayer().Padding(32).Column(col =>
            {
                // üst: section label tek başına ortalı
                col.Item().PaddingTop(8).AlignCenter().Text(sectionLabel)
                    .FontFamily(DisplayFont).FontSize(11).Bold().FontColor(PrimaryHex).LetterSpacing(3.5f);

                col.Item().PaddingTop(8).AlignCenter().Row(r =>
                {
                    r.AutoItem().AlignMiddle().Width(48).Height(1).Background(AccentHex);
                    r.AutoItem().AlignMiddle().PaddingHorizontal(6).Width(8).Height(8).Svg(BuildSmallDiamondSvg());
                    r.AutoItem().AlignMiddle().Width(48).Height(1).Background(AccentHex);
                });

                // logo
                col.Item().PaddingTop(32).AlignCenter().Element(c =>
                {
                    if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        c.Width(70).Height(70).Image(brand.LogoPath).FitArea();
                    else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                });

                // ortalı panel: koyu zemin + başlık
                col.Item().PaddingTop(28).PaddingHorizontal(8).Background(PrimaryHex).Padding(26).Column(c =>
                {
                    c.Item().AlignCenter().Text(bigTitle)
                        .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 38f, 14, 22f)).Bold().FontColor(White).LetterSpacing(-0.01f);

                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        c.Item().PaddingTop(6).AlignCenter().Text(subtitle)
                            .FontFamily(DisplayFont).FontSize(16).Light().FontColor(AccentHex);
                    }

                    c.Item().PaddingTop(14).AlignCenter().Width(56).LineHorizontal(1.2f).LineColor(AccentHex);

                    if (!string.IsNullOrWhiteSpace(tagline))
                    {
                        c.Item().PaddingTop(10).AlignCenter().Text(tagline)
                            .FontFamily(BodyFont).FontSize(10.5f).Italic().FontColor(AccentHex);
                    }
                });

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(22).PaddingHorizontal(36).AlignCenter().Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10.5f).LineHeight(1.65f).FontColor(TextHex);
                }

                if (cover.ShowFeatures)
                {
                    col.Item().PaddingTop(24).PaddingHorizontal(20).Row(row =>
                    {
                        BuildFeatureItem(row, BuildFactoryIconSvg(), Fallback(cover.Feature1, "KALİTE"));
                        BuildFeatureItem(row, BuildGearIconSvg(), Fallback(cover.Feature2, "UZMANLIK"));
                        BuildFeatureItem(row, BuildShieldIconSvg(), Fallback(cover.Feature3, "GÜVEN"));
                        BuildFeatureItem(row, BuildTruckIconSvg(), Fallback(cover.Feature4, "HIZ"));
                    });
                }

                // alt madalya: year
                col.Item().PaddingTop(26).AlignCenter().Row(r =>
                {
                    r.AutoItem().AlignMiddle().Width(60).Height(0.8f).Background(PrimaryHex);
                    r.AutoItem().AlignMiddle().PaddingHorizontal(14).Background(PrimaryHex).PaddingHorizontal(18).PaddingVertical(8)
                        .Text(year).FontFamily(DisplayFont).FontSize(18).Bold().FontColor(White).LetterSpacing(3f);
                    r.AutoItem().AlignMiddle().Width(60).Height(0.8f).Background(PrimaryHex);
                });

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(18).AlignCenter().Text(
                        new[] { brand.Phone, brand.Email, brand.Web }
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Aggregate("", (a, b) => string.IsNullOrEmpty(a) ? b! : $"{a}  ·  {b}"))
                        .FontFamily(BodyFont).FontSize(9).FontColor(MutedHex).LetterSpacing(0.5f);
                }
            });
        });
    }

    // BLUEPRINT — mühendislik grid + teknik vana sembolü
    private static void RenderCoverBlueprint(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Background(White).PaddingHorizontal(50).PaddingVertical(44).Column(col =>
        {
            // üst: brand + edition kompakt
            col.Item().Row(row =>
            {
                row.AutoItem().Element(c =>
                {
                    if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                        c.Width(36).Height(36).Image(brand.LogoPath).FitArea();
                    else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                });
                row.AutoItem().PaddingLeft(14).AlignMiddle().Column(c =>
                {
                    c.Item().Text("TECHNICAL CATALOG")
                        .FontFamily(DisplayFont).FontSize(9).Bold().FontColor(PrimaryHex).LetterSpacing(2f);
                    c.Item().PaddingTop(2).Text($"REV · {year}")
                        .FontFamily(BodyFont).FontSize(8).FontColor(MutedHex).LetterSpacing(1f);
                });
                row.RelativeItem();
                row.AutoItem().AlignMiddle().Border(0.8f).BorderColor(PrimaryHex).PaddingHorizontal(12).PaddingVertical(6)
                    .Text(editionText).FontFamily(DisplayFont).FontSize(9).Bold().FontColor(PrimaryHex).LetterSpacing(0.6f);
            });

            col.Item().PaddingTop(10).LineHorizontal(0.8f).LineColor(PrimaryHex);

            // koordinat etiketleri (sayfa identity)
            col.Item().PaddingTop(6).Row(row =>
            {
                row.AutoItem().Text("A").FontFamily(BodyFont).FontSize(7).FontColor(MutedHex).LetterSpacing(1f);
                row.RelativeItem();
                row.AutoItem().Text("B").FontFamily(BodyFont).FontSize(7).FontColor(MutedHex).LetterSpacing(1f);
                row.RelativeItem();
                row.AutoItem().Text("C").FontFamily(BodyFont).FontSize(7).FontColor(MutedHex).LetterSpacing(1f);
            });

            // orta: başlık (sol) + vana sembolü (sağ)
            col.Item().PaddingTop(36).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(sectionLabel)
                        .FontFamily(DisplayFont).FontSize(10).FontColor(PrimaryHex).LetterSpacing(2.4f);
                    c.Item().PaddingTop(12).Text(bigTitle)
                        .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 40f, 14, 22f)).Bold().FontColor(TextHex).LetterSpacing(-0.02f);
                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        c.Item().PaddingTop(4).Text(subtitle)
                            .FontFamily(DisplayFont).FontSize(15).Light().FontColor(PrimaryHex);
                    }
                    c.Item().PaddingTop(14).Width(80).LineHorizontal(1.6f).LineColor(AccentHex);
                    if (!string.IsNullOrWhiteSpace(tagline))
                    {
                        c.Item().PaddingTop(10).Text(tagline)
                            .FontFamily(BodyFont).FontSize(11).FontColor(TextHex).LetterSpacing(0.3f);
                    }
                });
                row.ConstantItem(130).AlignMiddle().Svg(BuildValveTechSvg());
            });

            if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
            {
                col.Item().PaddingTop(26).Border(0.6f).BorderColor(BorderHex).Padding(14).Text(brand.About)
                    .FontFamily(BodyFont).FontSize(10).LineHeight(1.6f).FontColor(TextHex);
            }

            if (cover.ShowFeatures)
            {
                col.Item().PaddingTop(18).Row(row =>
                {
                    BuildFeatureItem(row, BuildFactoryIconSvg(), Fallback(cover.Feature1, "ISO 9001"));
                    BuildFeatureItem(row, BuildGearIconSvg(), Fallback(cover.Feature2, "CNC HASSAS"));
                    BuildFeatureItem(row, BuildShieldIconSvg(), Fallback(cover.Feature3, "PN16/40"));
                    BuildFeatureItem(row, BuildTruckIconSvg(), Fallback(cover.Feature4, "STOK"));
                });
            }

            col.Item().PaddingTop(16).LineHorizontal(0.8f).LineColor(PrimaryHex);

            // alt: technical metadata
            col.Item().PaddingTop(8).Row(row =>
            {
                row.AutoItem().Text("PROJ·001")
                    .FontFamily(BodyFont).FontSize(8).FontColor(MutedHex).LetterSpacing(1.2f);
                row.RelativeItem();
                row.AutoItem().Text($"DWG·{year}.{DateTime.Now.Month:00}")
                    .FontFamily(BodyFont).FontSize(8).FontColor(MutedHex).LetterSpacing(1.2f);
                row.RelativeItem();
                row.AutoItem().Text("SHEET 01 OF 01")
                    .FontFamily(BodyFont).FontSize(8).FontColor(MutedHex).LetterSpacing(1.2f);
            });

            if (cover.ShowContactBar)
            {
                col.Item().PaddingTop(12).Background(PrimaryHex).PaddingHorizontal(18).PaddingVertical(12).Row(row =>
                {
                    BuildContactItem(row, BuildPhoneIconSvg(), brand.Phone);
                    BuildContactDivider(row);
                    BuildContactItem(row, BuildEmailIconSvg(), brand.Email);
                    BuildContactDivider(row);
                    BuildContactItem(row, BuildWebIconSvg(), brand.Web);
                });
            }
        });
    }

    // AKIS — sayfayı kat eden bronz akış eğrisi, dinamik kompozisyon
    private static void RenderCoverAkis(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Layers(layers =>
        {
            // arka plan: akış eğrileri (tüm sayfa)
            layers.Layer().Svg(BuildFlowCurvesSvg());

            layers.PrimaryLayer().PaddingHorizontal(50).PaddingVertical(44).Column(col =>
            {
                // üst sol: brand kompakt
                col.Item().Row(row =>
                {
                    row.AutoItem().Element(c =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                            c.Width(40).Height(40).Image(brand.LogoPath).FitArea();
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                    row.AutoItem().PaddingLeft(14).AlignMiddle().Text(sectionLabel)
                        .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(MutedHex).LetterSpacing(2.4f);
                    row.RelativeItem();
                    row.AutoItem().AlignMiddle().Background(PrimaryHex).PaddingHorizontal(14).PaddingVertical(6)
                        .Text(editionText).FontFamily(DisplayFont).FontSize(9).Bold().FontColor(White).LetterSpacing(0.4f);
                });

                // başlık (sağa hizalı)
                col.Item().PaddingTop(70).AlignRight().Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 40f, 14, 22f)).Bold().FontColor(PrimaryHex).LetterSpacing(-0.02f);

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    col.Item().PaddingTop(6).AlignRight().Text(subtitle)
                        .FontFamily(DisplayFont).FontSize(17).Light().FontColor(AccentHex);
                }

                col.Item().PaddingTop(14).AlignRight().Width(80).LineHorizontal(1.4f).LineColor(PrimaryHex);

                if (!string.IsNullOrWhiteSpace(tagline))
                {
                    col.Item().PaddingTop(10).AlignRight().Text(tagline)
                        .FontFamily(BodyFont).FontSize(11.5f).Italic().FontColor(PrimaryHex).LetterSpacing(0.3f);
                }

                // sağ üst köşede büyük yıl (akış'ın hissini destekler)
                col.Item().PaddingTop(8).AlignRight().Text(year)
                    .FontFamily(DisplayFont).FontSize(54).Light().FontColor(AccentHex).LetterSpacing(-0.04f);

                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    col.Item().PaddingTop(28).PaddingRight(100).Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10.5f).LineHeight(1.65f).FontColor(TextHex);
                }

                if (cover.ShowFeatures)
                {
                    col.Item().PaddingTop(24).Row(row =>
                    {
                        BuildFeatureItem(row, BuildFactoryIconSvg(), Fallback(cover.Feature1, "KALİTE"));
                        BuildFeatureItem(row, BuildGearIconSvg(), Fallback(cover.Feature2, "UZMANLIK"));
                        BuildFeatureItem(row, BuildTruckIconSvg(), Fallback(cover.Feature3, "HIZLI TESLİMAT"));
                        BuildFeatureItem(row, BuildShieldIconSvg(), Fallback(cover.Feature4, "GÜVEN"));
                    });
                }

                if (cover.ShowContactBar)
                {
                    col.Item().PaddingTop(24).Background(PrimaryHex).Padding(14).Row(row =>
                    {
                        BuildContactItem(row, BuildPhoneIconSvg(), brand.Phone);
                        BuildContactDivider(row);
                        BuildContactItem(row, BuildEmailIconSvg(), brand.Email);
                        BuildContactDivider(row);
                        BuildContactItem(row, BuildWebIconSvg(), brand.Web);
                    });
                }
            });
        });
    }

    // YATAY BANT — orta yarısı kaplayan koyu bant, editöryel sinematik
    private static void RenderCoverYatayBant(PageDescriptor page, BrandInfo brand, CoverPageInfo cover)
    {
        var (bigTitle, subtitle, tagline, editionText, sectionLabel, year) = BuildCoverStrings(brand, cover);

        page.Content().Column(col =>
        {
            // ÜST krem alan — auto height
            col.Item().PaddingHorizontal(50).PaddingTop(40).PaddingBottom(20).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.AutoItem().Element(e =>
                    {
                        if (brand.ShowLogoOnCover && !string.IsNullOrWhiteSpace(brand.LogoPath) && File.Exists(brand.LogoPath))
                            e.Width(46).Height(46).Image(brand.LogoPath).FitArea();
                        else { /* Cover badge fallback removed 2026-05-22 — ShowLogoOnCover=false durumunda hicbir badge gosterilmez */ }
                    });
                    row.AutoItem().PaddingLeft(16).AlignMiddle().Column(cc =>
                    {
                        cc.Item().Text(brand.Name?.ToUpper(TrCulture) ?? "")
                            .FontFamily(DisplayFont).FontSize(12).Bold().FontColor(PrimaryHex).LetterSpacing(0.4f);
                        cc.Item().PaddingTop(2).Text(sectionLabel)
                            .FontFamily(DisplayFont).FontSize(9).FontColor(MutedHex).LetterSpacing(2.4f);
                    });
                    row.RelativeItem();
                    row.AutoItem().AlignMiddle().Text(year)
                        .FontFamily(DisplayFont).FontSize(48).Light().FontColor(AccentHex).LetterSpacing(-0.04f);
                });

                c.Item().PaddingTop(14).LineHorizontal(0.8f).LineColor(BorderHex);
            });

            // ORTA koyu bant (büyük başlık burada)
            col.Item().Background(PrimaryHex).PaddingHorizontal(50).PaddingVertical(56).Column(c =>
            {
                c.Item().Text(editionText)
                    .FontFamily(DisplayFont).FontSize(10).Bold().FontColor(AccentHex).LetterSpacing(2.6f);

                c.Item().PaddingTop(18).Text(bigTitle)
                    .FontFamily(DisplayFont).FontSize(AutoFitFontSize(bigTitle, 56f, 14, 28f)).Bold().FontColor(White).LetterSpacing(-0.025f);

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    c.Item().PaddingTop(6).Text(subtitle)
                        .FontFamily(DisplayFont).FontSize(22).Light().FontColor(AccentHex);
                }

                c.Item().PaddingTop(20).Width(100).LineHorizontal(1.6f).LineColor(AccentHex);

                if (!string.IsNullOrWhiteSpace(tagline))
                {
                    c.Item().PaddingTop(14).Text(tagline)
                        .FontFamily(BodyFont).FontSize(12).Italic().FontColor(AccentHex).LetterSpacing(0.3f);
                }
            });

            // ALT krem alan
            col.Item().PaddingHorizontal(50).PaddingTop(28).PaddingBottom(36).Column(c =>
            {
                if (cover.ShowAbout && !string.IsNullOrWhiteSpace(brand.About))
                {
                    c.Item().Text(brand.About)
                        .FontFamily(BodyFont).FontSize(10.5f).LineHeight(1.65f).FontColor(TextHex);
                }

                if (cover.ShowFeatures)
                {
                    c.Item().PaddingTop(20).Row(row =>
                    {
                        BuildFeatureItem(row, BuildFactoryIconSvg(), Fallback(cover.Feature1, "KALİTE"));
                        BuildFeatureItem(row, BuildGearIconSvg(), Fallback(cover.Feature2, "UZMANLIK"));
                        BuildFeatureItem(row, BuildTruckIconSvg(), Fallback(cover.Feature3, "HIZLI TESLİMAT"));
                        BuildFeatureItem(row, BuildShieldIconSvg(), Fallback(cover.Feature4, "GÜVEN"));
                    });
                }

                if (cover.ShowContactBar)
                {
                    c.Item().PaddingTop(22).LineHorizontal(0.8f).LineColor(BorderHex);
                    c.Item().PaddingTop(12).Row(row =>
                    {
                        BuildMinimalContactItem(row, brand.Phone);
                        BuildMinimalContactItem(row, brand.Email);
                        BuildMinimalContactItem(row, brand.Web);
                    });
                }
            });
        });
    }

    // ---- Yeni SVG helper'ları ----

    private static string BuildBlueprintGridSvg()
    {
        // A4 595x842pt boyutunda subtle grid pattern
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 595 842'>");
        sb.Append($"<rect width='595' height='842' fill='{White}'/>");
        // dikey çizgiler her 40pt
        for (int x = 40; x < 595; x += 40)
            sb.Append($"<line x1='{x}' y1='0' x2='{x}' y2='842' stroke='{PrimaryHex}' stroke-width='0.15' opacity='0.18'/>");
        // yatay çizgiler her 40pt
        for (int y = 40; y < 842; y += 40)
            sb.Append($"<line x1='0' y1='{y}' x2='595' y2='{y}' stroke='{PrimaryHex}' stroke-width='0.15' opacity='0.18'/>");
        // major grid her 200pt
        for (int x = 200; x < 595; x += 200)
            sb.Append($"<line x1='{x}' y1='0' x2='{x}' y2='842' stroke='{PrimaryHex}' stroke-width='0.35' opacity='0.28'/>");
        for (int y = 200; y < 842; y += 200)
            sb.Append($"<line x1='0' y1='{y}' x2='595' y2='{y}' stroke='{PrimaryHex}' stroke-width='0.35' opacity='0.28'/>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string BuildBlueprintRulerSvg()
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 842'>");
        // her 20pt'de tick
        for (int y = 20; y < 842; y += 20)
        {
            int len = (y % 100 == 0) ? 14 : (y % 40 == 0 ? 8 : 4);
            sb.Append($"<line x1='0' y1='{y}' x2='{len}' y2='{y}' stroke='{PrimaryHex}' stroke-width='0.6' opacity='0.55'/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string BuildValveTechSvg()
    {
        // basit teknik vana sembolü: gövde + iki flanş + el çarkı
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 140 140'>");
        // sol flanş
        sb.Append($"<rect x='6' y='52' width='10' height='36' fill='none' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        // sol boru
        sb.Append($"<rect x='16' y='62' width='28' height='16' fill='none' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        // gövde (orta)
        sb.Append($"<path d='M44 70 L60 50 L80 50 L96 70 L80 90 L60 90 Z' fill='none' stroke='{PrimaryHex}' stroke-width='1.8'/>");
        // sağ boru
        sb.Append($"<rect x='96' y='62' width='28' height='16' fill='none' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        // sağ flanş
        sb.Append($"<rect x='124' y='52' width='10' height='36' fill='none' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        // mil
        sb.Append($"<line x1='70' y1='50' x2='70' y2='22' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        // el çarkı
        sb.Append($"<circle cx='70' cy='20' r='14' fill='none' stroke='{PrimaryHex}' stroke-width='1.6'/>");
        sb.Append($"<line x1='56' y1='20' x2='84' y2='20' stroke='{PrimaryHex}' stroke-width='1.2'/>");
        sb.Append($"<line x1='70' y1='6' x2='70' y2='34' stroke='{PrimaryHex}' stroke-width='1.2'/>");
        // ölçü çizgileri
        sb.Append($"<line x1='6' y1='105' x2='134' y2='105' stroke='{AccentHex}' stroke-width='0.4'/>");
        sb.Append($"<text x='70' y='118' font-family='monospace' font-size='8' fill='{MutedHex}' text-anchor='middle'>DN 50</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string BuildFlowCurvesSvg()
    {
        // sayfayı kat eden bronz akış eğrileri (alt sol → üst sağ)
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 595 842'>");
        // ana eğri (kalın)
        sb.Append($"<path d='M -20 720 Q 200 580 320 480 T 620 200' fill='none' stroke='{AccentHex}' stroke-width='2.6' opacity='0.85'/>");
        // ikinci eğri (orta)
        sb.Append($"<path d='M -20 760 Q 220 620 360 500 T 620 240' fill='none' stroke='{AccentHex}' stroke-width='1.2' opacity='0.55'/>");
        // üçüncü eğri (ince)
        sb.Append($"<path d='M -20 800 Q 240 660 400 520 T 620 280' fill='none' stroke='{AccentHex}' stroke-width='0.7' opacity='0.35'/>");
        // dördüncü eğri (ince yukarıda)
        sb.Append($"<path d='M -20 680 Q 180 540 280 460 T 620 160' fill='none' stroke='{AccentHex}' stroke-width='0.7' opacity='0.35'/>");
        // alt-sol bronz blob (dolgu)
        sb.Append($"<circle cx='-30' cy='800' r='180' fill='{PrimaryHex}' opacity='0.06'/>");
        sb.Append($"<circle cx='620' cy='180' r='160' fill='{PrimaryHex}' opacity='0.04'/>");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
