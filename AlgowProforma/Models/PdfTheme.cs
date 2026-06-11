using System.Collections.Generic;

namespace AlgowProforma.Models;

public class PdfTheme
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    // Renk paleti — boş bırakılırsa PdfService varsayılan renklerini kullanır
    public string PrimaryHex { get; init; } = "";
    public string SecondaryHex { get; init; } = "";
    public string AccentHex { get; init; } = "";
    public string SurfaceHex { get; init; } = "";
    public string BorderHex { get; init; } = "";
    public string TextHex { get; init; } = "";
    public string MutedHex { get; init; } = "";

    public static PdfTheme Default { get; } = new()
    {
        Id = "klasik",
        Name = "Klasik",
        Description = "Lacivert + altın aksan — varsayılan tasarım",
        PrimaryHex = "#2E338F",
        SecondaryHex = "#5368A6",
        AccentHex = "#7C84C7",
        SurfaceHex = "#F5F5F7",
        BorderHex = "#E5E5E7",
        TextHex = "#1A1A1F",
        MutedHex = "#818285",
    };

    public static IReadOnlyList<PdfTheme> All { get; } = new List<PdfTheme>
    {
        Default,
        new()
        {
            Id = "canli-bakir",
            Name = "Canlı Bakır",
            Description = "Canlı bakır + sıcak krem — endüstriyel premium",
            PrimaryHex = "#C2410C",
            SecondaryHex = "#9A3412",
            AccentHex = "#EA8A4B",
            SurfaceHex = "#FAF5EF",
            BorderHex = "#ECDCC9",
            TextHex = "#1A1A1F",
            MutedHex = "#7A6F5F",
        },
        new()
        {
            Id = "petrol",
            Name = "Petrol",
            Description = "Derin petrol mavisi — kurumsal ve modern",
            PrimaryHex = "#0F3D52",
            SecondaryHex = "#2C5C75",
            AccentHex = "#6B92AB",
            SurfaceHex = "#F0F4F7",
            BorderHex = "#DCE2E8",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "bordo",
            Name = "Bordo",
            Description = "Şarap kırmızısı — sıcak ve klasik",
            PrimaryHex = "#6B1F2A",
            SecondaryHex = "#8E3849",
            AccentHex = "#B86A78",
            SurfaceHex = "#FDF5F5",
            BorderHex = "#E8D8DA",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "yesil",
            Name = "Yeşil",
            Description = "Orman yeşili — doğal ve dengeli",
            PrimaryHex = "#1F4F38",
            SecondaryHex = "#3E7656",
            AccentHex = "#82A893",
            SurfaceHex = "#F4F8F5",
            BorderHex = "#DCE4DE",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "antrasit",
            Name = "Antrasit",
            Description = "Koyu gri — sade ve profesyonel",
            PrimaryHex = "#2A2D33",
            SecondaryHex = "#4A4D55",
            AccentHex = "#8A8E97",
            SurfaceHex = "#F5F5F6",
            BorderHex = "#E5E5E7",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "bakir",
            Name = "Bakır",
            Description = "Bakır kahve + gül altın — sıcak premium",
            PrimaryHex = "#8B4423",
            SecondaryHex = "#B5683E",
            AccentHex = "#D4A07A",
            SurfaceHex = "#FAF5F1",
            BorderHex = "#EDE2D8",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "mor",
            Name = "Mor",
            Description = "Kraliyet moru — şık ve dikkat çekici",
            PrimaryHex = "#4A2D72",
            SecondaryHex = "#6B4A98",
            AccentHex = "#9F87C2",
            SurfaceHex = "#F7F4FA",
            BorderHex = "#E5DFEC",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "turkuaz",
            Name = "Turkuaz",
            Description = "Derin teal — temiz ve ferah",
            PrimaryHex = "#1F5862",
            SecondaryHex = "#3F8290",
            AccentHex = "#7FB3BF",
            SurfaceHex = "#F2F7F8",
            BorderHex = "#DCE5E7",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "gece",
            Name = "Gece",
            Description = "Gece siyahı + elektrik mavisi — gizemli",
            PrimaryHex = "#0A0E2A",
            SecondaryHex = "#1F2660",
            AccentHex = "#5B6CC9",
            SurfaceHex = "#F2F3F8",
            BorderHex = "#DDE0EA",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "sahil",
            Name = "Sahil",
            Description = "Gök mavisi — açık ve ferah",
            PrimaryHex = "#2B6CB0",
            SecondaryHex = "#5599D5",
            AccentHex = "#93C5E8",
            SurfaceHex = "#F4F8FC",
            BorderHex = "#DDE7F0",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "toprak",
            Name = "Toprak",
            Description = "Terracotta turuncu-kahve — sıcak ve doğal",
            PrimaryHex = "#A6502E",
            SecondaryHex = "#C97B5A",
            AccentHex = "#DDA887",
            SurfaceHex = "#FBF5F1",
            BorderHex = "#EBDDD3",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "zeytin",
            Name = "Zeytin",
            Description = "Zeytin yeşili — sakin ve toprak tonu",
            PrimaryHex = "#5C6B2F",
            SecondaryHex = "#7E8E4D",
            AccentHex = "#B7C384",
            SurfaceHex = "#F8F8EE",
            BorderHex = "#E5E5D2",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "gul",
            Name = "Gül",
            Description = "Tozlu gül pembesi — şık ve modern",
            PrimaryHex = "#A04060",
            SecondaryHex = "#C2658A",
            AccentHex = "#DDA1B4",
            SurfaceHex = "#FBF4F6",
            BorderHex = "#EFD8DE",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "siyah",
            Name = "Saf Siyah",
            Description = "Siyah + gümüş — minimalist lüks",
            PrimaryHex = "#0F0F12",
            SecondaryHex = "#2D2D33",
            AccentHex = "#6E6E78",
            SurfaceHex = "#F2F2F4",
            BorderHex = "#DEDEE2",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "kahve",
            Name = "Kahve",
            Description = "Koyu kahve + sütlü ton — sıcak ve doygun",
            PrimaryHex = "#3D2E20",
            SecondaryHex = "#6B4F38",
            AccentHex = "#A88B6F",
            SurfaceHex = "#F8F4EE",
            BorderHex = "#E8DCCD",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "lila",
            Name = "Lila",
            Description = "Açık lila + lavanta — yumuşak ve zarif",
            PrimaryHex = "#7E5F9A",
            SecondaryHex = "#A084BD",
            AccentHex = "#C7B3D8",
            SurfaceHex = "#F9F6FB",
            BorderHex = "#E8DFEE",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "sampanya",
            Name = "Şampanya",
            Description = "Şampanya altını — premium parlaklık",
            PrimaryHex = "#8E7233",
            SecondaryHex = "#B89A57",
            AccentHex = "#DDC78A",
            SurfaceHex = "#FAF7EE",
            BorderHex = "#EDE5CD",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "okyanus",
            Name = "Okyanus",
            Description = "Derin okyanus mavisi — güçlü ve serin",
            PrimaryHex = "#003E5C",
            SecondaryHex = "#0E6585",
            AccentHex = "#5BA0B8",
            SurfaceHex = "#F1F6F9",
            BorderHex = "#D8E3EA",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "kömür",
            Name = "Kömür",
            Description = "Yumuşak gri tonlar — modern siyah-beyaz",
            PrimaryHex = "#3A3F47",
            SecondaryHex = "#5C6168",
            AccentHex = "#9DA2AB",
            SurfaceHex = "#F4F5F7",
            BorderHex = "#E1E3E7",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "fistik",
            Name = "Fıstık",
            Description = "Fıstık yeşili — taze ve yumuşak",
            PrimaryHex = "#3F6B4A",
            SecondaryHex = "#6E9970",
            AccentHex = "#A8C9A0",
            SurfaceHex = "#F4F8F4",
            BorderHex = "#DEE7DE",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "kor",
            Name = "Kor",
            Description = "Kor kırmızısı + gül — yoğun ve sıcak",
            PrimaryHex = "#8B1F26",
            SecondaryHex = "#B14048",
            AccentHex = "#D67D80",
            SurfaceHex = "#FBF4F4",
            BorderHex = "#EFD7D8",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
        new()
        {
            Id = "indigo",
            Name = "İndigo",
            Description = "Yoğun indigo + menekşe — modern ve odaklı",
            PrimaryHex = "#2C2978",
            SecondaryHex = "#4F4BA8",
            AccentHex = "#8783D2",
            SurfaceHex = "#F4F3FA",
            BorderHex = "#DCDAEC",
            TextHex = "#1A1A1F",
            MutedHex = "#818285",
        },
    };

    public static PdfTheme GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Default;
        foreach (var t in All)
            if (t.Id == id) return t;
        return Default;
    }
}
