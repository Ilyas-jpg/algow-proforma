using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.Views;

public partial class EmailPreviewWindow : Window
{
    private readonly Quote _quote;
    private readonly string _pdfPath;
    private readonly string _attachmentDisplayName;
    private readonly AppSettings _settings;
    private readonly GoogleGmailCredential? _gmailCredential;

    public EmailPreviewWindow(Quote quote, string pdfPath, string attachmentDisplayName)
    {
        InitializeComponent();

        _quote = quote;
        _pdfPath = pdfPath;
        _attachmentDisplayName = attachmentDisplayName;
        _settings = new SettingsService().Load();
        _gmailCredential = CredentialStore.LoadGmailCredential();

        CompanyText.Text = EmptyFallback(_quote.CustomerCompany);
        ContactText.Text = EmptyFallback(_quote.CustomerContact);
        ToEmailBox.Text = _quote.CustomerEmail;
        AttachmentText.Text = _attachmentDisplayName;
        SubjectBox.Text = _settings.EmailTemplate.Subject;
        BodyBox.Text = BuildDefaultBody();

        GmailButton.IsEnabled = _gmailCredential?.IsConnected == true;
        SmtpButton.IsEnabled = CredentialStore.HasPassword;
        ProviderText.Text = GmailButton.IsEnabled
            ? $"Gmail: {_gmailCredential!.GoogleEmail}"
            : (SmtpButton.IsEnabled ? $"SMTP: {_settings.Mail.FromEmail}" : "Gmail veya SMTP bağlı değil");
        StatusText.Text = GmailButton.IsEnabled
            ? "Gmail bağlı. Gönderim Gmail API ile yapılabilir."
            : "Gmail bağlı değil. SMTP şifresi varsa onunla gönderilebilir.";
    }

    private string BuildDefaultBody()
    {
        var templateBody = _settings.EmailTemplate.FullBody(_quote.CustomerContact, _quote.CustomerSalutation);
        if (!string.IsNullOrWhiteSpace(templateBody)) return templateBody;

        var contact = string.IsNullOrWhiteSpace(_quote.CustomerContact) ? "Yetkili" : _quote.CustomerContact;
        var company = string.IsNullOrWhiteSpace(_quote.CustomerCompany) ? "firmaniz" : _quote.CustomerCompany;
        var sender = string.IsNullOrWhiteSpace(_settings.Mail.FromName) ? "" : _settings.Mail.FromName;
        return $"Sayin {contact},\n\n{company} icin hazirladigimiz proforma teklifimizi ekte bilgilerinize sunariz.\n\nTeklif kapsaminda yer alan urun/hizmet kalemleri, fiyatlandirma ve gecerlilik bilgileri ekteki proforma dosyasinda yer almaktadir.\n\nHerhangi bir sorunuz veya revizyon talebiniz olursa memnuniyetle yardimci oluruz.\n\nSaygilarimizla,\n{sender}";
    }

    private async void OnSendGmail(object sender, RoutedEventArgs e)
    {
        if (_gmailCredential is null || !_gmailCredential.IsConnected)
        {
            StatusText.Text = "Gmail bağlı değil.";
            return;
        }

        await SendAsync("gmail", async () =>
        {
            var svc = new GmailService();
            return await svc.SendAsync(
                _settings.Google,
                _gmailCredential,
                _settings.Mail.FromName,
                ToEmailBox.Text.Trim(),
                _quote.CustomerContact,
                SubjectBox.Text.Trim(),
                BodyBox.Text,
                _pdfPath,
                _attachmentDisplayName,
                bccSelf: _settings.Mail.BccSelf);
        });
    }

    private async void OnSendSmtp(object sender, RoutedEventArgs e)
    {
        await SendAsync("smtp", async () =>
        {
            var pw = CredentialStore.LoadPassword();
            if (string.IsNullOrWhiteSpace(pw))
                return new MailSendResult { Success = false, Recipient = ToEmailBox.Text.Trim(), Error = "SMTP App Password kayıtlı değil." };

            var svc = new MailService(_settings.Mail, pw);
            return await svc.SendOnceAsync(
                ToEmailBox.Text.Trim(),
                _quote.CustomerContact,
                SubjectBox.Text.Trim(),
                BodyBox.Text,
                _pdfPath,
                attachmentDisplayName: _attachmentDisplayName);
        });
    }

    private async Task SendAsync(string provider, Func<Task<MailSendResult>> send)
    {
        if (!Services.EmailValidator.IsValid(ToEmailBox.Text))   // uygulama genelindeki tek doğrulayıcı
        {
            StatusText.Text = "Geçerli bir alıcı e-postası girin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SubjectBox.Text))
        {
            StatusText.Text = "Konu boş olamaz.";
            return;
        }
        if (!File.Exists(_pdfPath))
        {
            StatusText.Text = "PDF dosyası bulunamadı: " + _pdfPath;
            return;
        }

        GmailButton.IsEnabled = false;
        SmtpButton.IsEnabled = false;
        StatusText.Text = provider == "gmail" ? "Gmail API ile gönderiliyor…" : "SMTP ile gönderiliyor…";

        // async-void handler'lardan çağrılıyoruz: buradan kaçan HER istisna uygulamayı kapatır —
        // üstelik mail GİTMİŞ olabilirken (örn. log diski dolu). Hata akışı pencerede kalır.
        try
        {
            var result = await send();
            AppendLog(provider, result);
            if (result.Success && _quote.Status == QuoteStatus.Taslak)
            {
                // Teklif artık müşteride — pano Taslak yerine Gönderildi göstersin (anında kalıcı).
                _quote.Status = QuoteStatus.Gonderildi;
                try { new QuoteService().Save(_quote); } catch { /* durum güncellemesi gönderimi geri almaz */ }
            }
            StatusText.Text = result.Success
                ? $"Gönderildi: {result.Recipient}"
                : $"Gönderilemedi: {result.Error}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gönderim hatası: " + ex.Message;
        }
        finally
        {
            GmailButton.IsEnabled = _gmailCredential?.IsConnected == true;
            SmtpButton.IsEnabled = CredentialStore.HasPassword;
        }
    }

    private void AppendLog(string provider, MailSendResult result)
    {
        // Log yazımı best-effort: gönderim geçmişi kaydedilemedi diye GİTMİŞ maili hata sayma.
        try
        {
            new EmailLogService().Append(new EmailSendLog
            {
                QuoteId = _quote.Id,
                QuoteNo = _quote.QuoteNo,
                Provider = provider,
                FromEmail = provider == "gmail" ? (_gmailCredential?.GoogleEmail ?? "") : _settings.Mail.FromEmail,
                ToEmail = ToEmailBox.Text.Trim(),
                RecipientCompany = _quote.CustomerCompany,
                RecipientContact = _quote.CustomerContact,
                Subject = SubjectBox.Text.Trim(),
                AttachmentFileName = _attachmentDisplayName,
                Success = result.Success,
                Error = result.Error ?? "",
                SentAt = DateTime.Now,
            });
        }
        catch { /* geçmiş penceresinde eksik kayıt olur; gönderim sonucu kullanıcıya yine gösterilir */ }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static string EmptyFallback(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
