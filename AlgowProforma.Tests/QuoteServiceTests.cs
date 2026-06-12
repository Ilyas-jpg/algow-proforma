using System;
using System.IO;
using System.Linq;
using AlgowProforma.Models;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// QuoteService integration: gerçek dosya I/O'su TempLibraryRoot ile geçici köke yönlendirilir —
/// kullanıcının gerçek Belgeler verisi (Teklifler/, counters.json, settings.json) etkilenmez.
/// Kapsam: round-trip (Türkçe + decimal hassasiyeti), numara atama zinciri (Save → GenerateNextNo
/// → SettingsService.Load → QuoteNoPrefix), bozuk dosya toleransı, silme.
/// </summary>
[Collection("AppPathsIsolation")]
public class QuoteServiceTests : IDisposable
{
    private readonly TempLibraryRoot _tmp = new();
    private readonly QuoteService _svc = new();

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public void SaveLoad_RoundTrip_PreservesCoreFields()
    {
        var q = new Quote
        {
            QuoteNo = "TST-2026-0042",
            Currency = "USD",
            CustomerCompany = "Çelikel Isı Sanayi Ltd. Şti.",
            CustomerContact = "İlkay Özgür",
            DiscountMode = DiscountMode.Yuzde,
            DiscountValue = 5m,
            PaymentTerms = "%50 peşin, kalan teslimatta",
            Notes = "Fiyatlara KDV dâhil değildir.",
        };
        q.Lines.Add(new QuoteLine { Code = "KV-50", Name = "Küresel Vana DN50 — ½\" rakorlu", Quantity = 12m, UnitPrice = 1850.75m, DiscountPct = 10m, VatRate = 20m });
        q.Lines.Add(new QuoteLine { Name = "Conta seti", Quantity = 3.5m, UnitPrice = 99.90m });

        _svc.Save(q);
        var loaded = _svc.Load(q.Id);

        Assert.NotNull(loaded);
        Assert.Equal("TST-2026-0042", loaded!.QuoteNo);
        Assert.Equal("USD", loaded.Currency);
        Assert.Equal("Çelikel Isı Sanayi Ltd. Şti.", loaded.CustomerCompany);
        Assert.Equal("İlkay Özgür", loaded.CustomerContact);
        Assert.Equal(DiscountMode.Yuzde, loaded.DiscountMode);
        Assert.Equal(5m, loaded.DiscountValue);
        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal("Küresel Vana DN50 — ½\" rakorlu", loaded.Lines[0].Name);
        Assert.Equal(1850.75m, loaded.Lines[0].UnitPrice);
        Assert.Equal(3.5m, loaded.Lines[1].Quantity);
        Assert.Equal("Fiyatlara KDV dâhil değildir.", loaded.Notes);
    }

    [Fact]
    public void Save_EmptyQuoteNo_AssignsSequentialFromSettingsPrefix()
    {
        // Gerçek zincir: Save → GenerateNextNo → SettingsService.Load → QuoteNoPrefix
        new SettingsService().Save(new AppSettings { QuoteNoPrefix = "TST" });
        int year = DateTime.Today.Year;

        var q1 = new Quote(); var q2 = new Quote();
        _svc.Save(q1);
        _svc.Save(q2);

        Assert.Equal($"TST-{year}-0001", q1.QuoteNo);
        Assert.Equal($"TST-{year}-0002", q2.QuoteNo);
    }

    [Fact]
    public void Save_ExistingQuoteNo_NotRegenerated_CounterNotConsumed()
    {
        var manual = new Quote { QuoteNo = "ELLE-2026-9999" };
        _svc.Save(manual);
        Assert.Equal("ELLE-2026-9999", manual.QuoteNo);

        var auto = new Quote();
        _svc.Save(auto);
        Assert.EndsWith("-0001", auto.QuoteNo);   // farklı ön-ekli elle numara sayacı İTMEZ
    }

    [Fact]
    public void CountersReset_DoesNotReissueExistingNumber()
    {
        new SettingsService().Save(new AppSettings { QuoteNoPrefix = "TKF" });
        int year = DateTime.Today.Year;

        var q1 = new Quote(); var q2 = new Quote();
        _svc.Save(q1);
        _svc.Save(q2);
        Assert.Equal($"TKF-{year}-0002", q2.QuoteNo);

        // counters.json kayboldu (yedekten kısmi dönüş / elle temizlik) — sayaç sıfırdan başlar.
        File.Delete(AppPaths.CountersFile);

        var q3 = new Quote();
        _svc.Save(q3);

        // Eski davranış: TKF-{yıl}-0001 İKİNCİ kez kesilirdi (iki müşteride aynı fiş no).
        Assert.Equal($"TKF-{year}-0003", q3.QuoteNo);
    }

    [Fact]
    public void LoadAll_SkipsCorruptFiles_OrdersNewestFirst()
    {
        var old = new Quote { CreatedAt = DateTime.Now.AddDays(-2), QuoteNo = "A" };
        var fresh = new Quote { CreatedAt = DateTime.Now, QuoteNo = "B" };
        _svc.Save(old);
        _svc.Save(fresh);
        File.WriteAllText(Path.Combine(AppPaths.QuotesDir, "bozuk.json"), "{ json degil ");

        var all = _svc.LoadAll();

        Assert.Equal(2, all.Count);                       // bozuk atlandı
        Assert.Equal("B", all[0].QuoteNo);                // en yeni önce
        Assert.Equal("A", all[1].QuoteNo);
    }

    [Fact]
    public void Load_Missing_ReturnsNull_And_Delete_MovesToTrash()
    {
        Assert.Null(_svc.Load("yok-boyle-bir-id"));

        var q = new Quote { QuoteNo = "SIL-1" };
        _svc.Save(q);
        Assert.NotNull(_svc.Load(q.Id));

        _svc.Delete(q);
        Assert.Null(_svc.Load(q.Id));
        Assert.Empty(Directory.GetFiles(AppPaths.QuotesDir, q.Id + "*"));   // kökten kalktı (.tmp artığı da yok)
        // Kalıcı silme DEĞİL: JSON geri-dönülebilir .trash'te yaşıyor (30 gün purge penceresi).
        var trash = Path.Combine(AppPaths.QuotesDir, ".trash");
        Assert.True(File.Exists(Path.Combine(trash, q.Id + ".json")));
    }
}
