using System.Collections.Generic;

namespace AlgowProforma.Models;

public enum CoverLayout
{
    Klasik,    // dişli + lacivert panel + büyük yıl
    Egri,      // iç içe iki ark, sağ üstte şerit deseni
    Cerceve,   // köşe üçgenleri + iç eğik çerçeve
    Geometri,  // keskin geometrik poligonlar
    Madalya,   // ortada beyaz panel + yan kanatlar
    Katman,    // diyagonal katmanlı düzlemler
    Mozaik,    // sol koyu blok + sağda kesişen poligonlar
    Diyagonal, // sağ üst diyagonal şerit + kavis + dikey çizgili bant
    Minimalist, // sade tipografi, geniş whitespace, ince hat aksanı
    Sertifika,  // çift çizgi çerçeve + köşe motifleri, ortalı klasik panel
    Blueprint,  // mühendislik teknik çizim — koordinat etiketleri, metadata, vana sembolü
    Akis,       // sayfayı kat eden akış eğrileri, sağa hizalı dinamik kompozisyon
    YatayBant,  // ortada koyu bant — editöryel sinematik, krem üst/alt
}

public enum CardStyle
{
    Standard,
}

public enum HeaderStyle
{
    Filled,
    Slim,
}

public class PdfDesign
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    public int ProductsPerRow { get; init; } = 3;
    public int ProductsPerPage { get; init; } = 9;

    public CoverLayout CoverLayout { get; init; } = CoverLayout.Klasik;
    public bool ShowGearOnCover { get; init; } = true;
    public bool ShowAccentDecorationOnCover { get; init; } = true;
    public bool ShowYearLargeOnCover { get; init; } = true;

    public CardStyle CardStyle { get; init; } = CardStyle.Standard;
    public HeaderStyle HeaderStyle { get; init; } = HeaderStyle.Filled;

    public static PdfDesign Default => Klasik;

    public static PdfDesign Klasik { get; } = new()
    {
        Id = "klasik",
        Name = "Klasik",
        Description = "Endüstriyel — blueprint dişli, lacivert şeritli kart",
        CoverLayout = CoverLayout.Klasik,
        ShowGearOnCover = true,
        ShowAccentDecorationOnCover = true,
        ShowYearLargeOnCover = true,
    };

    public static PdfDesign Egri { get; } = new()
    {
        Id = "egri",
        Name = "Eğri",
        Description = "Üst üste binen iki ark, şerit deseni — minimal kompozisyon",
        CoverLayout = CoverLayout.Egri,
    };

    public static PdfDesign Cerceve { get; } = new()
    {
        Id = "cerceve",
        Name = "Çerçeve",
        Description = "Köşe üçgenleri + iç eğik çift çizgi çerçeve — sertifika hissi",
        CoverLayout = CoverLayout.Cerceve,
    };

    public static PdfDesign Geometri { get; } = new()
    {
        Id = "geometri",
        Name = "Geometri",
        Description = "Keskin poligonlar, dik kontrast — modern grafik",
        CoverLayout = CoverLayout.Geometri,
    };

    public static PdfDesign Madalya { get; } = new()
    {
        Id = "madalya",
        Name = "Madalya",
        Description = "Ortada panel + yan kanatlar — flama / sancak",
        CoverLayout = CoverLayout.Madalya,
    };

    public static PdfDesign Katman { get; } = new()
    {
        Id = "katman",
        Name = "Katman",
        Description = "Diyagonal kesilmiş katmanlar — mimari & malzeme",
        CoverLayout = CoverLayout.Katman,
    };

    public static PdfDesign Mozaik { get; } = new()
    {
        Id = "mozaik",
        Name = "Mozaik",
        Description = "Sol koyu blok + sağda kesişen geometrik parçalar — endüstriyel grafik",
        CoverLayout = CoverLayout.Mozaik,
    };

    public static PdfDesign Diyagonal { get; } = new()
    {
        Id = "diyagonal",
        Name = "Diyagonal",
        Description = "Üstte eğik şerit, sağda dikey çizgili bant + alt blok — karma kompozisyon",
        CoverLayout = CoverLayout.Diyagonal,
    };

    public static PdfDesign Minimalist { get; } = new()
    {
        Id = "minimalist",
        Name = "Minimalist",
        Description = "Sade tipografi, geniş whitespace, ince hat — premium minimalist",
        CoverLayout = CoverLayout.Minimalist,
        ShowGearOnCover = false,
        ShowAccentDecorationOnCover = false,
        ShowYearLargeOnCover = false,
    };

    public static PdfDesign Sertifika { get; } = new()
    {
        Id = "sertifika",
        Name = "Sertifika",
        Description = "Çift çizgi çerçeve, köşe motifleri — klasik kurumsal prestij",
        CoverLayout = CoverLayout.Sertifika,
        ShowGearOnCover = false,
        ShowAccentDecorationOnCover = true,
        ShowYearLargeOnCover = false,
    };

    public static PdfDesign Blueprint { get; } = new()
    {
        Id = "blueprint",
        Name = "Teknik Çizim",
        Description = "Mühendislik blueprint — koordinat etiketleri, teknik metadata, vana sembolü",
        CoverLayout = CoverLayout.Blueprint,
        ShowGearOnCover = false,
        ShowAccentDecorationOnCover = true,
        ShowYearLargeOnCover = false,
    };

    public static PdfDesign Akis { get; } = new()
    {
        Id = "akis",
        Name = "Akış",
        Description = "Sayfayı kat eden akış eğrileri, sağa hizalı dinamik kompozisyon + büyük yıl",
        CoverLayout = CoverLayout.Akis,
    };

    public static PdfDesign YatayBant { get; } = new()
    {
        Id = "yatay-bant",
        Name = "Yatay Bant",
        Description = "Ortada koyu bant — editöryel sinematik, krem üst/alt + büyük başlık",
        CoverLayout = CoverLayout.YatayBant,
    };

    public static IReadOnlyList<PdfDesign> All { get; } = new List<PdfDesign>
    {
        Klasik,
        Egri,
        Cerceve,
        Geometri,
        Madalya,
        Katman,
        Mozaik,
        Diyagonal,
        Minimalist,
        Sertifika,
        Blueprint,
        Akis,
        YatayBant,
    };

    public static PdfDesign GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Default;
        foreach (var d in All)
            if (d.Id == id) return d;
        return Default;
    }
}
