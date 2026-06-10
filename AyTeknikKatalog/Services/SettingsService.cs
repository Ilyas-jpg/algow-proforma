using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

/// <summary>Uygulama ayarlarının (mail hesabı + e-posta şablonu) kalıcılığı (settings.json).</summary>
public class SettingsService
{
    public AppSettings Load() => JsonStore.LoadOrNew<AppSettings>(AppPaths.SettingsFile);

    public void Save(AppSettings settings) => JsonStore.SaveObject(AppPaths.SettingsFile, settings);
}
