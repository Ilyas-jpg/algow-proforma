using System.ComponentModel;
using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class PriceBookWindow : Window
{
    private readonly PriceBookViewModel _vm;

    public PriceBookWindow()
    {
        InitializeComponent();
        _vm = new PriceBookViewModel();
        DataContext = _vm;
        Closing += OnClosing;
    }

    // Toplu zam / hücre düzenlemesi pencere X'iyle sessizce kaybolmasın (QuoteEditor H5 deseni).
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_vm.HasUnsavedChanges) return;

        var r = MessageBox.Show(this,
            "Kaydedilmemiş değişiklikler var (toplu zam dahil). Çıkmadan önce kaydedilsin mi?",
            "Fiyat Listesi", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (r == MessageBoxResult.Yes && !_vm.TrySaveForClose()) e.Cancel = true;
    }
}
