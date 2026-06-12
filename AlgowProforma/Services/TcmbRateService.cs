using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AlgowProforma.Services;

/// <summary>Günün TCMB döviz satış kurları (today.xml'in Tarih attribute'u + USD/EUR ForexSelling).</summary>
public sealed record TcmbRates(string DateText, decimal UsdSelling, decimal EurSelling);

/// <summary>
/// TCMB günlük kur servisi — resmî ücretsiz XML (kurlar/today.xml), anahtar/abonelik gerektirmez.
/// Teklif editöründeki "TCMB Kurunu Çek" butonu ExchangeRateNote'u buradan doldurur.
/// Ağ hatası çağırana fırlar (UI status bar'da gösterir) — teklif akışını bloklamaz.
/// </summary>
public static class TcmbRateService
{
    public const string TodayUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";

    // Tek paylaşımlı HttpClient (soket tükenmesi önlemi, GmailService ile aynı disiplin).
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static async Task<TcmbRates> FetchTodayAsync(CancellationToken ct = default)
        => Parse(await _http.GetStringAsync(TodayUrl, ct));

    /// <summary>XML çekirdeği — ağsız test edilebilir. ForexSelling boşsa BanknoteSelling'e düşer.</summary>
    internal static TcmbRates Parse(string xml)
    {
        var root = XDocument.Parse(xml).Root
            ?? throw new InvalidDataException("TCMB kur XML'i boş.");
        var date = (string?)root.Attribute("Tarih") ?? "";

        decimal Rate(string code)
        {
            var cur = root.Elements("Currency")
                          .FirstOrDefault(e => string.Equals((string?)e.Attribute("Kod"), code, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"TCMB XML'inde {code} kuru bulunamadı.");
            var s = (string?)cur.Element("ForexSelling");
            if (string.IsNullOrWhiteSpace(s)) s = (string?)cur.Element("BanknoteSelling");
            if (string.IsNullOrWhiteSpace(s))
                throw new InvalidDataException($"TCMB XML'inde {code} satış kuru boş.");
            // TCMB ondalık ayracı NOKTA kullanır — invariant parse (TR culture 41.2345'i 412345 yapar!)
            return decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        return new TcmbRates(date, Rate("USD"), Rate("EUR"));
    }

    /// <summary>Teklifin "Kur Notu" alanına yazılacak hazır cümle (TR biçim: 41,2345).</summary>
    public static string BuildNote(TcmbRates r)
    {
        var tr = new CultureInfo("tr-TR");
        var date = string.IsNullOrWhiteSpace(r.DateText) ? DateTime.Today.ToString("dd.MM.yyyy", tr) : r.DateText;
        return $"1 USD = {r.UsdSelling.ToString("#,##0.0000", tr)} TL · 1 EUR = {r.EurSelling.ToString("#,##0.0000", tr)} TL (TCMB döviz satış, {date})";
    }
}
