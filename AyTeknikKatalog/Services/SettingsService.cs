using System.IO;
using System.Text.Json.Nodes;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

/// <summary>
/// Uygulama ayarlarının (mail hesabı + e-posta şablonu + OAuth) kalıcılığı (settings.json).
/// OAuth client_secret düz metin DOSYAYA yazılmaz — DPAPI ile CredentialStore'da şifreli tutulur
/// (SMTP parolasıyla aynı desen); settings.json'da yalnızca in-memory binding için JsonIgnore'lu alan.
/// </summary>
public class SettingsService
{
    public AppSettings Load()
    {
        MigrateLegacyClientSecret();
        var settings = JsonStore.LoadOrNew<AppSettings>(AppPaths.SettingsFile);
        settings.Google.ClientSecret = CredentialStore.LoadClientSecret() ?? "";  // DPAPI → in-memory
        return settings;
    }

    public void Save(AppSettings settings)
    {
        // Secret'i şifreli sakla (boşsa CredentialStore siler); settings.json'a JsonIgnore ile yazılmaz.
        CredentialStore.SaveClientSecret(settings.Google.ClientSecret);
        JsonStore.SaveObject(AppPaths.SettingsFile, settings);
    }

    /// <summary>
    /// Eski sürümlerde settings.json'a DÜZ METİN yazılmış client_secret'i (varsa) bir kez DPAPI'ye taşır
    /// ve dosyadan temizler. Best-effort: bozuk/eksik dosyada sessizce normal Load akışına döner.
    /// </summary>
    private static void MigrateLegacyClientSecret()
    {
        var path = AppPaths.SettingsFile;
        if (!File.Exists(path)) return;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            var legacy = root?["Google"]?["ClientSecret"]?.GetValue<string>();   // PascalCase (default policy)
            if (string.IsNullOrWhiteSpace(legacy)) return;

            if (!CredentialStore.HasClientSecret)
                CredentialStore.SaveClientSecret(legacy);   // DPAPI master — zaten varsa üzerine yazma
            root!["Google"]!["ClientSecret"] = "";          // düz metni dosyadan kaldır
            AtomicFile.WriteAllText(path, root.ToJsonString(JsonStore.Options));
        }
        catch { /* migration best-effort */ }
    }
}
