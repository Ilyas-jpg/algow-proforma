using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace AlgowProforma.Services;

public static class ShareService
{
    public static void CopyFileToClipboard(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var files = new StringCollection { path };
        Clipboard.SetFileDropList(files);
    }

    public static void ShowInExplorer(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    public static void ShareViaEmail(string path, string brandName)
    {
        if (File.Exists(path)) CopyFileToClipboard(path);
        var subject = WebUtility.UrlEncode($"{brandName} - Ürün Kataloğu");
        var body = WebUtility.UrlEncode(
            $"Merhaba,\r\n\r\n{brandName} ürün kataloğunu ekte paylaşıyorum. " +
            "PDF dosyası panonuza kopyalandı, Ctrl+V ile e-postaya ekleyebilirsiniz.\r\n\r\n" +
            "İyi günler dileriz.");
        Process.Start(new ProcessStartInfo($"mailto:?subject={subject}&body={body}") { UseShellExecute = true });
    }

    public static void ShareViaWhatsApp(string path, string brandName)
    {
        if (File.Exists(path)) CopyFileToClipboard(path);
        var text = WebUtility.UrlEncode($"{brandName} ürün kataloğumuzu paylaşıyoruz. PDF eki için sohbete yapıştırın.");
        Process.Start(new ProcessStartInfo($"https://wa.me/?text={text}") { UseShellExecute = true });
    }

    public static void ShareViaTelegram(string path, string brandName)
    {
        if (File.Exists(path)) CopyFileToClipboard(path);
        var text = WebUtility.UrlEncode($"{brandName} ürün kataloğu");
        Process.Start(new ProcessStartInfo($"https://t.me/share/url?url=&text={text}") { UseShellExecute = true });
    }

    public static void ShareViaInstagram(string path)
    {
        if (File.Exists(path)) CopyFileToClipboard(path);
        Process.Start(new ProcessStartInfo("https://www.instagram.com/direct/inbox/") { UseShellExecute = true });
    }

    public static void ShareViaLinkedIn(string brandName)
    {
        var text = WebUtility.UrlEncode($"{brandName} - Ürün Kataloğu");
        Process.Start(new ProcessStartInfo($"https://www.linkedin.com/feed/?shareActive=true&text={text}") { UseShellExecute = true });
    }
}
