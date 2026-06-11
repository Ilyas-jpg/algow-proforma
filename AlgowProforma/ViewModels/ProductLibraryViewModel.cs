using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

/// <summary>
/// Görsel ürün kütüphanesi penceresi VM'i (Faz 2). Kayıtlı ürünleri listeler; "Kataloğa Ekle"
/// ile (callback üzerinden) mevcut kataloğa bağımsız kopya ekler, "Sil" ile kütüphaneden çıkarır.
/// </summary>
public partial class ProductLibraryViewModel : ObservableObject
{
    private readonly ProductLibraryService _service = new();
    private readonly Action<Product>? _onAddToCatalog;

    [ObservableProperty] private ObservableCollection<Product> _items = new();
    [ObservableProperty] private string _statusText = "";

    public ProductLibraryViewModel(Action<Product>? onAddToCatalog = null)
    {
        _onAddToCatalog = onAddToCatalog;
        foreach (var p in _service.Load()) Items.Add(p);
        UpdateStatus();
    }

    private void UpdateStatus() =>
        StatusText = Items.Count == 0
            ? "Kütüphane boş — katalogdaki ürünleri 'Kütüphaneye Kaydet' ile ekleyin."
            : $"{Items.Count} ürün kütüphanede.";

    [RelayCommand]
    private void AddToCatalog(Product? p)
    {
        if (p is null || _onAddToCatalog is null) return;
        _onAddToCatalog(ProductLibraryService.ToCatalogProduct(p));  // yeni Id'li bağımsız kopya
        StatusText = $"Kataloğa eklendi: {p.Name}";
    }

    [RelayCommand]
    private void DeleteItem(Product? p)
    {
        if (p is null) return;
        if (MessageBox.Show($"\"{p.Name}\" ürünü kütüphaneden silinsin mi?\n\n(Görsel başka katalog/kütüphane kullanmıyorsa temizlikte .trash'e taşınır — geri alınabilir.)",
                "Kütüphaneden Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Items.Remove(p);
        _service.Save(Items);
        UpdateStatus();
    }

    /// <summary>Excel'i doğrudan kütüphaneye al (Faz 2 follow-up) — katalogdan geçirmeden, aynı tekilleştirme.</summary>
    [RelayCommand]
    private void ImportFromExcel()
    {
        var dialog = new Views.ExcelImportDialog
        {
            // Modal kütüphane penceresinin üstünde kalsın — aktif pencere owner olur
            Owner = System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.OfType<Window>(Application.Current.Windows), w => w.IsActive),
        };
        if (dialog.ShowDialog() != true || dialog.ImportedProducts.Count == 0) { return; }

        var list = new System.Collections.Generic.List<Product>(Items);
        int added = ProductLibraryService.UpsertAll(list, dialog.ImportedProducts);
        _service.Save(list);

        Items.Clear();
        foreach (var p in list) Items.Add(p);
        StatusText = $"Excel'den kütüphaneye alındı: +{added} yeni, {dialog.ImportedProducts.Count - added} güncellendi ({Items.Count} toplam).";
    }

    /// <summary>Kütüphaneyi Excel'e aktar (kod/ad/fiyat/para birimi — görsel path'i taşınmaz).</summary>
    [RelayCommand]
    private void ExportToExcel()
    {
        if (Items.Count == 0) { StatusText = "Aktarılacak ürün yok."; return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"urun-kutuphanesi-{DateTime.Today:yyyyMMdd}.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExcelDataService.ExportProducts(dlg.FileName, Items);
            StatusText = $"{Items.Count} ürün Excel'e aktarıldı: {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Excel'e aktarılamadı: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
