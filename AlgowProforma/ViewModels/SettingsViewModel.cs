using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service = new();

    [ObservableProperty] private AppSettings _settings;
    [ObservableProperty] private bool _hasStoredPassword;
    [ObservableProperty] private string _testEmail = "";
    [ObservableProperty] private string _statusText = "";
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [ObservableProperty] private bool _busy;
    /// <summary>OAuth/test akışı sürerken buton kilidi — çifte tıklama ikinci listener/akış açmasın.</summary>
    public bool IsNotBusy => !Busy;
    [ObservableProperty] private string _googleLoginStatus = "";
    [ObservableProperty] private string _gmailStatus = "";
    [ObservableProperty] private string _connectedGmailEmail = "";
    [ObservableProperty] private bool _hasGmailConnection;

    /// <summary>"Teklif teması" dropdown kaynağı — katalogla aynı 23 palet.</summary>
    public System.Collections.Generic.IReadOnlyList<PdfTheme> QuoteThemes => PdfTheme.All;

    public SettingsViewModel()
    {
        _settings = _service.Load();
        HasStoredPassword = CredentialStore.HasPassword;
        TestEmail = _settings.Mail.FromEmail;
        StatusText = HasStoredPassword
            ? "SMTP App Password kayitli (sifreli)."
            : "SMTP App Password henuz girilmedi.";
        RefreshGoogleStatus();
    }

    [RelayCommand]
    private void Save()
    {
        _service.Save(Settings);
        StatusText = "Ayarlar kaydedildi.";
    }

    public void SavePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) { StatusText = "Sifre bos olamaz."; return; }
        CredentialStore.SavePassword(password);
        HasStoredPassword = true;
        StatusText = "SMTP App Password sifrelenip kaydedildi (DPAPI).";
    }

    public void ClearPassword()
    {
        CredentialStore.Clear();
        HasStoredPassword = false;
        StatusText = "Kayitli SMTP sifresi silindi.";
    }

    public async Task TestSendAsync(string? passwordFromBox)
    {
        if (Busy) return;   // akış sürerken ikinci tıklama yok sayılır
        var pw = !string.IsNullOrWhiteSpace(passwordFromBox) ? passwordFromBox : CredentialStore.LoadPassword();
        if (string.IsNullOrWhiteSpace(pw)) { StatusText = "Once SMTP App Password girin."; return; }

        var to = string.IsNullOrWhiteSpace(TestEmail) ? Settings.Mail.FromEmail : TestEmail.Trim();
        Busy = true;
        StatusText = $"Test e-postasi gonderiliyor -> {to} ...";
        try
        {
            _service.Save(Settings);
            var svc = new MailService(Settings.Mail, pw);
            var r = await svc.SendOnceAsync(
                to, "Test", "Algow Proforma - Mail Ayari Testi",
                "Bu bir test e-postasidir.\n\nAlgow Proforma mail gonderim ayarlari calisiyor.", null);
            StatusText = r.Success ? $"Test gonderildi: {to}" : $"Gonderilemedi: {r.Error}";
        }
        catch (Exception ex)
        {
            StatusText = "Hata: " + ex.Message;
        }
        finally { Busy = false; }
    }

    public async Task GoogleSignInAsync()
    {
        if (Busy) return;   // ikinci tıklama ikinci OAuth listener'ı açmasın
        Busy = true;
        StatusText = "Google girisi icin tarayici aciliyor...";
        try
        {
            _service.Save(Settings);
            var user = await new GoogleOAuthService().SignInAsync(Settings.Google);
            Settings.Google.Identity.GoogleSub = user.Sub;
            Settings.Google.Identity.Email = user.Email;
            Settings.Google.Identity.Name = user.Name;
            Settings.Google.Identity.EmailVerified = user.EmailVerified;
            Settings.Google.Identity.ConnectedAt = DateTime.Now;
            _service.Save(Settings);
            RefreshGoogleStatus();
            StatusText = $"Google girisi tamam: {user.Email}";
        }
        catch (Exception ex)
        {
            StatusText = "Google girisi basarisiz: " + ex.Message;
        }
        finally { Busy = false; }
    }

    public async Task ConnectGmailAsync()
    {
        if (Busy) return;   // ikinci tıklama ikinci OAuth listener'ı açmasın
        Busy = true;
        StatusText = "Gmail gonderim izni icin tarayici aciliyor...";
        try
        {
            _service.Save(Settings);
            var credential = await new GoogleOAuthService().ConnectGmailAsync(Settings.Google);
            CredentialStore.SaveGmailCredential(credential);
            RefreshGoogleStatus();
            StatusText = $"Gmail baglandi: {credential.GoogleEmail}";
        }
        catch (Exception ex)
        {
            StatusText = "Gmail baglantisi basarisiz: " + ex.Message;
        }
        finally { Busy = false; }
    }

    public async Task DisconnectGmailAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            var credential = CredentialStore.LoadGmailCredential();
            if (credential is not null)
            {
                var token = string.IsNullOrWhiteSpace(credential.RefreshToken) ? credential.AccessToken : credential.RefreshToken;
                await new GoogleOAuthService().RevokeAsync(token);
            }
            CredentialStore.ClearGmailCredential();
            RefreshGoogleStatus();
            StatusText = "Gmail baglantisi kaldirildi.";
        }
        catch (Exception ex)
        {
            StatusText = "Gmail baglantisi kaldirilamadi: " + ex.Message;
        }
        finally { Busy = false; }
    }

    // ---- tek-tık veri yedeği (ZIP) ----
    public async Task CreateBackupAsync(string zipPath)
    {
        if (Busy) return;
        Busy = true;
        StatusText = "Yedek alınıyor…";
        try
        {
            var (added, skipped) = await Task.Run(() => BackupService.CreateBackup(zipPath));
            StatusText = skipped == 0
                ? $"Yedek alındı: {added} dosya → {System.IO.Path.GetFileName(zipPath)}"
                : $"Yedek alındı: {added} dosya ({skipped} kilitli dosya atlandı) → {System.IO.Path.GetFileName(zipPath)}";
        }
        catch (Exception ex)
        {
            StatusText = "Yedek alınamadı: " + ex.Message;
        }
        finally { Busy = false; }
    }

    public async Task RestoreBackupAsync(string zipPath)
    {
        if (Busy) return;
        Busy = true;
        StatusText = "Yedek geri yükleniyor…";
        try
        {
            var safety = await Task.Run(() => BackupService.RestoreBackup(zipPath));
            StatusText = string.IsNullOrEmpty(safety)
                ? "Yedek geri yüklendi — uygulamayı YENİDEN BAŞLATIN."
                : $"Yedek geri yüklendi (önceki veri: {System.IO.Path.GetFileName(safety)}) — uygulamayı YENİDEN BAŞLATIN.";
        }
        catch (Exception ex)
        {
            StatusText = "Geri yükleme başarısız: " + ex.Message;
        }
        finally { Busy = false; }
    }

    private void RefreshGoogleStatus()
    {
        GoogleLoginStatus = Settings.Google.Identity.IsSignedIn
            ? $"Google girisi: {Settings.Google.Identity.Email}"
            : "Google girisi yok.";

        var credential = CredentialStore.LoadGmailCredential();
        HasGmailConnection = credential?.IsConnected == true;
        ConnectedGmailEmail = credential?.GoogleEmail ?? "";
        GmailStatus = HasGmailConnection
            ? $"Gmail gonderimi bagli: {ConnectedGmailEmail}"
            : "Gmail gonderimi bagli degil.";
    }
}
