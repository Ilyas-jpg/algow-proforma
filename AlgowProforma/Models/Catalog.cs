using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

public partial class Catalog : ObservableObject
{
    [ObservableProperty] private BrandInfo _brand = new();
    [ObservableProperty] private CoverPageInfo _cover = new();
    [ObservableProperty] private ObservableCollection<Reference> _references = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private DateTime _lastModified = DateTime.Now;
    [ObservableProperty] private string _libraryLabel = string.Empty;
    [ObservableProperty] private string _themeId = PdfTheme.Default.Id;
    [ObservableProperty] private string _designId = PdfDesign.Default.Id;
    [ObservableProperty] private string _layoutId = PageLayout.Default.Id;
    [ObservableProperty] private bool _skipReferencesPage;
    [ObservableProperty] private bool _useCustomPageLayouts;
    [ObservableProperty] private ObservableCollection<CustomPageEntry> _customPages = new();

    // White-label: yeni katalog boş/nötr başlar. Marka kimliği (ad, logo, iletişim, slogan)
    // kullanıcı tarafından "Marka Bilgileri" sekmesinden girilir — hiçbir müşteriye sabitlenmez.
    // Tema varsayılanı = PdfTheme.Default (klasik). Kapak etiketleri jenerik örnek olarak bırakıldı.
    public static Catalog CreateDefault() => new()
    {
        Brand = new BrandInfo
        {
            Name = "",
            Tagline = "",
            About = "",
            Phone = "",
            Email = "",
            Web = "",
            Address = "",
            LogoPath = "",
        },
        Cover = new CoverPageInfo
        {
            SectionLabel = "ÜRÜN KATALOĞU",
            Feature1 = "KALİTE",
            Feature2 = "HASSASİYET",
            Feature3 = "DAYANIKLILIK",
            Feature4 = "GÜVEN",
        },
    };

}
