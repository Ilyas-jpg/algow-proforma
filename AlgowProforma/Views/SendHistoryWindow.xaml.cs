using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.Views;

/// <summary>
/// E-posta gönderim logu görüntüleyicisi — log v1.4.0'dan beri JSONL'a yazılıyordu ama uygulamada
/// hiçbir pencere OKUMUYORDU (KULLANIM vaat ediyordu). Tekli (E-posta Önizleme) + toplu (BulkSend)
/// tüm gönderimler; arama + yalnız-başarısız süzgeci. Salt-okunur.
/// </summary>
public partial class SendHistoryWindow : Window
{
    private List<EmailSendLog> _all = new();

    public SendHistoryWindow()
    {
        InitializeComponent();
        Reload();
    }

    private void Reload()
    {
        try { _all = new EmailLogService().Load(); }
        catch (Exception ex)
        {
            _all = new();
            MessageBox.Show(this, "Gönderim logu okunamadı: " + ex.Message, "Hata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (LogGrid is null) return;   // InitializeComponent sırasında erken event
        var q = (SearchBox?.Text ?? "").Trim();
        bool onlyFailed = OnlyFailedCheck?.IsChecked == true;

        IEnumerable<EmailSendLog> view = _all;
        if (onlyFailed) view = view.Where(l => !l.Success);
        if (q.Length > 0)
            view = view.Where(l =>
                Contains(l.QuoteNo, q) || Contains(l.ToEmail, q) ||
                Contains(l.RecipientCompany, q) || Contains(l.Subject, q) ||
                Contains(l.Error, q));

        var list = view.ToList();
        LogGrid.ItemsSource = list;

        int ok = _all.Count(l => l.Success), fail = _all.Count - ok;
        SummaryText.Text = _all.Count == 0
            ? "Henüz gönderim kaydı yok — e-posta gönderimleri burada listelenir."
            : $"{_all.Count} kayıt  ·  ✓ {ok} başarılı  ·  ✗ {fail} başarısız" +
              (list.Count == _all.Count ? "" : $"  ·  süzgeç: {list.Count} gösteriliyor");
    }

    private static bool Contains(string? hay, string needle) =>
        (hay ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();
    private void OnRefresh(object sender, RoutedEventArgs e) => Reload();
}
