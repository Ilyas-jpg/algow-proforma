using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

/// <summary>SMTP gönderim hesabı. Parola BURADA tutulmaz — DPAPI ile CredentialStore'da şifreli.</summary>
public partial class MailAccount : ObservableObject
{
    [ObservableProperty] private string _smtpHost = "smtp.gmail.com";
    [ObservableProperty] private int _smtpPort = 587;
    [ObservableProperty] private string _fromName = "";
    [ObservableProperty] private string _fromEmail = "";
    [ObservableProperty] private string _username = "";
    /// <summary>Gönderilen her teklifin bir kopyası kendine BCC ile (arşiv).</summary>
    [ObservableProperty] private bool _bccSelf = false;
    /// <summary>Toplu gönderimde dakikada en fazla kaç mail (spam/limit koruması).</summary>
    [ObservableProperty] private int _maxPerMinute = 20;
}

/// <summary>
/// E-posta şablonu — nötr Türkçe varsayılan. Kullanıcı kendi imza/gövde/konu metnini Ayarlar'dan girer.
/// {ad} = kişi adı yer tutucu. Türkçe virgül+boşluk konvansiyonu korunur.
/// </summary>
public partial class EmailTemplate : ObservableObject
{
    [ObservableProperty] private string _subject = "fiyat teklifi";
    [ObservableProperty] private string _greetingBey = "Merhaba {ad} Bey ,";
    [ObservableProperty] private string _greetingHanim = "Merhaba {ad} Hanım ,";
    [ObservableProperty] private string _greetingNeutral = "Merhabalar ,";
    [ObservableProperty] private string _body = "Talebte bulunduğunuz ürünlerin termini ve fiyatları ektedir.";
    [ObservableProperty] private string _closing = "Teşekkür eder , iyi çalışmalar dileriz.";
    [ObservableProperty] private string _signature = "";
    /// <summary>Ek dosya adı kalıbı. {musteri} ve {tarih} (GGAAYYYY) yer tutucu.</summary>
    [ObservableProperty] private string _attachmentPattern = "{musteri} {tarih}.pdf";

    public string Greeting(string? contactName, Salutation s)
    {
        var name = (contactName ?? "").Trim();
        if (string.IsNullOrEmpty(name) || s == Salutation.Yok) return GreetingNeutral;
        var tmpl = s == Salutation.Hanim ? GreetingHanim : GreetingBey;
        return tmpl.Replace("{ad}", name);
    }

    /// <summary>Tam mail gövdesi: selam + gövde + kapanış + imza.</summary>
    public string FullBody(string? contactName, Salutation s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Greeting(contactName, s));
        sb.AppendLine();
        sb.AppendLine(Body);
        sb.AppendLine();
        sb.AppendLine(Closing);
        sb.AppendLine();
        sb.Append(Signature);
        return sb.ToString();
    }

    public string AttachmentName(string? customer, DateTime date)
    {
        var cust = string.IsNullOrWhiteSpace(customer) ? "MUSTERI" : customer.Trim().ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        var name = AttachmentPattern.Replace("{musteri}", cust).Replace("{tarih}", date.ToString("ddMMyyyy"));
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

/// <summary>Google OAuth ayarları. Secret env'den de okunabilir; settings.json'a yazmak zorunlu değil.</summary>
public partial class GoogleOAuthSettings : ObservableObject
{
    [ObservableProperty] private string _clientId = "";
    // Düz metin settings.json'a YAZILMAZ — DPAPI ile CredentialStore'da şifreli (SMTP parolasıyla aynı desen).
    // In-memory kalır (UI binding + EffectiveClientSecret) ama JsonIgnore ile diske sızmaz.
    [ObservableProperty]
    [property: JsonIgnore]
    private string _clientSecret = "";
    [ObservableProperty] private string _authRedirectUri = "http://127.0.0.1:8777/google-auth/callback/";
    [ObservableProperty] private string _gmailRedirectUri = "http://127.0.0.1:8777/gmail/callback/";
    // İzin verilen alan adı(ları), virgülle. Boş = kısıt yok. Kullanıcı kendi kurumsal domainini Ayarlar'dan girer
    // → personel yanlışlıkla kişisel Gmail bağlayıp o hesaptan gönderemez (gmail.send bağlanan hesap adına gönderir).
    [ObservableProperty] private string _allowedDomains = "";
    [ObservableProperty] private GoogleIdentitySettings _identity = new();

    // Effective* = env-aware HESAPLANAN görünümler — kalıcılığa girmez. JsonIgnore şart:
    // System.Text.Json get-only public property'leri de YAZAR; EffectiveClientSecret üzerinden
    // düz-metin secret settings.json'a sızıyordu (SettingsServiceTests yakaladı, 2026-06-12).
    [JsonIgnore]
    public string EffectiveClientId =>
        Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? ClientId;

    [JsonIgnore]
    public string EffectiveClientSecret =>
        Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? ClientSecret;

    [JsonIgnore]
    public string EffectiveAuthRedirectUri =>
        Environment.GetEnvironmentVariable("GOOGLE_AUTH_REDIRECT_URI") ?? AuthRedirectUri;

    [JsonIgnore]
    public string EffectiveGmailRedirectUri =>
        Environment.GetEnvironmentVariable("GOOGLE_GMAIL_REDIRECT_URI") ?? GmailRedirectUri;

    [JsonIgnore]
    public string EffectiveAllowedDomains =>
        Environment.GetEnvironmentVariable("GOOGLE_OAUTH_ALLOWED_DOMAINS") ?? AllowedDomains;
}

public partial class GoogleIdentitySettings : ObservableObject
{
    [ObservableProperty] private string _googleSub = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _emailVerified;
    [ObservableProperty] private DateTime? _connectedAt;

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(GoogleSub) && !string.IsNullOrWhiteSpace(Email);
}

/// <summary>Uygulama ayarları (settings.json). Parola hariç her şey burada.</summary>
public partial class AppSettings : ObservableObject
{
    [ObservableProperty] private MailAccount _mail = new();
    [ObservableProperty] private EmailTemplate _emailTemplate = new();
    [ObservableProperty] private GoogleOAuthSettings _google = new();

    /// <summary>
    /// Teklif numarası ön eki (örn. "TKF" → TKF-2026-0001). Boş = salt yıl-sayaç (2026-0001).
    /// White-label: müşteri kendi kurum kodunu Ayarlar'dan girer (varsayılan boş — markaya bağımlı değil).
    /// </summary>
    [ObservableProperty] private string _quoteNoPrefix = "";
}
