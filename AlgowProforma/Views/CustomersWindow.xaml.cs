using System.ComponentModel;
using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class CustomersWindow : Window
{
    private readonly CustomersViewModel _vm;

    public CustomersWindow()
    {
        InitializeComponent();
        _vm = new CustomersViewModel();
        DataContext = _vm;
        Closing += OnClosing;
    }

    // Hücre düzenlemesi / yeni müşteri pencere X'iyle sessizce kaybolmasın (QuoteEditor H5 deseni).
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_vm.HasUnsavedChanges) return;

        var r = MessageBox.Show(this,
            "Kaydedilmemiş değişiklikler var. Çıkmadan önce kaydedilsin mi?",
            "Müşteriler", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (r == MessageBoxResult.Yes && !_vm.TrySaveForClose()) e.Cancel = true;
    }
}
