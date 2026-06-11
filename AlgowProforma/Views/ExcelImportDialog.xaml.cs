using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;
using ClosedXML.Excel;

namespace AlgowProforma.Views;

public partial class ExcelImportDialog : Window
{
    private string? _filePath;
    private List<ExcelImportPreviewRow> _rows = new();

    public List<Product> ImportedProducts { get; private set; } = new();

    public ExcelImportDialog() => InitializeComponent();

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnBrowseFile(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel dosyası|*.xlsx;*.xlsm",
            Title = "Excel dosyası seç",
        };
        if (dlg.ShowDialog() != true) return;

        _filePath = dlg.FileName;
        PathBox.Text = _filePath;

        try
        {
            var sheets = ExcelImportService.GetSheetNames(_filePath);
            SheetCombo.ItemsSource = sheets;
            if (sheets.Count > 0) SheetCombo.SelectedIndex = 0;
            // OnSheetChanged tetiklenecek; kolonları orada doldurup önizlemeyi yapacağız
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel dosyası açılamadı:\n{ex.Message}", "Hata",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSheetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath) || SheetCombo.SelectedItem is null) return;
        PopulateColumnCombos();
        AutoMapColumns();
        TryPreview();
    }

    private void PopulateColumnCombos()
    {
        if (string.IsNullOrEmpty(_filePath) || SheetCombo.SelectedItem is null) return;

        var headers = new List<string> { "(yok)" };
        try
        {
            using var wb = new XLWorkbook(_filePath);
            var ws = wb.Worksheet((string)SheetCombo.SelectedItem);
            var range = ws.RangeUsed();
            if (range != null)
            {
                int firstCol = range.FirstColumn().ColumnNumber();
                int lastCol = range.LastColumn().ColumnNumber();
                int firstRow = range.FirstRow().RowNumber();
                bool hasHeader = HeaderCheck.IsChecked == true;
                int headerRow = firstRow;
                int sampleRow = hasHeader ? firstRow + 1 : firstRow;

                for (int c = firstCol; c <= lastCol; c++)
                {
                    string header = "";
                    if (hasHeader)
                    {
                        try { header = ws.Cell(headerRow, c).GetString().Trim(); } catch { }
                    }
                    string sample = "";
                    try { sample = ws.Cell(sampleRow, c).GetString().Trim(); } catch { }
                    if (sample.Length > 22) sample = sample.Substring(0, 22) + "…";

                    var letter = XLHelper.GetColumnLetterFromNumber(c);
                    string label;
                    if (!string.IsNullOrWhiteSpace(header))
                        label = $"{letter} — {header}" + (string.IsNullOrWhiteSpace(sample) ? "" : $"  ·  {sample}");
                    else if (!string.IsNullOrWhiteSpace(sample))
                        label = $"Sütun {letter}  ·  {sample}";
                    else
                        label = $"Sütun {letter}";
                    headers.Add(label);
                }
            }
        }
        catch { }

        // Each combo's selected index = column number (0 = (yok), 1 = first column, ...)
        SetCombo(CodeCombo, headers);
        SetCombo(NameCombo, headers);
        SetCombo(PriceCombo, headers);

        var imgOptions = new List<string> { "Otomatik (gömülü görsel)" };
        for (int i = 1; i < headers.Count; i++) imgOptions.Add(headers[i] + "  (dosya yolu)");
        SetCombo(ImageCombo, imgOptions);
        ImageCombo.SelectedIndex = 0;
    }

    private static void SetCombo(ComboBox combo, IEnumerable<string> items)
    {
        combo.ItemsSource = items.ToList();
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void AutoMapColumns()
    {
        if (string.IsNullOrEmpty(_filePath) || SheetCombo.SelectedItem is null) return;
        try
        {
            using var wb = new XLWorkbook(_filePath);
            var ws = wb.Worksheet((string)SheetCombo.SelectedItem);
            var range = ws.RangeUsed();
            if (range is null) return;

            bool hasHeader = HeaderCheck.IsChecked == true;
            int firstRow = range.FirstRow().RowNumber();
            int lastRow = range.LastRow().RowNumber();
            int firstCol = range.FirstColumn().ColumnNumber();
            int lastCol = range.LastColumn().ColumnNumber();
            int dataStart = hasHeader ? firstRow + 1 : firstRow;

            int code = 0, name = 0, price = 0;

            // 1) Header keyword based detection
            if (hasHeader)
            {
                for (int c = firstCol; c <= lastCol; c++)
                {
                    string h;
                    try { h = ws.Cell(firstRow, c).GetString().Trim().ToLowerInvariant(); }
                    catch { h = ""; }
                    int idx = c - firstCol + 1;

                    if (code == 0 && (h.Contains("kod") || h.Contains("stok") || h.Contains("sku") || h == "code"))
                        code = idx;
                    else if (price == 0 && (h.Contains("fiyat") || h.Contains("price") || h.Contains("tutar") || h.Contains("ücret")))
                        price = idx;
                    else if (name == 0 && (h.Contains("ürün ad") || h.Contains("urun ad") || h.Contains("ürünad")
                                           || h.Contains("ürün is") || h.Contains("isim") || h == "ad"
                                           || h.Contains("açıklama") || h.Contains("aciklama")
                                           || h.Contains("tanım") || h.Contains("tanim")
                                           || h.Contains("name") || h.Contains("title") || h.Contains("description")))
                        name = idx;
                }
            }

            // 2) Data based detection — analyze each column's values
            int colCount = lastCol - firstCol + 1;
            var stats = new ColumnStats[colCount];
            int sampleLimit = Math.Min(lastRow, dataStart + 30);
            for (int c = firstCol; c <= lastCol; c++)
            {
                int relIdx = c - firstCol;
                stats[relIdx] = new ColumnStats();
                for (int r = dataStart; r <= sampleLimit; r++)
                {
                    string v;
                    try { v = ws.Cell(r, c).GetString().Trim(); } catch { continue; }
                    if (string.IsNullOrEmpty(v)) continue;
                    stats[relIdx].NonEmpty++;
                    stats[relIdx].TotalLen += v.Length;
                    if (decimal.TryParse(v.Replace("₺", "").Replace("TL", "").Replace("$", "").Replace("€", "").Trim(),
                            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) ||
                        decimal.TryParse(v, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("tr-TR"), out _))
                        stats[relIdx].NumericCount++;
                    else if (v.Any(char.IsLetter))
                        stats[relIdx].TextCount++;
                }
            }

            // Fallbacks
            if (code == 0)
            {
                // shortest text-ish column with letters+digits
                int best = -1; double bestScore = double.MaxValue;
                for (int i = 0; i < colCount; i++)
                {
                    if (stats[i].NonEmpty == 0) continue;
                    if (stats[i].TextCount == 0) continue;
                    double avg = (double)stats[i].TotalLen / stats[i].NonEmpty;
                    if (avg < 22 && avg < bestScore) { bestScore = avg; best = i; }
                }
                if (best >= 0) code = best + 1;
            }

            if (price == 0)
            {
                // most numeric column
                int best = -1; int bestNum = 0;
                for (int i = 0; i < colCount; i++)
                {
                    if (stats[i].NumericCount > bestNum && stats[i].NumericCount >= stats[i].TextCount)
                    {
                        bestNum = stats[i].NumericCount; best = i;
                    }
                }
                if (best >= 0 && bestNum > 0) price = best + 1;
            }

            if (name == 0)
            {
                // longest avg-text column that isn't code or price
                int best = -1; double bestAvg = 0;
                for (int i = 0; i < colCount; i++)
                {
                    if (i + 1 == code || i + 1 == price) continue;
                    if (stats[i].NonEmpty == 0 || stats[i].TextCount == 0) continue;
                    double avg = (double)stats[i].TotalLen / stats[i].NonEmpty;
                    if (avg > bestAvg) { bestAvg = avg; best = i; }
                }
                if (best >= 0) name = best + 1;
            }

            // Final guard: name and code shouldn't be the same
            if (name == code && name != 0)
            {
                int alt = -1; double bestAvg = 0;
                for (int i = 0; i < colCount; i++)
                {
                    if (i + 1 == code || i + 1 == price) continue;
                    if (stats[i].NonEmpty == 0) continue;
                    double avg = (double)stats[i].TotalLen / stats[i].NonEmpty;
                    if (avg > bestAvg) { bestAvg = avg; alt = i; }
                }
                name = alt >= 0 ? alt + 1 : 0;
            }

            if (code < CodeCombo.Items.Count) CodeCombo.SelectedIndex = code;
            if (name < NameCombo.Items.Count) NameCombo.SelectedIndex = name;
            if (price < PriceCombo.Items.Count) PriceCombo.SelectedIndex = price;
        }
        catch { }
    }

    private struct ColumnStats
    {
        public int NonEmpty;
        public int TotalLen;
        public int NumericCount;
        public int TextCount;
    }

    private void OnPreviewRequested(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        TryPreview();
    }

    private void TryPreview()
    {
        if (ImportBtn is null) return;
        if (string.IsNullOrEmpty(_filePath) || SheetCombo.SelectedItem is null)
        {
            ImportBtn.IsEnabled = false;
            return;
        }

        var opts = new ExcelImportOptions
        {
            SheetName = (string)SheetCombo.SelectedItem,
            HasHeaderRow = HeaderCheck.IsChecked == true,
            CodeColumn = ColumnIndex(CodeCombo),
            NameColumn = ColumnIndex(NameCombo),
            PriceColumn = ColumnIndex(PriceCombo),
            ImageColumn = ImageCombo.SelectedIndex == 0 ? 0 : ImageCombo.SelectedIndex,
        };

        try
        {
            _rows = ExcelImportService.Import(_filePath, opts);
            PreviewList.ItemsSource = _rows;
            EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ImportBtn.IsEnabled = _rows.Count > 0;
            int withImage = _rows.Count(r => r.HasImage);
            StatusText.Text = $"{_rows.Count} satır · {withImage} görsel hazır";
        }
        catch (Exception ex)
        {
            ImportBtn.IsEnabled = false;
            MessageBox.Show($"Önizleme oluşturulamadı:\n{ex.Message}", "Hata",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static int ColumnIndex(ComboBox combo)
    {
        // SelectedIndex 0 = (yok), 1 = first data column, ...
        return Math.Max(0, combo.SelectedIndex);
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var currency = ((ComboBoxItem?)CurrencyCombo.SelectedItem)?.Content?.ToString() ?? "TL";
        ImportedProducts = _rows.Select(r => ExcelImportService.ToProduct(r, currency)).ToList();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
