using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

public partial class Product : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private string _currency = "TL";
    [ObservableProperty] private string _imagePath = "";
    [ObservableProperty] private bool _isFeatured;
    [ObservableProperty] private bool _hasTable;
    [ObservableProperty] private ProductTable? _table;

    public Product Clone() => new()
    {
        Id = Id,
        Name = Name,
        Code = Code,
        Price = Price,
        Currency = Currency,
        ImagePath = ImagePath,
        IsFeatured = IsFeatured,
        HasTable = HasTable,
        Table = Table?.Clone(),
    };
}
