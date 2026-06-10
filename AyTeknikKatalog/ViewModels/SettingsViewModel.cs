using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;

namespace AyTeknikKatalog.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service = new();

    [ObservableProperty] private AppSettings _settings;
    [ObservableProperty] private bool _hasStoredPassword;
    [ObservableProperty] private string _testEmail = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _googleLoginStatus = "";
    [ObservableProperty] private string _gmailStatus = "";
    [ObservableProperty] private string _connectedGmailEmail = "";
    [ObservableProperty] private bool _hasGmailConnection;

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
