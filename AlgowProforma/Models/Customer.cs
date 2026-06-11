using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

/// <summary>Hitap — mail merge selamında kullanılır (Merhaba Ahmet Bey / Hanım / Merhabalar).</summary>
public enum Salutation { Yok, Bey, Hanim }

/// <summary>
/// Müşteri / kişi kaydı (CRM-lite). Excel'den içe/dışa aktarılır, elle düzenlenir.
/// Teklife eklenince bilgileri Quote'a snapshot'lanır (teklif sabit kalsın diye).
/// </summary>
public partial class Customer : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _contactName = "";
    [ObservableProperty] private Salutation _salutation = Salutation.Bey;
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _taxOffice = "";
    [ObservableProperty] private string _taxNumber = "";
    /// <summary>Virgülle ayrılmış serbest etiket/segment (örn: "bayi, kırıkkale").</summary>
    [ObservableProperty] private string _tags = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private DateTime _createdAt = DateTime.Now;

    /// <summary>Görünen ad — listede/teklifte. Firma yoksa kişi adına düşer.</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CompanyName) ? CompanyName :
        !string.IsNullOrWhiteSpace(ContactName) ? ContactName : "(isimsiz)";

    public Customer Clone() => new()
    {
        Id = Id,
        CompanyName = CompanyName,
        ContactName = ContactName,
        Salutation = Salutation,
        Email = Email,
        Phone = Phone,
        Address = Address,
        TaxOffice = TaxOffice,
        TaxNumber = TaxNumber,
        Tags = Tags,
        Notes = Notes,
        CreatedAt = CreatedAt,
    };
}
