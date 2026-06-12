using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AlgowProforma.Models;
using AlgowProforma.Services;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class QuotesWindow : Window
{
    private readonly QuotesViewModel _vm;

    public QuotesWindow()
    {
        InitializeComponent();
        _vm = new QuotesViewModel();
        DataContext = _vm;
    }

    private void OnOpenQuote(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Quote quote }) return;
        new QuoteEditorWindow(quote) { Owner = this }.ShowDialog();
        _vm.Reload();   // editör kaydetmeden kapatılmış olabilir — diskteki gerçek hâle dön
    }

    private void OnOpenSendHistory(object sender, RoutedEventArgs e)
        => new SendHistoryWindow { Owner = this }.ShowDialog();

    private void OnOpenPdf(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Quote quote }) return;
        var path = Path.Combine(AppPaths.QuotesDir, QuoteService.PdfFileName(quote));
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "Bu teklif için üretilmiş PDF bulunamadı.\nEditörden \"PDF Üret & Aç\" ile üretebilirsiniz.",
                "PDF Yok", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PDF açılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
