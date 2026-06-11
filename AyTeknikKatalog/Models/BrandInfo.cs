using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

public partial class BrandInfo : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _tagline = "";
    [ObservableProperty] private string _about = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _web = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _logoPath = "";
    // İçerik sayfalarında çapraz watermark (örn: "DRAFT", "ÖRNEKTİR", "FİRMA 2026"). Boş ise watermark yok.
    [ObservableProperty] private string _watermarkText = "";
    // Logo etrafında beyaz dikdörtgen çerçeve. False (default): logo direkt görsel; true: beyaz kart içinde (önceki davranış).
    [ObservableProperty] private bool _showLogoBackground;
    // Kapak sayfasında üst köşedeki marka logosu görünsün mü.
    // False (default, 2026-05-22 kullanıcı kararı): kapak temiz, hiçbir badge çizilmez (logo da initials da yok).
    // True: kapakta logo görünür; false ise kapak daha temiz kalır.
    [ObservableProperty] private bool _showLogoOnCover;
}
