using AyTeknikKatalog.Services;
using Xunit;

namespace AyTeknikKatalog.Tests;

/// <summary>
/// Toplu gönderim alıcı doğrulaması (M2). Naif "@ ve . içeriyor mu" yerine MimeKit MailboxAddress.TryParse +
/// @ konumu + domain'de nokta şartı. Geçersizleri (a@.b yok; domain'de nokta zorunlu) eler, "Ad &lt;mail&gt;" kabul.
/// </summary>
public class EmailValidatorTests
{
    [Theory]
    [InlineData("info@algow.net")]
    [InlineData("a.b-c_d@sub.example.com")]
    [InlineData("Ad Soyad <kisi@firma.com.tr>")]
    public void Valid_Addresses_Pass(string email) => Assert.True(EmailValidator.IsValid(email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainstring")]
    [InlineData("@firma.com")]
    [InlineData("kisi@firma")]   // domain'de nokta yok
    public void Invalid_Addresses_Fail(string? email) => Assert.False(EmailValidator.IsValid(email));
}
