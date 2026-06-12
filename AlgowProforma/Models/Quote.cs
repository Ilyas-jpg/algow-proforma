using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

/// <summary>Genel (teklif geneli) iskonto modu.</summary>
public enum DiscountMode { Yok, Yuzde, Tutar }

/// <summary>Teklif yaşam döngüsü durumu — pano + takip. JSON'a sayı yazılır; eski kayıtlar Taslak okunur.</summary>
public enum QuoteStatus { Taslak = 0, Gonderildi = 1, Onaylandi = 2, Reddedildi = 3 }

/// <summary>Durum → Türkçe etiket (ComboBox kaynağı). Enum adında Türkçe karakter kullanılamadığından ayrı.</summary>
public sealed record QuoteStatusOption(QuoteStatus Value, string Label)
{
    public static readonly System.Collections.Generic.IReadOnlyList<QuoteStatusOption> All = new[]
    {
        new QuoteStatusOption(QuoteStatus.Taslak, "Taslak"),
        new QuoteStatusOption(QuoteStatus.Gonderildi, "Gönderildi"),
        new QuoteStatusOption(QuoteStatus.Onaylandi, "Onaylandı"),
        new QuoteStatusOption(QuoteStatus.Reddedildi, "Reddedildi"),
    };

    public static string LabelOf(QuoteStatus s) => s switch
    {
        QuoteStatus.Gonderildi => "Gönderildi",
        QuoteStatus.Onaylandi => "Onaylandı",
        QuoteStatus.Reddedildi => "Reddedildi",
        _ => "Taslak",
    };
}

/// <summary>Teklif PDF şablonu seçenekleri (Quote.TemplateId). Bilinmeyen id → modern.</summary>
public sealed record QuoteTemplateOption(string Id, string Label)
{
    public static readonly System.Collections.Generic.IReadOnlyList<QuoteTemplateOption> All = new[]
    {
        new QuoteTemplateOption("modern", "Modern (varsayılan)"),
        new QuoteTemplateOption("kompakt", "Kompakt — dar kenar, küçük punto"),
        new QuoteTemplateOption("sade", "Sade — tek renk, dolgusuz"),
    };
}

/// <summary>Teklif PDF dili seçenekleri (Quote.Language).</summary>
public sealed record QuoteLanguageOption(string Id, string Label)
{
    public static readonly System.Collections.Generic.IReadOnlyList<QuoteLanguageOption> All = new[]
    {
        new QuoteLanguageOption("tr", "Türkçe"),
        new QuoteLanguageOption("en", "English (proforma invoice)"),
    };
}

/// <summary>
/// Fiyat teklifi (proforma). Müşteri bilgileri SNAPSHOT'lanır (teklif anındaki hâl sabit kalır,
/// sonradan müşteri/havuz değişse teklif bozulmaz). Toplamlar <see cref="Totals"/> üzerinden
/// QuoteCalculator'dan okunur. Satır/iskonto değişince VM <see cref="NotifyTotalsChanged"/> çağırır.
/// </summary>
public partial class Quote : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _quoteNo = "";
    /// <summary>0 = ilk teklif, 1 = rev.1, ...</summary>
    [ObservableProperty] private int _revision;

    [NotifyPropertyChangedFor(nameof(ValidUntil))]
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [NotifyPropertyChangedFor(nameof(ValidUntil))]
    [ObservableProperty] private int _validityDays = 15;

    [ObservableProperty] private string _currency = "TL";

    // --- Müşteri snapshot ---
    [ObservableProperty] private string _customerId = "";
    [ObservableProperty] private string _customerCompany = "";
    [ObservableProperty] private string _customerContact = "";
    [ObservableProperty] private Salutation _customerSalutation = Salutation.Bey;
    [ObservableProperty] private string _customerEmail = "";
    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private string _customerAddress = "";
    [ObservableProperty] private string _customerTaxOffice = "";
    [ObservableProperty] private string _customerTaxNumber = "";

    [ObservableProperty] private ObservableCollection<QuoteLine> _lines = new();

    // --- Genel iskonto (satır iskontolarının üstüne) ---
    [NotifyPropertyChangedFor(nameof(Totals))]
    [ObservableProperty] private DiscountMode _discountMode = DiscountMode.Yok;
    [NotifyPropertyChangedFor(nameof(Totals))]
    [ObservableProperty] private decimal _discountValue;

    // --- Şartlar ---
    [ObservableProperty] private string _paymentTerms = "";
    [ObservableProperty] private string _deliveryTime = "";
    [ObservableProperty] private string _deliveryPlace = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _exchangeRateNote = "";

    [ObservableProperty] private string _templateId = "modern";

    /// <summary>PDF dili: "tr" (varsayılan) | "en" (ihracat proforması). Eski kayıtlar tr okunur.</summary>
    [ObservableProperty] private string _language = "tr";

    /// <summary>Yaşam döngüsü durumu — başarılı gönderimde Taslak→Gönderildi otomatik geçer.</summary>
    [ObservableProperty] private QuoteStatus _status = QuoteStatus.Taslak;

    [ObservableProperty] private DateTime _createdAt = DateTime.Now;
    [ObservableProperty] private DateTime _updatedAt = DateTime.Now;

    public DateTime ValidUntil => Date.AddDays(ValidityDays);

    /// <summary>Tüm toplamlar (tek doğru kaynak — QuoteCalculator).</summary>
    public QuoteTotals Totals => QuoteCalculator.Compute(this);

    /// <summary>Satır eklendi/silindi/değişti veya iskonto değişti → toplamları yeniden yayınla (VM'den çağrılır).</summary>
    public void NotifyTotalsChanged() => OnPropertyChanged(nameof(Totals));

    /// <summary>Görünen başlık (liste/pencere): "TKF-2026-0001 · Firma" (+ rev varsa). Ön ek AppSettings.QuoteNoPrefix'e göre değişir.</summary>
    public string DisplayTitle =>
        (string.IsNullOrWhiteSpace(QuoteNo) ? "(numarasız)" : QuoteNo)
        + (Revision > 0 ? $" rev.{Revision}" : "")
        + (string.IsNullOrWhiteSpace(CustomerCompany) ? "" : $" · {CustomerCompany}");

    /// <summary>Derin kopya. newId=true ise yeni Id atar (kaydet-farklı-kaydet için).</summary>
    public Quote Clone(bool newId = false)
    {
        var c = new Quote
        {
            Id = newId ? Guid.NewGuid().ToString("N") : Id,
            QuoteNo = QuoteNo,
            Revision = Revision,
            Date = Date,
            ValidityDays = ValidityDays,
            Currency = Currency,
            CustomerId = CustomerId,
            CustomerCompany = CustomerCompany,
            CustomerContact = CustomerContact,
            CustomerSalutation = CustomerSalutation,
            CustomerEmail = CustomerEmail,
            CustomerPhone = CustomerPhone,
            CustomerAddress = CustomerAddress,
            CustomerTaxOffice = CustomerTaxOffice,
            CustomerTaxNumber = CustomerTaxNumber,
            DiscountMode = DiscountMode,
            DiscountValue = DiscountValue,
            PaymentTerms = PaymentTerms,
            DeliveryTime = DeliveryTime,
            DeliveryPlace = DeliveryPlace,
            Notes = Notes,
            ExchangeRateNote = ExchangeRateNote,
            TemplateId = TemplateId,
            Language = Language,
            Status = Status,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
        foreach (var l in Lines) c.Lines.Add(l.Clone());
        return c;
    }

    /// <summary>Yeni revizyon üretir (yeni Id, Revision+1, aynı QuoteNo, satırlar kopyalanır).
    /// Durum Taslak'a döner — revizyon yeni bir müzakere turudur, henüz gönderilmemiştir.</summary>
    public Quote CreateRevision()
    {
        var r = Clone(newId: true);
        r.Revision = Revision + 1;
        r.Date = DateTime.Today;
        r.Status = QuoteStatus.Taslak;
        r.CreatedAt = DateTime.Now;
        r.UpdatedAt = DateTime.Now;
        return r;
    }

    /// <summary>Müşteri kaydından snapshot doldurur.</summary>
    public void ApplyCustomer(Customer c)
    {
        CustomerId = c.Id;
        CustomerCompany = c.CompanyName;
        CustomerContact = c.ContactName;
        CustomerSalutation = c.Salutation;
        CustomerEmail = c.Email;
        CustomerPhone = c.Phone;
        CustomerAddress = c.Address;
        CustomerTaxOffice = c.TaxOffice;
        CustomerTaxNumber = c.TaxNumber;
    }
}
