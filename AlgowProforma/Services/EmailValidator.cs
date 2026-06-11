using MimeKit;

namespace AlgowProforma.Services;

/// <summary>
/// RFC-uyumlu e-posta doğrulama (MimeKit <see cref="MailboxAddress.TryParse(string, out MailboxAddress)"/>).
/// Eski naif "@ ve . içeriyor mu" kontrolü "a@.b" / "a@b@c.com" gibi geçersizleri kabul ediyordu →
/// toplu gönderimde bounce / hatalı alıcı riski. Burada parse + @ konumu + domain'de nokta şartı aranır.
/// </summary>
public static class EmailValidator
{
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (!MailboxAddress.TryParse(email.Trim(), out var addr)) return false;

        var a = addr.Address;                       // "Ad <x@y>" girilse bile salt adres
        if (string.IsNullOrWhiteSpace(a)) return false;
        var at = a.LastIndexOf('@');
        return at > 0 && at < a.Length - 1 && a.IndexOf('.', at) > at;   // @ var + sonrası domain + nokta
    }
}
