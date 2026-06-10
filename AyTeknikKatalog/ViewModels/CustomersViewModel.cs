using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;

namespace AyTeknikKatalog.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly CustomerService _service = new();

    [ObservableProperty] private ObservableCollection<Customer> _items = new();
    [ObservableProperty] private Customer? _selectedItem;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusText = "";

    public ICollectionView ItemsView { get; }

    public CustomersViewModel()
    {
        foreach (var c in _service.Load()) Items.Add(c);
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = Filter;
        StatusText = Items.Count == 0 ? "Müşteri yok — Excel'den içe aktarın veya elle ekleyin." : $"{Items.Count} müşteri.";
    }

    partial void OnSearchTextChanged(string value) => ItemsView.Refresh();

    private bool Filter(object o)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        if (o is not Customer c) return true;
        var q = SearchText.Trim().ToLowerInvariant();
        return (c.CompanyName ?? "").ToLowerInvariant().Contains(q)
            || (c.ContactName ?? "").ToLowerInvariant().Contains(q)
            || (c.Email ?? "").ToLowerInvariant().Contains(q)
            || (c.Phone ?? "").ToLowerInvariant().Contains(q)
            || (c.Tags ?? "").ToLowerInvariant().Contains(q);
    }

    [RelayCommand]
    private void AddItem()
    {
        var c = new Customer { CompanyName = "Yeni müşteri", Salutation = Salutation.Bey };
        Items.Add(c);
        SelectedItem = c;
        StatusText = $"{Items.Count} müşteri.";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedItem is null) return;
        Items.Remove(SelectedItem);
        StatusText = $"{Items.Count} müşteri.";
    }

    [RelayCommand]
    private void Save()
    {
        _service.Save(Items);
        StatusText = $"Kaydedildi · {Items.Count} müşteri.";
    }

    [RelayCommand]
    private void ImportExcel()
    {
        var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Müşteri listesi Excel'i seç" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var imported = ExcelDataService.ImportCustomers(dlg.FileName);
            var working = Items.ToList();
            int before = working.Count;
            foreach (var imp in imported) CustomerService.UpsertByEmail(working, imp);
            // Items aynı koleksiyon kalmalı (ItemsView ona bağlı) → yerinde güncelle
            Items.Clear();
            foreach (var c in working) Items.Add(c);
            ItemsView.Refresh();
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
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "algow-musteriler.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExcelDataService.ExportCustomers(dlg.FileName, Items);
            StatusText = "Excel'e aktarıldı.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Dışa aktarım hatası: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
