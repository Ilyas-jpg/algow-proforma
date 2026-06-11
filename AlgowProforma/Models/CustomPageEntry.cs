using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.Models;

public partial class CustomPageEntry : ObservableObject
{
    [ObservableProperty] private string _layoutId = "";

    /// <summary>
    /// Bu sayfaya atanan ürünlerin Id listesi (Product.Id).
    /// Sıralama da bu listede tutulur. Boşsa kullanıcı henüz ürün atamamış demektir.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _productIds = new();

    public CustomPageEntry() { }
    public CustomPageEntry(string id) { LayoutId = id; }
}
