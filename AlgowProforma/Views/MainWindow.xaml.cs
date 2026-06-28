using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AlgowProforma.Models;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var vm = DataContext as MainViewModel;
            vm?.CheckCrashRecovery();
            vm?.RefreshCoverPreview();
            // NOT: EnsureDesignThumbnails artık burada ÇAĞRILMAZ — açılışta 13 kapak render'ı
            // ilk ~20 sn tıklamaları yutabiliyordu (canlı testte yakalandı). Tasarımlar sekmesi
            // ilk seçildiğinde başlar (OnMainTabChanged); sekme açılana kadar maliyet sıfır.
        };
    }

    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender)) return;   // sekme içindeki combobox bubbling'i değil
        if (sender is TabControl tc && ReferenceEquals(tc.SelectedItem, DesignsTab)
            && DataContext is MainViewModel vm)
            vm.EnsureDesignThumbnails();   // idempotent — guard VM'de
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Restore-restart yolunda autosave YAZILMAZ: bellekteki eski oturum, az önce yedekten
        // geri yüklenen veriyi autosave üzerinden geri taşırdı ("önceki oturum?" sorusu kirlenir).
        if (App.IsRestartingForRestore) return;

        if (DataContext is MainViewModel vm)
        {
            vm.SaveSessionNow(); // kapanışta bekleyen değişikliği session'a yaz → "kaldığın yerden devam"
            if (!vm.ConfirmDiscardChanges()) e.Cancel = true;
        }
    }

    private void OnRecentButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (DataContext is MainViewModel vm) vm.RefreshRecentEntries();
        if (btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>Toolbar dropdown butonu (Teklifler ▾ / Kayıtlar ▾) — ContextMenu'yu butonun ALTINDA açar
    /// (sağ-tık menüsü gibi imlecte değil), gerçek bir menü düğmesi hissi verir.</summary>
    private void OnDropdownButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        btn.ContextMenu.IsOpen = true;
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => Services.ThemeManager.Toggle();

    private void OnNewQuoteClick(object sender, RoutedEventArgs e)
        => new QuoteEditorWindow { Owner = this }.ShowDialog();

    private void OnQuotesClick(object sender, RoutedEventArgs e)
        => new QuotesWindow { Owner = this }.ShowDialog();

    private void OnBulkSendClick(object sender, RoutedEventArgs e)
        => new BulkSendWindow { Owner = this }.ShowDialog();

    private void OnPriceBookClick(object sender, RoutedEventArgs e)
        => new PriceBookWindow { Owner = this }.ShowDialog();

    private void OnCustomersClick(object sender, RoutedEventArgs e)
        => new CustomersWindow { Owner = this }.ShowDialog();

    private void OnSettingsClick(object sender, RoutedEventArgs e)
        => new SettingsWindow { Owner = this }.ShowDialog();

    // Sayfa kartında "Ürün Ekle" combobox'tan seçim yapıldığında.
    private void OnAssignProductToPage(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.Tag is not CustomPageEntry page) return;
        if (cb.SelectedItem is not Product product) return;
        if (DataContext is not MainViewModel vm) return;

        vm.AssignProductToPage(page, product);
        // ComboBox seçimini sıfırla — bir sonraki seçim için temiz kalsın.
        cb.SelectedIndex = -1;
    }

    // Sayfa kartı içindeki ürün chip'inde ✕ butonuna basıldığında.
    private void OnRemoveProductFromPage(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string productId) return;
        if (DataContext is not MainViewModel vm) return;

        // Chip'in ait olduğu sayfayı bul: parent ItemsControl'un üzerinde,
        // DataContext olarak CustomPageEntry duruyor.
        var pageEntry = FindParentDataContext<CustomPageEntry>(btn);
        if (pageEntry is null) return;

        vm.UnassignProductFromPage(pageEntry, productId);
    }

    private static T? FindParentDataContext<T>(DependencyObject child) where T : class
    {
        var current = child;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T match) return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
