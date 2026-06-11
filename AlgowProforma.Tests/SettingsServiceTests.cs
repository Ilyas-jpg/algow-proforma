using System;
using System.IO;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// SettingsService integration (TempLibraryRoot ile izole): client_secret'in settings.json'a
/// SIZMADIĞI, DPAPI (CredentialStore) round-trip'i ve eski düz-metin secret'in tek seferlik
/// DPAPI migration'ı. DPAPI CurrentUser scope — test, koşan kullanıcı hesabıyla şifreler/çözer.
/// </summary>
[Collection("AppPathsIsolation")]
public class SettingsServiceTests : IDisposable
{
    private readonly TempLibraryRoot _tmp = new();
    private readonly SettingsService _svc = new();

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public void SaveLoad_RoundTrip_SecretEncrypted_NotInJsonFile()
    {
        var s = new AppSettings { QuoteNoPrefix = "RT" };
        s.Google.ClientId = "cid-123";
        s.Google.ClientSecret = "cok-gizli-s3cret";

        _svc.Save(s);

        var json = File.ReadAllText(AppPaths.SettingsFile);
        Assert.DoesNotContain("cok-gizli-s3cret", json);          // düz metin diske sızmadı
        Assert.True(CredentialStore.HasClientSecret);             // DPAPI bin oluştu

        var loaded = _svc.Load();
        Assert.Equal("RT", loaded.QuoteNoPrefix);
        Assert.Equal("cid-123", loaded.Google.ClientId);
        Assert.Equal("cok-gizli-s3cret", loaded.Google.ClientSecret);   // DPAPI → in-memory
    }

    [Fact]
    public void Load_LegacyPlaintextSecret_MigratesToDpapi_AndCleansFile()
    {
        // Eski sürüm davranışı: client_secret settings.json'da DÜZ METİN
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(AppPaths.SettingsFile,
            "{\"Google\":{\"ClientId\":\"cid-legacy\",\"ClientSecret\":\"eski-duz-metin\"}}");

        var loaded = _svc.Load();

        Assert.Equal("eski-duz-metin", loaded.Google.ClientSecret);     // migration sonrası erişilir
        Assert.True(CredentialStore.HasClientSecret);
        Assert.Equal("eski-duz-metin", CredentialStore.LoadClientSecret());
        Assert.DoesNotContain("eski-duz-metin", File.ReadAllText(AppPaths.SettingsFile));  // dosyadan temizlendi
        Assert.Equal("cid-legacy", loaded.Google.ClientId);             // diğer alanlar korundu
    }

    [Fact]
    public void Load_LegacySecret_DoesNotOverwriteExistingDpapiSecret()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        CredentialStore.SaveClientSecret("dpapi-guncel");               // DPAPI master zaten dolu
        File.WriteAllText(AppPaths.SettingsFile,
            "{\"Google\":{\"ClientSecret\":\"eski-bayat\"}}");

        var loaded = _svc.Load();

        Assert.Equal("dpapi-guncel", loaded.Google.ClientSecret);       // mevcut DPAPI kazanır
        Assert.DoesNotContain("eski-bayat", File.ReadAllText(AppPaths.SettingsFile));
    }

    [Fact]
    public void Load_LeakedEffectiveSecret_MigratedToDpapi_AndScrubbed()
    {
        // JsonIgnore eklenmeden önceki sürümler EffectiveClientSecret üzerinden düz metin sızdırmıştı
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(AppPaths.SettingsFile,
            "{\"Google\":{\"ClientId\":\"cid\",\"ClientSecret\":\"\",\"EffectiveClientSecret\":\"sizan-secret\"}}");

        var loaded = _svc.Load();

        Assert.Equal("sizan-secret", loaded.Google.ClientSecret);       // sızan kopya DPAPI'ye taşındı
        var json = File.ReadAllText(AppPaths.SettingsFile);
        Assert.DoesNotContain("sizan-secret", json);                    // değer dosyadan gitti
        Assert.DoesNotContain("EffectiveClientSecret", json);           // anahtar da temizlendi
    }

    [Fact]
    public void Save_EmptySecret_ClearsStoredCredential()
    {
        CredentialStore.SaveClientSecret("silinecek");
        Assert.True(CredentialStore.HasClientSecret);

        _svc.Save(new AppSettings());                                   // ClientSecret = ""

        Assert.False(CredentialStore.HasClientSecret);                  // boş → kayıt silindi
    }
}
