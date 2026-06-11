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
}
