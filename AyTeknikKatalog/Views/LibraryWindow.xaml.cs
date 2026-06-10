using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;

namespace AyTeknikKatalog.Views;

public partial class LibraryWindow : Window
{
    private readonly LibraryService _libraryService;

    public ObservableCollection<CatalogEntry> Entries { get; } = new();
    public Catalog? LoadedCatalog { get; private set; }
    public string? LoadedEntryName { get; private set; }

    public LibraryWindow(LibraryService libraryService)
    {
        InitializeComponent();
        _libraryService = libraryService;
        DataContext = this;
        Refresh();
    }

    private void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _libraryService.ListEntries())
            Entries.Add(entry);
    }

    private void OnPreview(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CatalogEntry entry }) return;
        try
        {
            Process.Start(new ProcessStartInfo(entry.PdfPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PDF açılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnLoad(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CatalogEntry entry }) return;
        try
        {
            LoadedCatalog = _libraryService.Load(entry);
            LoadedEntryName = entry.DisplayTitle;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Katalog yüklenemedi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CatalogEntry entry }) return;
        var current = string.IsNullOrWhiteSpace(entry.LibraryLabel) ? entry.BrandName : entry.LibraryLabel;
        var dialog = new NameInputDialog("Yeniden Adlandır", current,
            "Bu isim sadece kütüphanede görünür, PDF içeriği değişmez.")
        { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _libraryService.Rename(entry, dialog.EnteredName);
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Yeniden adlandırılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CatalogEntry entry }) return;
        if (MessageBox.Show($"\"{entry.DisplayTitle}\" ({entry.CreatedAt:dd MMM yyyy HH:mm}) kütüphaneden silinsin mi?\nBu işlem geri alınamaz.",
                "Kütüphaneden Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _libraryService.Delete(entry);
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Silinemedi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnShareEmail(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShareViaEmail(entry.PdfPath, entry.BrandName), "E-posta uygulaması açıldı, PDF panoya kopyalandı.");

    private void OnShareWhatsApp(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShareViaWhatsApp(entry.PdfPath, entry.BrandName), "WhatsApp açıldı, PDF panoya kopyalandı.");

    private void OnShareInstagram(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShareViaInstagram(entry.PdfPath), "Instagram açıldı, PDF panoya kopyalandı.");

    private void OnShareTelegram(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShareViaTelegram(entry.PdfPath, entry.BrandName), "Telegram açıldı, PDF panoya kopyalandı.");

    private void OnShareLinkedIn(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShareViaLinkedIn(entry.BrandName), "LinkedIn paylaşım sayfası açıldı.");

    private void OnCopyFile(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.CopyFileToClipboard(entry.PdfPath), "PDF panoya kopyalandı.");

    private void OnShowInExplorer(object sender, RoutedEventArgs e) =>
        DoShare(sender, entry => ShareService.ShowInExplorer(entry.PdfPath), null);

    private void DoShare(object sender, Action<CatalogEntry> action, string? toast)
    {
        if (sender is not Button { Tag: CatalogEntry entry }) return;
        try
        {
            action(entry);
            ClosePopupOf(sender);
            if (!string.IsNullOrEmpty(toast))
                MessageBox.Show(toast, "Paylaşım", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Paylaşılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ClosePopupOf(object sender)
    {
        var fe = sender as System.Windows.FrameworkElement;
        while (fe is not null)
        {
            if (fe is System.Windows.Controls.Primitives.Popup popup) { popup.IsOpen = false; return; }
            fe = (fe.Parent ?? fe.TemplatedParent) as System.Windows.FrameworkElement;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
