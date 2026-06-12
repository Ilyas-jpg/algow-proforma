using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

/// <summary>
/// Fiyat havuzu + müşteri için Excel içe/dışa aktarma (ClosedXML).
/// İçe aktarımda kolonlar BAŞLIK ADINA göre eşlenir (sıra önemli değil, kısmi eşleşme destekli) — kullanıcı dostu.
/// </summary>
public static class ExcelDataService
{
    // ---------- FİYAT LİSTESİ ----------

    public static List<PriceItem> ImportPriceItems(string xlsxPath, string defaultCurrency = "TL")
    {
        ImportLimits.EnsureExcelSize(xlsxPath);
        var list = new List<PriceItem>();
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheets.First();
        var range = ws.RangeUsed();
        if (range is null) return list;

        int headerRow = range.FirstRow().RowNumber();
        var cols = MapHeaders(ws, headerRow, range);

        int codeC = Find(cols, "kod", "stok kodu", "ürün kodu", "urun kodu", "malzeme kodu");
        int nameC = Find(cols, "ad", "ürün adı", "urun adi", "ürün", "urun", "malzeme", "açıklama", "aciklama");
        int descC = Find(cols, "açıklama", "aciklama", "detay", "tanım", "tanim");
        int unitC = Find(cols, "birim", "ölçü", "olcu", "birimi");
        int priceC = Find(cols, "birim fiyat", "fiyat", "tutar", "price", "satış fiyatı", "satis fiyati");
        int vatC = Find(cols, "kdv %", "kdv", "kdv oranı", "kdv orani", "vat");
        int catC = Find(cols, "kategori", "grup", "category");
        int curC = Find(cols, "para birimi", "döviz", "doviz", "currency", "kur");

        for (int row = headerRow + 1; row <= range.LastRow().RowNumber(); row++)
        {
            string code = Cell(ws, row, codeC);
            string name = Cell(ws, row, nameC);
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name)) continue;

            list.Add(new PriceItem
            {
                Code = code.Trim(),
                Name = name.Trim(),
                Description = (descC > 0 && descC != nameC) ? Cell(ws, row, descC).Trim() : "",
                Unit = OrDefault(Cell(ws, row, unitC).Trim(), "adet"),
                UnitPrice = CellDecimal(ws, row, priceC) ?? 0m,
                VatRate = CellDecimal(ws, row, vatC) ?? 20m,
                Category = Cell(ws, row, catC).Trim(),
                Currency = OrDefault(Cell(ws, row, curC).Trim().ToUpperInvariant(), defaultCurrency),
            });
        }
        return list;
    }

    public static void ExportPriceItems(string xlsxPath, IEnumerable<PriceItem> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Fiyat Listesi");
        string[] headers = { "Kod", "Ad", "Açıklama", "Birim", "Birim Fiyat", "KDV %", "Para Birimi", "Kategori" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var it in items)
        {
            ws.Cell(r, 1).Value = it.Code;
            ws.Cell(r, 2).Value = it.Name;
            ws.Cell(r, 3).Value = it.Description;
            ws.Cell(r, 4).Value = it.Unit;
            ws.Cell(r, 5).Value = it.UnitPrice;
            ws.Cell(r, 6).Value = it.VatRate;
            ws.Cell(r, 7).Value = it.Currency;
            ws.Cell(r, 8).Value = it.Category;
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(xlsxPath);
    }

    /// <summary>Ürün kütüphanesini Excel'e aktarır. Görsel dosyası taşınmaz (path makineye özgü) —
    /// görseller kütüphanenin kendi images\ havuzunda yaşamaya devam eder.</summary>
    public static void ExportProducts(string xlsxPath, IEnumerable<Product> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Ürün Kütüphanesi");
        string[] headers = { "Kod", "Ürün Adı", "Fiyat", "Para Birimi", "Kategori" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var p in items)
        {
            ws.Cell(r, 1).Value = p.Code;
            ws.Cell(r, 2).Value = p.Name;
            ws.Cell(r, 3).Value = p.Price;
            ws.Cell(r, 4).Value = p.Currency;
            ws.Cell(r, 5).Value = p.Category;
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(xlsxPath);
    }

    // ---------- MÜŞTERİLER ----------

    public static List<Customer> ImportCustomers(string xlsxPath)
    {
        ImportLimits.EnsureExcelSize(xlsxPath);
        var list = new List<Customer>();
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheets.First();
        var range = ws.RangeUsed();
        if (range is null) return list;

        int headerRow = range.FirstRow().RowNumber();
        var cols = MapHeaders(ws, headerRow, range);

        int companyC = Find(cols, "firma", "firma adı", "firma adi", "şirket", "sirket", "cari", "müşteri", "musteri");
        int contactC = Find(cols, "yetkili", "ilgili", "kişi", "kisi", "ad soyad", "yetkili kişi", "isim");
        int salC = Find(cols, "hitap", "cinsiyet", "unvan");
        int emailC = Find(cols, "e-posta", "eposta", "email", "e-mail", "mail", "mail adresi");
        int phoneC = Find(cols, "telefon", "tel", "gsm", "cep", "phone");
        int addrC = Find(cols, "adres", "address");
        int taxOfficeC = Find(cols, "vergi dairesi", "vd");
        int taxNoC = Find(cols, "vergi no", "vergi numarası", "vkn", "tckn");
        int tagsC = Find(cols, "etiket", "grup", "segment", "kategori", "tag");

        for (int row = headerRow + 1; row <= range.LastRow().RowNumber(); row++)
        {
            string company = Cell(ws, row, companyC);
            string contact = Cell(ws, row, contactC);
            string email = Cell(ws, row, emailC);
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(contact) && string.IsNullOrWhiteSpace(email))
                continue;

            list.Add(new Customer
            {
                CompanyName = company.Trim(),
                ContactName = contact.Trim(),
                Salutation = ParseSalutation(Cell(ws, row, salC)),
                Email = email.Trim(),
                Phone = Cell(ws, row, phoneC).Trim(),
                Address = Cell(ws, row, addrC).Trim(),
                TaxOffice = Cell(ws, row, taxOfficeC).Trim(),
                TaxNumber = Cell(ws, row, taxNoC).Trim(),
                Tags = Cell(ws, row, tagsC).Trim(),
            });
        }
        return list;
    }

    public static void ExportCustomers(string xlsxPath, IEnumerable<Customer> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Müşteriler");
        string[] headers = { "Firma", "Yetkili", "Hitap", "E-posta", "Telefon", "Adres", "Vergi Dairesi", "Vergi No", "Etiket" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var c in items)
        {
            ws.Cell(r, 1).Value = c.CompanyName;
            ws.Cell(r, 2).Value = c.ContactName;
            ws.Cell(r, 3).Value = c.Salutation switch { Salutation.Bey => "Bey", Salutation.Hanim => "Hanım", _ => "" };
            ws.Cell(r, 4).Value = c.Email;
            ws.Cell(r, 5).Value = c.Phone;
            ws.Cell(r, 6).Value = c.Address;
            ws.Cell(r, 7).Value = c.TaxOffice;
            ws.Cell(r, 8).Value = c.TaxNumber;
            ws.Cell(r, 9).Value = c.Tags;
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(xlsxPath);
    }

    // ---------- yardımcılar ----------

    private static Dictionary<string, int> MapHeaders(IXLWorksheet ws, int headerRow, IXLRange range)
    {
        var map = new Dictionary<string, int>();
        for (int c = range.FirstColumn().ColumnNumber(); c <= range.LastColumn().ColumnNumber(); c++)
        {
            var h = Cell(ws, headerRow, c).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(h) && !map.ContainsKey(h)) map[h] = c;
        }
        return map;
    }

    /// <summary>Önce birebir, sonra "içeren" eşleşme ile başlık → kolon bulur. Bulamazsa 0.</summary>
    private static int Find(Dictionary<string, int> cols, params string[] aliases)
    {
        foreach (var a in aliases)
            if (cols.TryGetValue(a, out var c)) return c;
        foreach (var a in aliases)
        {
            var hit = cols.FirstOrDefault(kv => kv.Key.Contains(a));
            if (hit.Value > 0) return hit.Value;
        }
        return 0;
    }

    private static string Cell(IXLWorksheet ws, int row, int col)
    {
        if (col <= 0) return "";
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty()) return "";
        try
        {
            if (cell.DataType == XLDataType.Number)
                return cell.GetValue<double>().ToString(CultureInfo.InvariantCulture);
            return cell.GetString() ?? "";
        }
        catch { return cell.Value.ToString() ?? ""; }
    }

    /// <summary>
    /// Sayısal alan okuma (fiyat/KDV). Hücre GERÇEK sayı ise (XLDataType.Number) değer doğrudan
    /// alınır — string'e çevirip ParseDecimal ayraç sezgisine sokmak 3+ ondalıklı değeri
    /// ("12.345" görünümü) Türkçe binlik sanıp 1000 kat şişiriyordu (H①). Sezgi yalnız METİN
    /// hücreleri içindir.
    /// </summary>
    internal static decimal? CellDecimal(IXLWorksheet ws, int row, int col)
    {
        if (col <= 0) return null;
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.Number)
                return cell.GetValue<decimal>();
        }
        catch { /* sayı okunamadı — metin yoluna düş */ }
        try { return ParseDecimal(cell.GetString() ?? ""); }
        catch { return null; }
    }

    /// <summary>
    /// Kültür yarıştırmak yerine AYRAÇ DÜZENİNİ analiz eder — yoksa Türkçe "12.500" (=12500)
    /// Invariant'ta 12,5 olarak parse olup fiyatı 1000 kat bozar (sessiz hata).
    /// Kural: hem . hem , varsa SON görülen ondalıktır; sadece , → Türkçe; sadece . → tek nokta+1-2 hane ise ondalık, değilse binlik.
    /// TEK doğru kaynak: ExcelImportService (ürün/kütüphane importu) de bunu kullanır (H1 fix).
    /// </summary>
    internal static decimal? ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace("₺", "").Replace("TL", "").Replace("$", "").Replace("€", "").Replace("%", "").Trim();
        if (s.Length == 0) return null;

        var tr = new CultureInfo("tr-TR");
        var inv = CultureInfo.InvariantCulture;
        bool hasComma = s.Contains(','), hasDot = s.Contains('.');

        CultureInfo culture;
        if (hasComma && hasDot)
            culture = s.LastIndexOf(',') > s.LastIndexOf('.') ? tr : inv;   // 1.234,56→tr · 1,234.56→inv
        else if (hasComma)
            culture = tr;                                                    // 12,5 → 12.5
        else if (hasDot)
        {
            int dots = s.Length - s.Replace(".", "").Length;
            int afterDot = s.Length - s.LastIndexOf('.') - 1;
            culture = (dots == 1 && afterDot <= 2) ? inv : tr;               // 12.5→inv · 12.500/1.234.567→tr binlik
        }
        else
            culture = inv;                                                   // düz tam sayı

        return decimal.TryParse(s, NumberStyles.Any, culture, out var v) ? v : (decimal?)null;
    }

    private static string OrDefault(string v, string d) => string.IsNullOrWhiteSpace(v) ? d : v;

    private static Salutation ParseSalutation(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.StartsWith("bay") && !s.StartsWith("bayan")) return Salutation.Bey;
        if (s == "bey" || s == "erkek" || s == "e" || s == "mr") return Salutation.Bey;
        if (s.StartsWith("bayan") || s == "hanım" || s == "hanim" || s == "kadın" || s == "kadin" || s == "k" || s == "ms" || s == "mrs") return Salutation.Hanim;
        return Salutation.Yok;
    }
}
