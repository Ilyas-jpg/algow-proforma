using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

/// <summary>Toplu gönderimde tek alıcı satırı (önizleme + durum takibi).</summary>
public partial class BulkRecipient : ObservableObject
{
    [ObservableProperty] private bool _selected = true;
    [ObservableProperty] private string _company = "";
    [ObservableProperty] private string _contactName = "";
    [ObservableProperty] private Salutation _salutation = Salutation.Bey;
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _status = "Hazır";

    public string TargetName => string.IsNullOrWhiteSpace(Company) ? ContactName : Company;
}
