using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace AyTeknikKatalog.Services;

public enum AppTheme { Dark, Light }

/// <summary>
/// Runtime tema (dark/light) yöneticisi — "swappable palette dictionary" yöntemi.
///
/// NEDEN BÖYLE: WPF, ResourceDictionary'ye eklenen SolidColorBrush'ı (Source-merge'de) FREEZE eder →
/// brush.Color değiştirilemez. DynamicResource Color'lu brush ise donmaz ama paylaşıldığında
/// inheritance-context başına KLONLANIR → renk değişimi tüm tüketicilere tutarlı yayılmaz.
/// Çözüm: 9 tema brush'ı LİTERAL renkli (klonlanmaz; donsa da sorun yok, mutate etmiyoruz) bir
/// merged dictionary'de tutulur. UI bu brush'lara {DynamicResource XBrush} ile bağlanır. Tema değişimi
/// = bu merged dict'i komple yenisiyle DEĞİŞTİR → DynamicResource referansları yeniden çözülür, tüm UI
/// tutarlı güncellenir. Aksan (kobalt) brush'ları tema-bağımsız → Colors.xaml'de StaticResource kalır.
/// Tercih %LOCALAPPDATA%\AlgowProforma\theme.txt'te saklanır.
/// </summary>
public static class ThemeManager
{
    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    // Şu an uygulanmış palette merged-dict'i — toggle'da kaldırıp yenisini eklemek için tutulur.
    private static ResourceDictionary? _palette;

    private static string PrefPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlgowProforma", "theme.txt");

    // Elevation modeli: Background = canvas (+ inset field dolgusu), Surface = kart/panel/app-bar,
    // ElevatedSurface = hover/seçili/header-hücre. Dark'ta canvas en koyu, kart bir tık açık;
    // light'ta canvas yumuşak gri, kart beyaz → her iki temada da kart canvas'ın "üstünde" durur.
    private static readonly Dictionary<string, string> Dark = new()
    {
        ["BackgroundBrush"]      = "#FF0D0D13",
        ["SurfaceBrush"]         = "#FF17171F",
        ["ElevatedSurfaceBrush"] = "#FF202230",
        ["BorderBrush"]          = "#FF2A2B38",
        ["TextBrush"]            = "#FFF3F4F8",
        ["MutedBrush"]           = "#FF8E90A0",
        ["OverlayBrush"]         = "#B3050507",
        ["DangerBrush"]          = "#FFFF6B6B",
        ["DangerSurfaceBrush"]   = "#FF3A1F22",
    };

    private static readonly Dictionary<string, string> Light = new()
    {
        ["BackgroundBrush"]      = "#FFEDEFF4",
        ["SurfaceBrush"]         = "#FFFFFFFF",
        ["ElevatedSurfaceBrush"] = "#FFE7EAF1",
        ["BorderBrush"]          = "#FFDDE1EA",
        ["TextBrush"]            = "#FF15161C",
        ["MutedBrush"]           = "#FF666979",
        ["OverlayBrush"]         = "#40101017",
        ["DangerBrush"]          = "#FFE5484D",
        ["DangerSurfaceBrush"]   = "#FFFCECEE",
    };

    public static void Initialize()
    {
        var t = AppTheme.Dark;
        try
        {
            if (File.Exists(PrefPath) &&
                File.ReadAllText(PrefPath).Trim().Equals("light", StringComparison.OrdinalIgnoreCase))
                t = AppTheme.Light;
        }
        catch { /* tercih okunamadı — dark default */ }
        Apply(t);
    }

    public static void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        var pal = theme == AppTheme.Dark ? Dark : Light;
        var dict = new ResourceDictionary();
        foreach (var kv in pal)
            dict[kv.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kv.Value));

        // Eski palette'i çıkar, yenisini en sona ekle → {DynamicResource XBrush} yeni dict'e çözülür.
        if (_palette is not null)
            app.Resources.MergedDictionaries.Remove(_palette);
        app.Resources.MergedDictionaries.Add(dict);
        _palette = dict;

        Current = theme;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
            File.WriteAllText(PrefPath, theme == AppTheme.Dark ? "dark" : "light");
        }
        catch { /* tercih yazılamadı — kritik değil */ }
    }
}
