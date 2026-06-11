using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

public partial class PriceBookViewModel : ObservableObject
{
    private readonly PriceBookService _service = new();

    [ObservableProperty] private ObservableCollection<PriceItem> _items = new();
    [ObservableProperty] private PriceItem? _selectedItem;
    [ObservableProperty] private decimal _bulkPercent;
    [ObservableProperty] private string _statusText = "";

    public PriceBookViewModel()
    {
        foreach (var it in _service.Load()) Items.Add(it);
        StatusText = Items.Count == 0 ? "Havuz boş — Excel'den içe aktarın veya elle ekleyin." : $"{Items.Count} ürün.";
    }

    [RelayCommand]
    private void AddItem()
    {
        var it = new PriceItem { Name = "Yeni ürün", Unit = "adet", Currency = "TL", VatRate = 20m };
        Items.Add(it);
        SelectedItem = it;
        StatusText = $"{Items.Count} ürün.";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedItem is null) return;
        Items.Remove(SelectedItem);
        StatusText = $"{Items.Count} ürün.";
    }

    [RelayCommand]
    private void Save()
    {
        _service.Save(Items);
        StatusText = $"Kaydedildi · {Items.Count} ürün.";
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Fiyat listesi Excel'i seç" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var imported = ExcelDataService.ImportPriceItems(dlg.FileName);
            var list = Items.ToList();
            int before = list.Count;
            foreach (var imp in imported) PriceBookService.UpsertByCode(list, imp);
            Items = new ObservableCollection<PriceItem>(list);
            _service.Save(Items);
            StatusText = $"{imported.Count} satır okundu · toplam {Items.Count} (+{Items.Count - before} yeni).";
        }
        catch (Exception ex)
        {
            MessageBox.Show("İçe aktarım hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ExportExcel()
    {
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "algow-fiyat-listesi.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExcelDataService.ExportPriceItems(dlg.FileName, Items);
            StatusText = "Excel'e aktarıldı.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Dışa aktarım hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ApplyBulk()
    {
        if (BulkPercent == 0m) { StatusText = "Yüzde gir (örn 10 veya -5)."; return; }
        PriceBookService.ApplyPercentChange(Items, BulkPercent);
        StatusText = $"Tüm listeye %{BulkPercent} uygulandı — kaydetmeyi unutmayın.";
    }
}
