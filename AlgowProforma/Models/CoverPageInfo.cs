using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

public enum CoverImageMode
{
    // Görsel tam sayfa (A4) — kullanıcı A4 oranında kırpar, sıfır boşluk
    FullPage = 0,
    // Görsel üst 2/3, altta marka bilgi bandı (logo + slogan + iletişim)
    TopWithBrandBar = 1,
    // Görsel tam sayfa arka plan + üstte metin overlay (başlık, slogan, sektör)
    BackgroundWithOverlay = 2,
}

public partial class CoverPageInfo : ObservableObject
{
    [ObservableProperty] private string _customCoverImagePath = string.Empty;
    [ObservableProperty] private CoverImageMode _customCoverImageMode = CoverImageMode.FullPage;
    // Free-form designer'daki elementler. Boşsa legacy render (mevcut 10 design). Doluysa RenderFreeFormCover.
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<CoverElement> _elements = new();
    public bool IsFreeFormMode => Elements.Count > 0;
    [ObservableProperty] private string _mainTitle = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _yearText = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;  // opsiyonel, dolduruluyorsa yıl yerine basılır (örn: "Mayıs 2026", "Kasım 2026 Edisyonu")
    [ObservableProperty] private string _editionLabel = string.Empty;
    [ObservableProperty] private string _sectionLabel = "ÜRÜN KATALOĞU";
    [ObservableProperty] private bool _showAbout = true;
    [ObservableProperty] private bool _showFeatures = true;
    [ObservableProperty] private string _feature1 = "KALİTE";
    [ObservableProperty] private string _feature2 = "UZMANLIK";
    [ObservableProperty] private string _feature3 = "HIZLI TESLİMAT";
    [ObservableProperty] private string _feature4 = "GÜVEN";
    [ObservableProperty] private bool _showContactBar = true;
}
