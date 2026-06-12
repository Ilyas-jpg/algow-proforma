using System;
using System.IO;
using System.Linq;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// EmailLogService JSONL geçişi: Append O(1) satır ekler; eski tek-array dosya ilk Append'te
/// göç eder; yarım/bozuk satır tek kayıt feda edilerek atlanır (log'un kalanı yaşar).
/// </summary>
[Collection("AppPathsIsolation")]
public class EmailLogTests
{
    private static EmailSendLog L(string to, int minutesAgo) => new()
    {
        ToEmail = to,
        Subject = "Test",
        Success = true,
        SentAt = DateTime.Now.AddMinutes(-minutesAgo),
    };

    [Fact]
    public void Append_MigratesLegacyArrayFile_ThenAppendsAsJsonl()
    {
        using var tmp = new TempLibraryRoot();
        // Eski format: tek JSON array (her Append tüm dosyayı yeniden yazardı)
        JsonStore.SaveList(AppPaths.EmailLogsFile, new[] { L("a@x.com", 30), L("b@x.com", 20) });

        var svc = new EmailLogService();
        svc.Append(L("c@x.com", 10));

        var text = File.ReadAllText(AppPaths.EmailLogsFile);
        Assert.False(text.TrimStart().StartsWith("["));   // array değil, JSONL
        Assert.Equal(3, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        var logs = svc.Load();
        Assert.Equal(3, logs.Count);
        Assert.Equal("c@x.com", logs[0].ToEmail);          // en yeni başta (eski davranışla birebir)
        Assert.Equal("a@x.com", logs[2].ToEmail);
    }

    [Fact]
    public void Load_SkipsCorruptLine_KeepsRest()
    {
        using var tmp = new TempLibraryRoot();
        var svc = new EmailLogService();
        svc.Append(L("a@x.com", 5));
        File.AppendAllText(AppPaths.EmailLogsFile, "{\"ToEmail\":\"yarim-sat");   // çökme anı simülasyonu
        svc.Append(L("b@x.com", 1));

        var logs = svc.Load();
        Assert.Equal(2, logs.Count);
        Assert.Equal(new[] { "b@x.com", "a@x.com" }, logs.Select(l => l.ToEmail).ToArray());
    }

    [Fact]
    public void AppendLoad_RoundTrip_TurkishCharsSurvive()
    {
        using var tmp = new TempLibraryRoot();
        var svc = new EmailLogService();
        var log = L("musteri@ornek.com", 2);
        log.RecipientCompany = "Şirket ĞÜŞİÖÇ ığüşiöç";
        svc.Append(log);

        var loaded = svc.Load().Single();
        Assert.Equal("Şirket ĞÜŞİÖÇ ığüşiöç", loaded.RecipientCompany);
        // JSONL satırı düz UTF-8 yazılır — \u kaçışı yok (JsonStore encoder disiplini)
        Assert.Contains("Şirket", File.ReadAllText(AppPaths.EmailLogsFile));
    }

    [Fact]
    public void Load_EmptyOrMissingFile_ReturnsEmpty()
    {
        using var tmp = new TempLibraryRoot();
        Assert.Empty(new EmailLogService().Load());
        File.WriteAllText(AppPaths.EmailLogsFile, "");
        Assert.Empty(new EmailLogService().Load());
    }

    [Fact]
    public void Append_RotatesPreviousYearFile_LoadMergesArchives()
    {
        using var tmp = new TempLibraryRoot();
        var svc = new EmailLogService();
        svc.Append(L("eski@x.com", 60));

        // Dosya geçen yıldan kalmış gibi: rotasyon tetiklenir, arşive devrilir.
        int lastYear = DateTime.Now.Year - 1;
        File.SetLastWriteTime(AppPaths.EmailLogsFile, new DateTime(lastYear, 12, 30, 12, 0, 0));

        svc.Append(L("yeni@x.com", 1));

        var dir = Path.GetDirectoryName(AppPaths.EmailLogsFile)!;
        var stem = Path.GetFileNameWithoutExtension(AppPaths.EmailLogsFile);
        var ext = Path.GetExtension(AppPaths.EmailLogsFile);
        Assert.True(File.Exists(Path.Combine(dir, $"{stem}-{lastYear}{ext}")));   // arşiv doğdu

        // Aktif dosyada yalnız yeni kayıt; Load arşivle birleştirip ikisini de döndürür.
        Assert.Single(EmailLogService.Parse(File.ReadAllText(AppPaths.EmailLogsFile)));
        var all = svc.Load();
        Assert.Equal(2, all.Count);
        Assert.Equal("yeni@x.com", all[0].ToEmail);
        Assert.Equal("eski@x.com", all[1].ToEmail);
    }

    [Fact]
    public void MigrateLegacyArray_LeavesPreJsonlBackup()
    {
        using var tmp = new TempLibraryRoot();
        JsonStore.SaveList(AppPaths.EmailLogsFile, new[] { L("a@x.com", 30) });

        new EmailLogService().Append(L("b@x.com", 1));

        // Karışık-dosya migrasyonunda array içeriği satır-moduna düşüp KAYBOLABİLİYORDU —
        // rewrite öncesi orijinalin yedeği alınır.
        Assert.True(File.Exists(AppPaths.EmailLogsFile + ".pre-jsonl.bak"));
    }
}
