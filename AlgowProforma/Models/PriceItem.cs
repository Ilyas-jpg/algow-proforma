using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

/// <summary>
/// Fiyat havuzu (price book) ana kaydı. Tekliflerde satır kaynağıdır.
/// Excel'den içe/dışa aktarılır, elle düzenlenir, toplu güncellenir (% zam / kur).
/// Birim fiyatlar KDV HARİÇ (net) tutulur — B2B proforma normu.
/// </summary>
public partial class PriceItem : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _unit = "adet";
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private string _currency = "TL";
    /// <summary>KDV oranı yüzde (Türkiye default 20).</summary>
    [ObservableProperty] private decimal _vatRate = 20m;
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _imagePath = "";
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private DateTime _updatedAt = DateTime.Now;

    public PriceItem Clone() => new()
    {
        Id = Id,
        Code = Code,
        Name = Name,
        Description = Description,
        Unit = Unit,
        UnitPrice = UnitPrice,
        Currency = Currency,
        VatRate = VatRate,
        Category = Category,
        ImagePath = ImagePath,
        IsActive = IsActive,
        UpdatedAt = UpdatedAt,
    };

    /// <summary>Havuz kaydından teklif satırı üretir (snapshot — sonradan havuz değişse teklif sabit kalır).</summary>
    public QuoteLine ToQuoteLine(decimal quantity = 1m) => new()
    {
        PriceItemId = Id,
        Code = Code,
        Name = Name,
        Description = Description,
        Unit = Unit,
        Quantity = quantity,
        UnitPrice = UnitPrice,
        VatRate = VatRate,
    };
}
