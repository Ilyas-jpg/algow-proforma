using System.IO;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// TCMB today.xml parse çekirdeği — ağsız. Kritik: TCMB ondalık ayracı NOKTA kullanır;
/// TR-culture parse "41.1751"i 411751 yapardı (Excel 1000× fiyat bozulmasının kur versiyonu).
/// </summary>
public class TcmbRateTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Tarih_Date Tarih="12.06.2026" Date="06/12/2026" Bulten_No="2026/111">
          <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD">
            <Unit>1</Unit><Isim>ABD DOLARI</Isim><CurrencyName>US DOLLAR</CurrencyName>
            <ForexBuying>41.1011</ForexBuying><ForexSelling>41.1751</ForexSelling>
            <BanknoteBuying>41.0723</BanknoteBuying><BanknoteSelling>41.2369</BanknoteSelling>
          </Currency>
          <Currency CrossOrder="9" Kod="EUR" CurrencyCode="EUR">
            <Unit>1</Unit><Isim>EURO</Isim><CurrencyName>EURO</CurrencyName>
            <ForexBuying>44.2509</ForexBuying><ForexSelling>44.3306</ForexSelling>
          </Currency>
        </Tarih_Date>
        """;

    [Fact]
    public void Parse_ReadsDateAndSellingRates_InvariantDecimal()
    {
        var r = TcmbRateService.Parse(SampleXml);

        Assert.Equal("12.06.2026", r.DateText);
        Assert.Equal(41.1751m, r.UsdSelling);   // 411751 DEĞİL — invariant parse
        Assert.Equal(44.3306m, r.EurSelling);
    }

    [Fact]
    public void Parse_FallsBackToBanknoteSelling_WhenForexEmpty()
    {
        var xml = SampleXml.Replace("<ForexSelling>41.1751</ForexSelling>", "<ForexSelling></ForexSelling>");
        var r = TcmbRateService.Parse(xml);
        Assert.Equal(41.2369m, r.UsdSelling);   // BanknoteSelling'e düştü
    }

    [Fact]
    public void Parse_MissingCurrency_ThrowsClearError()
    {
        var xml = """<Tarih_Date Tarih="12.06.2026"><Currency Kod="USD"><ForexSelling>41.1</ForexSelling></Currency></Tarih_Date>""";
        var ex = Assert.Throws<InvalidDataException>(() => TcmbRateService.Parse(xml));
        Assert.Contains("EUR", ex.Message);
    }

    [Fact]
    public void BuildNote_TurkishFormat_WithDateAndBothRates()
    {
        var note = TcmbRateService.BuildNote(new TcmbRates("12.06.2026", 41.1751m, 44.3306m));

        Assert.Contains("41,1751", note);   // TR ondalık biçimi
        Assert.Contains("44,3306", note);
        Assert.Contains("TCMB", note);
        Assert.Contains("12.06.2026", note);
    }
}
