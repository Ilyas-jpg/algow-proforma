using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;
using AlgowProforma.Views;

namespace AlgowProforma.ViewModels;

public partial class QuoteEditorViewModel : ObservableObject
{
    private readonly QuoteService _quotes = new();
    private readonly PriceBookService _priceBook = new();
    private readonly CustomerService _customers = new();
    private readonly BrandInfo _brand;

    [ObservableProperty] private Quote _quote;
    [ObservableProperty] private ObservableCollection<PriceItem> _priceBookItems = new();
    [ObservableProperty] private ObservableCollection<Customer> _customers2 = new();
    [ObservableProperty] private PriceItem? _selectedPriceItem;
    [ObservableProperty] private decimal _addQuantity = 1m;
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private QuoteLine? _selectedLine;
    [ObservableProperty] private string _statusText = "";

    public QuoteEditorViewModel(Quote? existing = null)
    {
        // Teklif PDF'i kalıcı marka profilini kullanır (logo/firma/iletişim) — yoksa boş default.
        _brand = BrandProfileService.Load() ?? Catalog.CreateDefault().Brand;

        foreach (var p in _priceBook.Load().Where(p => p.IsActive)) PriceBookItems.Add(p);
        foreach (var c in _customers.Load()) Customers2.Add(c);

        _quote = existing ?? new Quote
        {
            ValidityDays = 15,
            Currency = "TL",
            PaymentTerms = "%50 peşin, %50 teslimde",
            DeliveryTime = "Sipariş onayından sonra 10 iş günü",
        };
        HookQuote(_quote);
        StatusText = existing is null ? "Yeni teklif." : $"Teklif yüklendi: {_quote.QuoteNo}";
    }

    partial void OnQuoteChanged(Quote? oldValue, Quote newValue)
    {
        if (oldValue is not null) UnhookQuote(oldValue);
        HookQuote(newValue);
    }

    // ---- canlı toplam reaktivitesi ----
    private void HookQuote(Quote q)
    {
        q.Lines.CollectionChanged += OnLinesChanged;
        foreach (var l in q.Lines) l.PropertyChanged += OnLineChanged;
    }

    private void UnhookQuote(Quote q)
    {
        q.Lines.CollectionChanged -= OnLinesChanged;
        foreach (var l in q.Lines) l.PropertyChanged -= OnLineChanged;
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (QuoteLine l in e.OldItems) l.PropertyChanged -= OnLineChanged;
        if (e.NewItems is not null) foreach (QuoteLine l in e.NewItems) l.PropertyChanged += OnLineChanged;
        Quote.NotifyTotalsChanged();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => Quote.NotifyTotalsChanged();

    // ---- komutlar ----
    [RelayCommand]
    private void AddFromPriceBook()
    {
        if (SelectedPriceItem is null) { StatusText = "Havuzdan bir ürün seçin."; return; }
        var qty = AddQuantity <= 0 ? 1m : AddQuantity;
        Quote.Lines.Add(SelectedPriceItem.ToQuoteLine(qty));
        StatusText = $"Eklendi: {SelectedPriceItem.Name}";
    }

    [RelayCommand]
    private void AddBlankLine()
    {
        Quote.Lines.Add(new QuoteLine { Name = "Yeni kalem", Unit = "adet", Quantity = 1m, VatRate = 20m });
    }

    [RelayCommand]
    private void RemoveLine()
    {
        if (SelectedLine is null) return;
        Quote.Lines.Remove(SelectedLine);
    }

    [RelayCommand]
    private void ApplySelectedCustomer()
    {
        if (SelectedCustomer is null) { StatusText = "Bir müşteri seçin."; return; }
        Quote.ApplyCustomer(SelectedCustomer);
        StatusText = $"Müşteri: {SelectedCustomer.DisplayName}";
    }

    [RelayCommand]
    private void Save()
    {
        if (Quote.Lines.Count == 0) { StatusText = "En az bir kalem ekleyin."; return; }
        _quotes.Save(Quote);
        OnPropertyChanged(nameof(Quote));
        StatusText = $"Kaydedildi: {Quote.QuoteNo}";
    }

    [RelayCommand]
    private void GeneratePdf()
    {
        if (Quote.Lines.Count == 0) { StatusText = "En az bir kalem ekleyin."; return; }
        try
        {
            _quotes.Save(Quote); // numara + arşiv
            OnPropertyChanged(nameof(Quote));
            var safeNo = string.IsNullOrWhiteSpace(Quote.QuoteNo) ? Quote.Id : Quote.QuoteNo;
            var path = Path.Combine(AppPaths.QuotesDir, safeNo + ".pdf");
            QuotePdfService.Generate(Quote, _brand, path);
            Process.Start(new ProcessStartInfo("msedge.exe", $"\"{path}\"") { UseShellExecute = true });
            StatusText = $"PDF üretildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("PDF üretilemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void PreviewEmail()
    {
        if (Quote.Lines.Count == 0) { StatusText = "En az bir kalem ekleyin."; return; }
        if (string.IsNullOrWhiteSpace(Quote.CustomerEmail)) { StatusText = "Musteri e-postasi bos."; return; }

        try
        {
            _quotes.Save(Quote);
            OnPropertyChanged(nameof(Quote));

            var settings = new SettingsService().Load();
            var safeNo = string.IsNullOrWhiteSpace(Quote.QuoteNo) ? Quote.Id : Quote.QuoteNo;
            var pdfPath = Path.Combine(AppPaths.QuotesDir, safeNo + ".pdf");
            QuotePdfService.Generate(Quote, _brand, pdfPath);
            var attachmentName = settings.EmailTemplate.AttachmentName(Quote.CustomerCompany, DateTime.Today);

            var win = new EmailPreviewWindow(Quote, pdfPath, attachmentName)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            };
            win.ShowDialog();
            StatusText = "E-posta onizleme kapatildi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("E-posta onizleme hazirlanamadi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void CopyText()
    {
        if (Quote.Lines.Count == 0) { StatusText = "Önce kalem ekleyin."; return; }
        try
        {
            System.Windows.Clipboard.SetText(QuoteTextService.Build(Quote, _brand));
            StatusText = "Teklif metni panoya kopyalandı (WhatsApp/mail için).";
        }
        catch { StatusText = "Panoya kopyalanamadı."; }
    }

    [RelayCommand]
    private void CreateRevision()
    {
        var rev = Quote.CreateRevision();
        Quote = rev;
        StatusText = $"Yeni revizyon: rev.{rev.Revision}";
    }
}
