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

    /// <summary>Kayda yansımamış değişiklik var mı — pencere kapanış uyarısı buna bakar.
    /// Toplu zam (ApplyBulk) dahil her ekleme/silme/hücre düzenlemesi işaretler.</summary>
    public bool HasUnsavedChanges { get; private set; }

    public PriceBookViewModel()
    {
        foreach (var it in _service.Load()) Items.Add(it);
        HookItems(Items);
        HasUnsavedChanges = false;   // yükleme sırasındaki Add'ler değişiklik sayılmaz
        StatusText = Items.Count == 0 ? "Havuz boş — Excel'den içe aktarın veya elle ekleyin." : $"{Items.Count} ürün.";
    }

    // ImportExcel koleksiyonu YENİDEN ATAR — hook'ları yeni koleksiyona taşı.
    partial void OnItemsChanged(ObservableCollection<PriceItem> value) => HookItems(value);

    private void HookItems(ObservableCollection<PriceItem> items)
    {
        items.CollectionChanged += (_, e) =>
        {
            // Önce -= sonra += : Clear+yeniden-Add akışında (import) aynı nesne ikinci kez
            // hook'lanıp handler sızdırıyordu (Reset olayı OldItems taşımaz, eski abonelik kalır).
            if (e.NewItems is not null)
                foreach (PriceItem it in e.NewItems) { it.PropertyChanged -= OnItemEdited; it.PropertyChanged += OnItemEdited; }
            if (e.OldItems is not null) foreach (PriceItem it in e.OldItems) it.PropertyChanged -= OnItemEdited;
            HasUnsavedChanges = true;
        };
        foreach (var it in items) { it.PropertyChanged -= OnItemEdited; it.PropertyChanged += OnItemEdited; }
    }

    private void OnItemEdited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => HasUnsavedChanges = true;

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
        HasUnsavedChanges = false;
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

    /// <summary>Yalnız seçili satırlara zam/indirim — grid çoklu seçim (Ctrl/Shift) destekler.
    /// Parametre DataGrid.SelectedItems (IList) olarak XAML'den gelir.</summary>
    [RelayCommand]
    private void ApplyBulkSelected(System.Collections.IList? selected)
    {
        if (BulkPercent == 0m) { StatusText = "Yüzde gir (örn 10 veya -5)."; return; }
        var targets = selected?.OfType<PriceItem>().ToList() ?? new();
        if (targets.Count == 0) { StatusText = "Önce listeden satır seçin (Ctrl/Shift ile çoklu)."; return; }
        PriceBookService.ApplyPercentChange(targets, BulkPercent);
        StatusText = $"{targets.Count} seçili kaleme %{BulkPercent} uygulandı — kaydetmeyi unutmayın.";
    }
}
