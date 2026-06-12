using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly CustomerService _service = new();

    [ObservableProperty] private ObservableCollection<Customer> _items = new();
    [ObservableProperty] private Customer? _selectedItem;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusText = "";

    public ICollectionView ItemsView { get; }

    /// <summary>Kayda yansımamış değişiklik var mı — pencere kapanış uyarısı buna bakar.</summary>
    public bool HasUnsavedChanges { get; private set; }

    public CustomersViewModel()
    {
        foreach (var c in _service.Load()) Items.Add(c);
        HookItems(Items);
        HasUnsavedChanges = false;   // yükleme sırasındaki Add'ler değişiklik sayılmaz
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = Filter;
        StatusText = Items.Count == 0 ? "Müşteri yok — Excel'den içe aktarın veya elle ekleyin." : $"{Items.Count} müşteri.";
    }

    private void HookItems(ObservableCollection<Customer> items)
    {
        items.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null) foreach (Customer c in e.NewItems) c.PropertyChanged += OnItemEdited;
            if (e.OldItems is not null) foreach (Customer c in e.OldItems) c.PropertyChanged -= OnItemEdited;
            HasUnsavedChanges = true;
        };
        foreach (var c in items) c.PropertyChanged += OnItemEdited;
    }

    private void OnItemEdited(object? sender, PropertyChangedEventArgs e) => HasUnsavedChanges = true;

    /// <summary>Kapanış uyarısının "Evet"i — kaydetmeyi dener, başarısızsa pencere açık kalır.</summary>
    public bool TrySaveForClose()
    {
        try
        {
            _service.Save(Items);
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kaydedilemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
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
        HasUnsavedChanges = false;
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
            HasUnsavedChanges = false;   // import anında diske yazıldı
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
