using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

/// <summary>Pano süzgeci için durum seçeneği — null değer = "Tümü".</summary>
public sealed record QuoteStatusFilterOption(QuoteStatus? Value, string Label)
{
    public static readonly IReadOnlyList<QuoteStatusFilterOption> All = new[]
    {
        new QuoteStatusFilterOption(null, "Tümü"),
        new QuoteStatusFilterOption(QuoteStatus.Taslak, "Taslak"),
        new QuoteStatusFilterOption(QuoteStatus.Gonderildi, "Gönderildi"),
        new QuoteStatusFilterOption(QuoteStatus.Onaylandi, "Onaylandı"),
        new QuoteStatusFilterOption(QuoteStatus.Reddedildi, "Reddedildi"),
    };
}

/// <summary>
/// Teklifler panosu: kayıtlı tüm teklifler + yaşam döngüsü durumu. Durum combo'dan değiştirilince
/// ANINDA diske yazılır (ayrı kaydet adımı yok — toplu zam penceresindeki "sessiz kayıp" dersinden).
/// Arama + durum süzgeci ICollectionView üzerinde; özet satırı onaylanan tutarı para birimi
/// kırılımıyla verir. Silme geri-dönülebilir: JSON + PDF Teklifler\.trash'e taşınır.
/// </summary>
public partial class QuotesViewModel : ObservableObject
{
    private readonly QuoteService _quotes = new();
    private readonly List<Quote> _hooked = new();
    private static readonly CultureInfo Tr = new("tr-TR");

    public ObservableCollection<Quote> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _approvedText = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private QuoteStatusFilterOption _statusFilter = QuoteStatusFilterOption.All[0];

    /// <summary>Satır içi durum combo kaynağı (Türkçe etiketli).</summary>
    public IReadOnlyList<QuoteStatusOption> StatusOptions => QuoteStatusOption.All;
    /// <summary>Üst süzgeç combo kaynağı ("Tümü" + 4 durum).</summary>
    public IReadOnlyList<QuoteStatusFilterOption> FilterOptions => QuoteStatusFilterOption.All;

    public QuotesViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = Filter;
        Reload();
    }

    public void Reload()
    {
        foreach (var q in _hooked) q.PropertyChanged -= OnQuoteChanged;
        _hooked.Clear();
        Items.Clear();
        foreach (var q in _quotes.LoadAll())
        {
            Items.Add(q);
            q.PropertyChanged += OnQuoteChanged;
            _hooked.Add(q);
        }
        UpdateSummary();
    }

    partial void OnSearchTextChanged(string value) => ItemsView.Refresh();
    partial void OnStatusFilterChanged(QuoteStatusFilterOption value) => ItemsView.Refresh();

    private bool Filter(object o)
    {
        if (o is not Quote q) return true;
        if (StatusFilter?.Value is { } s && q.Status != s) return false;
        var t = (SearchText ?? "").Trim();
        if (t.Length == 0) return true;
        return Contains(q.QuoteNo, t) || Contains(q.CustomerCompany, t)
            || Contains(q.CustomerContact, t) || Contains(q.CustomerEmail, t);
    }

    private static bool Contains(string? hay, string needle) =>
        (hay ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void OnQuoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Quote.Status) || sender is not Quote q) return;
        try { _quotes.Save(q); } catch { /* diske yazılamadıysa bir sonraki değişiklikte tekrar denenir */ }
        UpdateSummary();
        ItemsView.Refresh();   // aktif durum süzgecine artık girmiyor/giriyor olabilir
    }

    /// <summary>Teklifi geri-dönülebilir siler (JSON + PDF → Teklifler\.trash, 30 gün purge).</summary>
    [RelayCommand]
    private void DeleteQuote(Quote? quote)
    {
        if (quote is null) return;
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var confirm = owner is null
            ? MessageBox.Show($"\"{quote.DisplayTitle}\" silinsin mi?\n\nKalıcı silinmez — 30 gün .trash klasöründe geri alınabilir durur.",
                "Teklifi Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            : MessageBox.Show(owner, $"\"{quote.DisplayTitle}\" silinsin mi?\n\nKalıcı silinmez — 30 gün .trash klasöründe geri alınabilir durur.",
                "Teklifi Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _quotes.Delete(quote);
            quote.PropertyChanged -= OnQuoteChanged;
            _hooked.Remove(quote);
            Items.Remove(quote);
            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Silinemedi: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateSummary()
    {
        int Count(QuoteStatus s) => Items.Count(i => i.Status == s);
        SummaryText = Items.Count == 0
            ? "Kayıtlı teklif yok — Teklif penceresinden oluşturup kaydedin."
            : $"{Items.Count} teklif  ·  {Count(QuoteStatus.Taslak)} taslak  ·  {Count(QuoteStatus.Gonderildi)} gönderildi" +
              $"  ·  {Count(QuoteStatus.Onaylandi)} onaylandı  ·  {Count(QuoteStatus.Reddedildi)} reddedildi";

        // Onaylanan tutar — para birimleri TOPLANMAZ (40×'lik birim karışması dersi), kırılımla verilir.
        var approved = Items.Where(i => i.Status == QuoteStatus.Onaylandi)
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Currency) ? "TL" : i.Currency.Trim().ToUpperInvariant())
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Sum(i => i.Totals.GrandTotal).ToString("N2", Tr)} {g.Key}")
            .ToList();
        ApprovedText = approved.Count == 0 ? "" : "Onaylanan toplam (KDV dâhil):  " + string.Join("   ·   ", approved);
    }
}
