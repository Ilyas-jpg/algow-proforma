using System.ComponentModel;
using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class BulkSendWindow : Window
{
    private readonly BulkSendViewModel _vm;
    private bool _closeAfterCancel;

    public BulkSendWindow()
    {
        InitializeComponent();
        _vm = new BulkSendViewModel();
        DataContext = _vm;
        Closing += OnClosing;
        _vm.PropertyChanged += OnVmPropertyChanged;
        Closed += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;   // L3: tutarlı abonelik temizliği
    }

    // H3: gönderim sürerken pencere kapatılırsa döngü görünmez devam ediyordu —
    // artık önce iptal istenir; gönderim sıradaki alıcıda durunca pencere kendiliğinden kapanır.
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_vm.Busy) return;

        var r = MessageBox.Show(this,
            "Gönderim sürüyor. İptal edilip kapatılsın mı?\n\n(Gönderilmiş mailler geri alınamaz; sıradaki alıcıdan itibaren durur.)",
            "Toplu Gönderim", MessageBoxButton.YesNo, MessageBoxImage.Question);
        e.Cancel = true;                       // iptal işlenene kadar açık kal
        if (r != MessageBoxResult.Yes) return;

        _closeAfterCancel = true;
        _vm.CancelSendCommand.Execute(null);   // Busy düşünce OnVmPropertyChanged kapatır
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_closeAfterCancel && e.PropertyName == nameof(BulkSendViewModel.Busy) && !_vm.Busy)
        {
            _closeAfterCancel = false;
            Close();
        }
    }
}
