using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;

namespace AyTeknikKatalog.ViewModels;

public partial class BulkSendViewModel : ObservableObject
{
    private readonly QuoteService _quotes = new();
    private readonly CustomerService _customers = new();
    private readonly SettingsService _settingsSvc = new();
    // Toplu gönderimde üretilen teklif PDF'leri kalıcı marka profilini kullanır — yoksa boş default.
    private readonly BrandInfo _brand = BrandProfileService.Load() ?? Catalog.CreateDefault().Brand;
    private readonly AppSettings _settings;

    [ObservableProperty] private ObservableCollection<Quote> _savedQuotes = new();
    [ObservableProperty] private Quote? _selectedQuote;
    [ObservableProperty] private ObservableCollection<BulkRecipient> _recipients = new();
    [ObservableProperty] private string _manualEmails = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _busy;

    public BulkSendViewModel()
    {
        _settings = _settingsSvc.Load();
        foreach (var q in _quotes.LoadAll()) SavedQuotes.Add(q);
        SelectedQuote = SavedQuotes.FirstOrDefault();
        StatusText = SavedQuotes.Count == 0
            ? "Önce bir teklif oluşturup kaydedin."
            : $"{SavedQuotes.Count} kayıtlı teklif. Alıcı ekleyin.";
    }

    [RelayCommand]
    private void LoadFromCrm()
    {
        int added = 0;
        foreach (var c in _customers.Load())
        {
            if (!IsValidEmail(c.Email)) continue;
            if (Recipients.Any(r => r.Email.Equals(c.Email, StringComparison.OrdinalIgnoreCase))) continue;
            Recipients.Add(new BulkRecipient { Company = c.CompanyName, ContactName = c.ContactName, Salutation = c.Salutation, Email = c.Email });
            added++;
        }
        StatusText = $"{added} müşteri eklendi · toplam {Recipients.Count} alıcı.";
    }

    [RelayCommand]
    private void AddManual()
    {
        if (string.IsNullOrWhiteSpace(ManualEmails)) return;
        var parts = ManualEmails.Split(new[] { '\n', '\r', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int added = 0;
        foreach (var raw in parts)
        {
            var email = raw.Trim();
            if (!IsValidEmail(email)) continue;
            if (Recipients.Any(r => r.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) continue;
            Recipients.Add(new BulkRecipient { Email = email, Salutation = Salutation.Yok });
            added++;
        }
        ManualEmails = "";
        StatusText = $"{added} e-posta eklendi · toplam {Recipients.Count} alıcı.";
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Alıcı listesi Excel'i" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            int added = 0;
            foreach (var c in ExcelDataService.ImportCustomers(dlg.FileName))
            {
                if (!IsValidEmail(c.Email)) continue;
                if (Recipients.Any(r => r.Email.Equals(c.Email, StringComparison.OrdinalIgnoreCase))) continue;
                Recipients.Add(new BulkRecipient { Company = c.CompanyName, ContactName = c.ContactName, Salutation = c.Salutation, Email = c.Email });
                added++;
            }
            StatusText = $"{added} alıcı içe aktarıldı · toplam {Recipients.Count}.";
        }
        catch (Exception ex) { MessageBox.Show("İçe aktarım hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    [RelayCommand]
    private void ClearRecipients()
    {
        Recipients.Clear();
        StatusText = "Alıcı listesi temizlendi.";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (SelectedQuote is null) { StatusText = "Bir teklif seçin."; return; }

        // Sağlayıcı: Gmail bağlıysa Gmail (OAuth), değilse SMTP App Password fallback.
        var gmailCred = CredentialStore.LoadGmailCredential();
        bool useGmail = gmailCred?.IsConnected == true;
        string? pw = null;
        if (!useGmail)
        {
            pw = CredentialStore.LoadPassword();
            if (string.IsNullOrWhiteSpace(pw))
            {
                StatusText = "Gmail bağlı değil ve SMTP App Password yok. Ayarlar'dan Gmail bağlayın ya da App Password girin.";
                return;
            }
        }

        var targets = Recipients.Where(r => r.Selected && IsValidEmail(r.Email)).ToList();
        if (targets.Count == 0) { StatusText = "Seçili geçerli alıcı yok."; return; }

        var providerLabel = useGmail ? $"Gmail ({gmailCred!.GoogleEmail})" : $"SMTP ({_settings.Mail.FromEmail})";
        var confirm = MessageBox.Show(
            $"{targets.Count} alıcıya \"{SelectedQuote.DisplayTitle}\" teklifi {providerLabel} ile gönderilecek.\n\nHer alıcıya kendi adına PDF üretilip ekli gönderilir. Devam edilsin mi?",
            "Toplu Gönderim Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) { StatusText = "İptal edildi."; return; }

        Busy = true;
        int sent = 0, fail = 0;
        var tmpl = _settings.EmailTemplate;
        int delayMs = _settings.Mail.MaxPerMinute > 0 ? (int)(60000.0 / _settings.Mail.MaxPerMinute) : 0;
        var logSvc = new EmailLogService();

        // SMTP: tek oturum aç-kapat. Gmail: paylaşılan servis (token gerekince kendi tazeler).
        MailService? smtp = useGmail ? null : new MailService(_settings.Mail, pw!);
        var gmail = useGmail ? new GmailService() : null;

        try
        {
            if (smtp is not null) await smtp.OpenAsync();

            foreach (var r in targets)
            {
                r.Status = "Hazırlanıyor...";
                string quoteId = "", quoteNo = "", displayName = "";
                try
                {
                    var q = SelectedQuote.Clone(newId: true);
                    q.ApplyCustomer(new Customer { CompanyName = r.Company, ContactName = r.ContactName, Salutation = r.Salutation, Email = r.Email });
                    quoteId = q.Id; quoteNo = q.QuoteNo;

                    displayName = tmpl.AttachmentName(r.TargetName, DateTime.Today);
                    var pdfPath = Path.Combine(AppPaths.QuotesDir, q.Id + ".pdf"); // benzersiz disk yolu (aynı firma/boş ad çakışmasın)
                    QuotePdfService.Generate(q, _brand, pdfPath);

                    var body = tmpl.FullBody(r.ContactName, r.Salutation);
                    var res = useGmail
                        ? await gmail!.SendAsync(_settings.Google, gmailCred!, _settings.Mail.FromName, r.Email, r.TargetName, tmpl.Subject, body, pdfPath, displayName)
                        : await smtp!.SendAsync(r.Email, r.TargetName, tmpl.Subject, body, pdfPath, attachmentDisplayName: displayName);

                    if (res.Success) { r.Status = "✓ Gönderildi"; sent++; }
                    else { r.Status = "✗ " + res.Error; fail++; }

                    logSvc.Append(new EmailSendLog
                    {
                        QuoteId = quoteId,
                        QuoteNo = quoteNo,
                        Provider = useGmail ? "gmail" : "smtp",
                        FromEmail = useGmail ? gmailCred!.GoogleEmail : _settings.Mail.FromEmail,
                        ToEmail = r.Email,
                        RecipientCompany = r.Company,
                        RecipientContact = r.ContactName,
                        Subject = tmpl.Subject,
                        AttachmentFileName = displayName,
                        Success = res.Success,
                        Error = res.Error ?? "",
                    });
                }
                catch (Exception ex) { r.Status = "✗ " + ex.Message; fail++; }

                StatusText = $"Gönderiliyor… {sent + fail}/{targets.Count}  (✓{sent} ✗{fail})";
                if (delayMs > 0) await Task.Delay(delayMs);
            }
            StatusText = $"Tamamlandı — ✓ {sent} gönderildi · ✗ {fail} başarısız.";
        }
        catch (Exception ex)
        {
            StatusText = "Gönderim hatası: " + ex.Message;
        }
        finally
        {
            if (smtp is not null) await smtp.CloseAsync();
            Busy = false;
        }
    }

    private static bool IsValidEmail(string? e) => EmailValidator.IsValid(e);
}
