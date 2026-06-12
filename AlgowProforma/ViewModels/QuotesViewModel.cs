using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.ViewModels;

/// <summary>
/// Teklifler panosu: kayıtlı tüm teklifler + yaşam döngüsü durumu. Durum combo'dan değiştirilince
/// ANINDA diske yazılır (ayrı kaydet adımı yok — toplu zam penceresindeki "sessiz kayıp" dersinden).
/// Başarılı e-posta gönderimi Taslak→Gönderildi geçişini zaten otomatik yapar.
/// </summary>
public partial class QuotesViewModel : ObservableObject
{
    private readonly QuoteService _quotes = new();
    private readonly List<Quote> _hooked = new();

    public ObservableCollection<Quote> Items { get; } = new();
    [ObservableProperty] private string _summaryText = "";

    /// <summary>Durum combo kaynağı (Türkçe etiketli).</summary>
    public IReadOnlyList<QuoteStatusOption> StatusOptions => QuoteStatusOption.All;

    public QuotesViewModel() => Reload();

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

    private void OnQuoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Quote.Status) || sender is not Quote q) return;
        try { _quotes.Save(q); } catch { /* diske yazılamadıysa bir sonraki değişiklikte tekrar denenir */ }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        int Count(QuoteStatus s) => Items.Count(i => i.Status == s);
        SummaryText = Items.Count == 0
            ? "Kayıtlı teklif yok — Teklif penceresinden oluşturup kaydedin."
            : $"{Items.Count} teklif  ·  {Count(QuoteStatus.Taslak)} taslak  ·  {Count(QuoteStatus.Gonderildi)} gönderildi" +
              $"  ·  {Count(QuoteStatus.Onaylandi)} onaylandı  ·  {Count(QuoteStatus.Reddedildi)} reddedildi";
    }
}
