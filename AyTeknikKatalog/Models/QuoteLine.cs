using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

/// <summary>
/// Teklif satırı (kalem). Birim fiyat KDV HARİÇ (net). Satır iskontosu yüzde olarak.
/// LineNet/LineVat/LineGross computed — girdi değişince [NotifyPropertyChangedFor] ile canlı güncellenir.
/// </summary>
public partial class QuoteLine : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    /// <summary>Fiyat havuzu bağı (opsiyonel — elle eklenen satırda boş).</summary>
    [ObservableProperty] private string _priceItemId = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _unit = "adet";

    [NotifyPropertyChangedFor(nameof(LineNet))]
    [NotifyPropertyChangedFor(nameof(LineVat))]
    [NotifyPropertyChangedFor(nameof(LineGross))]
    [ObservableProperty] private decimal _quantity = 1m;

    [NotifyPropertyChangedFor(nameof(LineNet))]
    [NotifyPropertyChangedFor(nameof(LineVat))]
    [NotifyPropertyChangedFor(nameof(LineGross))]
    [ObservableProperty] private decimal _unitPrice;

    /// <summary>Satır iskontosu yüzde (0-100).</summary>
    [NotifyPropertyChangedFor(nameof(LineNet))]
    [NotifyPropertyChangedFor(nameof(LineVat))]
    [NotifyPropertyChangedFor(nameof(LineGross))]
    [ObservableProperty] private decimal _discountPct;

    /// <summary>KDV oranı yüzde (default 20).</summary>
    [NotifyPropertyChangedFor(nameof(LineVat))]
    [NotifyPropertyChangedFor(nameof(LineGross))]
    [ObservableProperty] private decimal _vatRate = 20m;

    /// <summary>İskontolu net satır tutarı (KDV hariç).</summary>
    public decimal LineNet =>
        Math.Round(Quantity * UnitPrice * (1m - DiscountPct / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Satır KDV tutarı (genel iskonto HARİÇ — genel iskonto Quote seviyesinde oransal düşülür).</summary>
    public decimal LineVat =>
        Math.Round(LineNet * VatRate / 100m, 2, MidpointRounding.AwayFromZero);

    public decimal LineGross => LineNet + LineVat;

    public QuoteLine Clone() => new()
    {
        Id = Id,
        PriceItemId = PriceItemId,
        Code = Code,
        Name = Name,
        Description = Description,
        Unit = Unit,
        Quantity = Quantity,
        UnitPrice = UnitPrice,
        DiscountPct = DiscountPct,
        VatRate = VatRate,
    };
}
