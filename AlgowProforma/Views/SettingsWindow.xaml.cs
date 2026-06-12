using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel();
        DataContext = _vm;
    }

    private void OnSavePassword(object sender, RoutedEventArgs e) => _vm.SavePassword(PwBox.Password);

    private void OnClearPassword(object sender, RoutedEventArgs e)
    {
        PwBox.Clear();
        _vm.ClearPassword();
    }

    private async void OnTestSend(object sender, RoutedEventArgs e) => await _vm.TestSendAsync(PwBox.Password);

    private async void OnGoogleSignIn(object sender, RoutedEventArgs e) => await _vm.GoogleSignInAsync();

    private async void OnConnectGmail(object sender, RoutedEventArgs e) => await _vm.ConnectGmailAsync();

    private async void OnDisconnectGmail(object sender, RoutedEventArgs e) => await _vm.DisconnectGmailAsync();

    private async void OnCreateBackup(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "ZIP yedek (*.zip)|*.zip",
            FileName = $"algow-proforma-yedek-{System.DateTime.Now:yyyyMMdd-HHmm}.zip",
            Title = "Yedek dosyasını kaydet",
        };
        if (dlg.ShowDialog(this) != true) return;
        await _vm.CreateBackupAsync(dlg.FileName);
    }

    private async void OnRestoreBackup(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "ZIP yedek (*.zip)|*.zip",
            Title = "Geri yüklenecek yedeği seç",
        };
        if (dlg.ShowDialog(this) != true) return;

        var confirm = MessageBox.Show(this,
            "Seçilen yedek geri yüklenecek.\n\n" +
            "Mevcut veriniz SİLİNMEZ — \"pre-restore\" güvenlik kopyasına taşınır.\n" +
            "İşlem sonrası uygulama OTOMATİK yeniden başlatılır.\n\nDevam edilsin mi?",
            "Yedekten Geri Yükle", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var ok = await _vm.RestoreBackupAsync(dlg.FileName);
        if (!ok) return;

        // Restart artık otomatik: eski akışta kullanıcı yeniden başlatmayınca bellekteki eski
        // veriyle çalışmaya devam edip geri yüklenen veriyi eziyordu (2 inceleme merceği bağımsız buldu).
        MessageBox.Show(this,
            "Veri geri yüklendi. Uygulama taze veriyle yeniden başlatılıyor.",
            "Geri Yükleme Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
        App.RestartForDataReload();
    }
}
