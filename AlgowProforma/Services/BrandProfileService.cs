using System.IO;
using System.Text.Json;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// Kurulum başına kalıcı MARKA PROFİLİ (firma adı, logo, iletişim, slogan, filigran).
/// Konum: Belgeler\Algow Proforma Kataloglar\data\brand-profile.json — yani KURULUM KLASÖRÜNÜN DIŞINDA.
/// Sonuç: app güncellemesi / yeniden kurulum (Programs\AlgowProforma değişse bile) markayı SİLMEZ.
/// Yeni kataloglara otomatik uygulanır; kullanıcı marka alanlarını düzenledikçe yazılır.
/// </summary>
public static class BrandProfileService
{
    private static string FilePath => Path.Combine(AppPaths.DataDir, "brand-profile.json");

    public static BrandInfo? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try { return JsonSerializer.Deserialize<BrandInfo>(File.ReadAllText(FilePath), JsonStore.Options); }
        catch { return null; }
    }

    public static void Save(BrandInfo brand)
    {
        try { JsonStore.SaveObject(FilePath, brand); } catch { /* best-effort — kritik değil */ }
    }
}
