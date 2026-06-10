using System.Windows;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

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
}
