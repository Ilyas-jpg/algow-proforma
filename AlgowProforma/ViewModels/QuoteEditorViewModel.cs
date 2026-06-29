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

    [ObservableProperty] private Quote _quote;
    [ObservableProperty] private ObservableCollection<PriceItem> _priceBookItems = new();
    [ObservableProperty] private ObservableCollection<Customer> _customers2 = new();
    [ObservableProperty] private PriceItem? _selectedPriceItem;
    [ObservableProperty] private decimal _addQuantity = 1m;
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private QuoteLine? _selectedLine;
    [ObservableProperty] private string _statusText = "";

    /// <summary>Dil / şablon dropdown kaynakları (Quote.Language / Quote.TemplateId).</summary>
    public System.Collections.Generic.IReadOnlyList<QuoteLanguageOption> LanguageOptions => QuoteLanguageOption.All;
    public System.Collections.Generic.IReadOnlyList<QuoteTemplateOption> TemplateOptions => QuoteTemplateOption.All;

    public QuoteEditorViewModel(Quote? existing = null)
    {
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

        StartPreviewTimer();
        SchedulePreview();   // açılışta ilk önizleme
    }

    // ---- canlı PDF önizleme (sağ panel) ----
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? _previewImage;
    [ObservableProperty] private bool _isPreviewLoading;

    private System.Windows.Threading.DispatcherTimer? _previewTimer;
    private int _previewSeq;   // bayat render sonucu bind edilmez (yenisi yoldayken)

    private void StartPreviewTimer()
    {
        // Debounce: her tuş vuruşunda değil, 700 ms sessizlikten sonra render (QuestPDF maliyetli).
        _previewTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _previewTimer.Tick += (_, _) => { _previewTimer!.Stop(); RenderPreview(); };
    }

    private void SchedulePreview()
    {
        if (_previewTimer is null) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    /// <summary>Pencere kapanınca çağrılır — bekleyen debounce tick'i kapalı pencere üzerinde
    /// render başlatmasın (timer Dispatcher'da yaşamaya devam ediyordu).</summary>
    public void Shutdown()
    {
        _previewTimer?.Stop();
        _previewTimer = null;
        _previewSeq++;   // yoldaki render sonucu artık bind edilmez
        UnhookQuote(Quote);
    }

    private void RenderPreview()
    {
        IsPreviewLoading = true;
        var seq = ++_previewSeq;
        var snapshot = Quote.Clone();           // canlı Quote arka thread'e verilmez (race)
        var brand = LoadBrand();                // M2: her render'da taze marka (Ayarlar'da değişmiş olabilir)
        System.Threading.Tasks.Task.Run(() =>
        {
            var theme = PdfTheme.GetById(new SettingsService().Load().QuoteThemeId);
            var bytes = QuotePdfService.GeneratePreviewPngBytes(snapshot, brand, theme);
            var bmp = BytesToFrozenBitmap(bytes);
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (seq != _previewSeq) return;   // daha yeni bir render kuyruğa girdi
                if (bmp is not null) PreviewImage = bmp;
                IsPreviewLoading = false;
            }));
        });
    }

    private static System.Windows.Media.Imaging.BitmapSource? BytesToFrozenBitmap(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;
        try
        {
            using var ms = new System.IO.MemoryStream(bytes);
            var bi = new System.Windows.Media.Imaging.BitmapImage();
            bi.BeginInit();
            bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();   // UI thread'e güvenli geçiş
            return bi;
        }
        catch { return null; }
    }

    // ---- TCMB kur notu ----
    [RelayCommand]
    private async System.Threading.Tasks.Task FetchTcmbRates()
    {
        try
        {
            StatusText = "TCMB kurları çekiliyor…";
            var rates = await TcmbRateService.FetchTodayAsync();
            Quote.ExchangeRateNote = TcmbRateService.BuildNote(rates);
            StatusText = "Kur notu TCMB satış kuruyla dolduruldu.";
        }
        catch (Exception ex)
        {
            StatusText = "TCMB kuru alınamadı: " + ex.Message;
        }
    }

    partial void OnQuoteChanged(Quote? oldValue, Quote newValue)
    {
        if (oldValue is not null) UnhookQuote(oldValue);
        HookQuote(newValue);
        SchedulePreview();
    }

    // ---- kaydedilmemiş değişiklik takibi (H5: X ile sessiz veri kaybını önler) ----
    /// <summary>Son kayıttan beri kalem/alan değişti mi. Pencere Closing'i buna bakar.</summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>Pencere kapanırken "kaydet" seçilirse çağrılır. Kalemsiz teklif zaten kaydedilmediği
    /// için kayıpsız sayılır ve kapanışa izin verilir; kayıt hatasında pencere açık kalır.</summary>
    public bool TrySaveForClose()
    {
        if (Quote.Lines.Count == 0) return true;
        try
        {
            _quotes.Save(Quote);
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kaydedilemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // ---- canlı toplam reaktivitesi ----
    private void HookQuote(Quote q)
    {
        q.PropertyChanged += OnQuoteFieldChanged;
        q.Lines.CollectionChanged += OnLinesChanged;
        foreach (var l in q.Lines) l.PropertyChanged += OnLineChanged;
    }

    private void UnhookQuote(Quote q)
    {
        q.PropertyChanged -= OnQuoteFieldChanged;
        q.Lines.CollectionChanged -= OnLinesChanged;
        foreach (var l in q.Lines) l.PropertyChanged -= OnLineChanged;
    }

    private void OnQuoteFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Status değişimi (gönderim/pano otomatiği) anında diske yazılır — editörü "kaydedilmemiş
        // değişiklik" moduna sokmamalı; PDF'e de basılmadığından önizleme yenilemesi gerektirmez.
        // UpdatedAt/QuoteNo da Save'in KENDİ mutasyonları: e-posta önizleme penceresi gönderim
        // sonrası Save çağırınca UpdatedAt değişiyor, editör sahte "kaydedilmemiş değişiklik"
        // uyarısına düşüyordu — kullanıcı girdisi olmayan alanlar dirty saymaz.
        if (e.PropertyName is nameof(Quote.Status) or nameof(Quote.UpdatedAt) or nameof(Quote.QuoteNo)) return;
        HasUnsavedChanges = true;
        SchedulePreview();
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (QuoteLine l in e.OldItems) l.PropertyChanged -= OnLineChanged;
        if (e.NewItems is not null) foreach (QuoteLine l in e.NewItems) l.PropertyChanged += OnLineChanged;
        HasUnsavedChanges = true;
        Quote.NotifyTotalsChanged();
        SchedulePreview();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        HasUnsavedChanges = true;
        Quote.NotifyTotalsChanged();
        SchedulePreview();
    }

    // ---- komutlar ----

    /// <summary>Marka profilini HER üretimde TAZE okur — editör açıkken Ayarlar'dan marka değişirse
    /// (M2) eski logo/firma/adresle PDF/mail çıkmasın (white-label'da yanlış marka = yanlış kimlik).</summary>
    private static BrandInfo LoadBrand() => BrandProfileService.Load() ?? Catalog.CreateDefault().Brand;

    /// <summary>"TL"/"TRY"/"₺" tek aileye iner — karşılaştırma ve dönüşüm anahtarı.</summary>
    private static string NormalizeCurrency(string? c) => (c ?? "TL").Trim().ToUpperInvariant() switch
    {
        "TL" or "TRY" or "₺" => "TL",
        "USD" or "$" => "USD",
        "EUR" or "€" => "EUR",
        var other => other,
    };

    [RelayCommand]
    private async System.Threading.Tasks.Task AddFromPriceBook()
    {
        if (SelectedPriceItem is null) { StatusText = "Havuzdan bir ürün seçin."; return; }
        var qty = AddQuantity <= 0 ? 1m : AddQuantity;
        var line = SelectedPriceItem.ToQuoteLine(qty);

        // Havuz kalemi ile teklifin para birimi farklıysa SESSİZCE karıştırma (USD havuz fiyatı
        // TL teklife sayı olarak girince tutar ~40× sapıyordu) — kullanıcıya sor, istenirse TCMB
        // günlük satış kuruyla çevir.
        var itemCur = NormalizeCurrency(SelectedPriceItem.Currency);
        var quoteCur = NormalizeCurrency(Quote.Currency);
        if (itemCur != quoteCur)
        {
            bool convertible = itemCur is "TL" or "USD" or "EUR" && quoteCur is "TL" or "USD" or "EUR";
            if (!convertible)
            {
                var keep = MessageBox.Show(
                    $"\"{SelectedPriceItem.Name}\" havuzda {itemCur}, teklif {quoteCur} — otomatik dönüşüm bu birim için yok.\n\n" +
                    "Fiyat SAYI olarak aynen eklensin mi?",
                    "Para Birimi Farklı", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (keep != MessageBoxResult.OK) { StatusText = "Kalem eklenmedi."; return; }
            }
            else
            {
                var r = MessageBox.Show(
                    $"\"{SelectedPriceItem.Name}\" havuzda {itemCur}, teklifin para birimi {quoteCur}.\n\n" +
                    $"EVET → TCMB günlük satış kuruyla {quoteCur} karşılığına çevir\n" +
                    $"HAYIR → fiyatı sayı olarak aynen al ({line.UnitPrice:N2} {quoteCur} yazılır)\n" +
                    "İPTAL → kalemi ekleme",
                    "Para Birimi Farklı", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (r == MessageBoxResult.Cancel) { StatusText = "Kalem eklenmedi."; return; }
                if (r == MessageBoxResult.Yes)
                {
                    try
                    {
                        StatusText = "TCMB kuru çekiliyor…";
                        var rates = await TcmbRateService.FetchTodayAsync();
                        decimal ToTl(string cur) => cur switch { "USD" => rates.UsdSelling, "EUR" => rates.EurSelling, _ => 1m };
                        line.UnitPrice = Math.Round(line.UnitPrice * ToTl(itemCur) / ToTl(quoteCur), 2, MidpointRounding.AwayFromZero);
                        StatusText = $"{itemCur}→{quoteCur} TCMB kuruyla çevrildi ({rates.DateText}).";
                    }
                    catch (Exception ex)
                    {
                        StatusText = "TCMB kuru alınamadı — kalem eklenmedi: " + ex.Message;
                        return;
                    }
                }
            }
        }

        Quote.Lines.Add(line);
        if (itemCur == quoteCur) StatusText = $"Eklendi: {SelectedPriceItem.Name}";
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

    /// <summary>Hızlı eklenen müşteriyi CRM'e kaydeder, combo'ya ekler ve teklife uygular —
    /// editörden ayrılmadan "CRM'de olmayan müşteriye teklif" akışı (pencere code-behind çağırır).
    /// Aynı e-posta CRM'de zaten varsa MEVCUT kart uygulanır (UpsertByEmail'in boş adres/vergiyle
    /// dolu kartı ezmesi bilinçli atlanır).</summary>
    public void AddCustomerToCrmAndApply(Customer c)
    {
        System.Collections.Generic.List<Customer> list;
        try { list = _customers.Load(); }
        catch (Exception ex) { StatusText = "Müşteri listesi okunamadı: " + ex.Message; return; }

        var email = (c.Email ?? "").Trim().ToLowerInvariant();
        var existing = email.Length == 0
            ? null
            : list.FirstOrDefault(x => (x.Email ?? "").Trim().ToLowerInvariant() == email);
        if (existing is not null)
        {
            var inCombo = Customers2.FirstOrDefault(x => x.Id == existing.Id);
            if (inCombo is null) { Customers2.Add(existing); inCombo = existing; }
            SelectedCustomer = inCombo;
            Quote.ApplyCustomer(inCombo);
            StatusText = $"Bu e-posta zaten kayıtlı — mevcut müşteri uygulandı: {inCombo.DisplayName}";
            return;
        }

        try
        {
            list.Add(c);
            _customers.Save(list);
        }
        catch (Exception ex)
        {
            StatusText = "Müşteri CRM'e kaydedilemedi: " + ex.Message;
            return;   // CRM'e girmeyen müşteri teklife de uygulanmaz (yarım durum bırakma)
        }
        Customers2.Add(c);
        SelectedCustomer = c;
        Quote.ApplyCustomer(c);
        StatusText = $"Müşteri eklendi ve uygulandı: {c.DisplayName}";
    }

    [RelayCommand]
    private void Save()
    {
        if (Quote.Lines.Count == 0) { StatusText = "En az bir kalem ekleyin."; return; }
        _quotes.Save(Quote);
        HasUnsavedChanges = false;   // Save'in kendi QuoteNo/UpdatedAt mutasyonlarından SONRA
        OnPropertyChanged(nameof(Quote));
        StatusText = $"Kaydedildi: {Quote.QuoteNo}";
    }

    /// <summary>Teklif PDF dosya adı — tek kaynak QuoteService.PdfFileName (pano ile aynı ad).</summary>
    private string QuotePdfFileName() => QuoteService.PdfFileName(Quote);

    [RelayCommand]
    private void GeneratePdf()
    {
        if (Quote.Lines.Count == 0) { StatusText = "En az bir kalem ekleyin."; return; }
        try
        {
            _quotes.Save(Quote); // numara + arşiv
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(Quote));
            var path = Path.Combine(AppPaths.QuotesDir, QuotePdfFileName());
            QuotePdfService.Generate(Quote, LoadBrand(), path, PdfTheme.GetById(new SettingsService().Load().QuoteThemeId));
            // Varsayılan PDF görüntüleyiciyle aç — msedge.exe hardcode'u Edge'siz makinede patlıyordu.
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(Quote));

            var settings = new SettingsService().Load();
            var pdfPath = Path.Combine(AppPaths.QuotesDir, QuotePdfFileName());
            QuotePdfService.Generate(Quote, LoadBrand(), pdfPath, PdfTheme.GetById(settings.QuoteThemeId));
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
            System.Windows.Clipboard.SetText(QuoteTextService.Build(Quote, LoadBrand()));
            StatusText = "Teklif metni panoya kopyalandı (WhatsApp/mail için).";
        }
        catch { StatusText = "Panoya kopyalanamadı."; }
    }

    [RelayCommand]
    private void CreateRevision()
    {
        var rev = Quote.CreateRevision();
        Quote = rev;
        HasUnsavedChanges = true;   // revizyon henüz kaydedilmedi (alan mutasyonları hook'tan önce olur)
        StatusText = $"Yeni revizyon: rev.{rev.Revision}";
    }
}
