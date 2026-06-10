using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

/// <summary>
/// SMTP parolasını Windows DPAPI ile şifreli saklar (CurrentUser scope).
/// Düz metin parola hiçbir yerde tutulmaz; dosya kopyalansa bile başka kullanıcı/makine çözemez.
/// </summary>
public static class CredentialStore
{
    private static string PasswordFile => Path.Combine(AppPaths.DataDir, "smtp.bin");
    private static string GmailTokenFile => Path.Combine(AppPaths.DataDir, "google-gmail.bin");

    public static bool HasPassword => File.Exists(PasswordFile);
    public static bool HasGmailCredential => File.Exists(GmailTokenFile);

    public static void SavePassword(string password)
    {
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password ?? ""), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PasswordFile, encrypted);
    }

    public static string? LoadPassword()
    {
        if (!File.Exists(PasswordFile)) return null;
        try
        {
            var decrypted = ProtectedData.Unprotect(
                File.ReadAllBytes(PasswordFile), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch { return null; }
    }

    public static void Clear()
    {
        if (File.Exists(PasswordFile)) File.Delete(PasswordFile);
    }

    public static void SaveGmailCredential(GoogleGmailCredential credential)
    {
        credential.UpdatedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(credential, JsonStore.Options);
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GmailTokenFile, encrypted);
    }

    public static GoogleGmailCredential? LoadGmailCredential()
    {
        if (!File.Exists(GmailTokenFile)) return null;
        try
        {
            var decrypted = ProtectedData.Unprotect(
                File.ReadAllBytes(GmailTokenFile), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<GoogleGmailCredential>(Encoding.UTF8.GetString(decrypted), JsonStore.Options);
        }
        catch { return null; }
    }

    public static void ClearGmailCredential()
    {
        if (File.Exists(GmailTokenFile)) File.Delete(GmailTokenFile);
    }
}
