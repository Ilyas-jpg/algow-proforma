using System.ComponentModel;
using System.Windows;
using AlgowProforma.Models;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class QuoteEditorWindow : Window
{
    private readonly QuoteEditorViewModel _vm;

    public QuoteEditorWindow(Quote? existing = null)
    {
        InitializeComponent();
        _vm = new QuoteEditorViewModel(existing);
        DataContext = _vm;
        Closing += OnClosing;
        // Önizleme debounce timer'ı pencereyle ölmeli — kapalı pencere üzerinde render başlatmasın.
        Closed += (_, _) => _vm.Shutdown();
    }

    // H5: kalemli/değişmiş teklif pencere X'iyle sessizce kaybolmasın.
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_vm.HasUnsavedChanges) return;

        var r = MessageBox.Show(this,
            "Kaydedilmemiş değişiklikler var. Çıkmadan önce kaydedilsin mi?",
            "Fiyat Teklifi", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (r == MessageBoxResult.Yes && !_vm.TrySaveForClose()) e.Cancel = true;
    }
}
