using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

public partial class ProductSpec : ObservableObject
{
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _value = "";

    public ProductSpec Clone() => new() { Label = Label, Value = Value };
}

public partial class ProductTableColumn : ObservableObject
{
    [ObservableProperty] private string _header = "";

    public ProductTableColumn Clone() => new() { Header = Header };
}

public partial class ProductTableCell : ObservableObject
{
    [ObservableProperty] private string _value = "";

    public ProductTableCell Clone() => new() { Value = Value };
}

public partial class ProductTableRow : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ProductTableCell> _cells = new();

    public ProductTableRow Clone()
    {
        var clone = new ProductTableRow();
        foreach (var c in Cells) clone.Cells.Add(c.Clone());
        return clone;
    }
}

public partial class ProductTable : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private ObservableCollection<ProductSpec> _specs = new();
    [ObservableProperty] private ObservableCollection<ProductTableColumn> _columns = new();
    [ObservableProperty] private ObservableCollection<ProductTableRow> _rows = new();

    public static ProductTable CreateDefault()
    {
        var table = new ProductTable();
        table.Specs.Add(new ProductSpec { Label = "Bağlantı Şekli", Value = "" });
        table.Specs.Add(new ProductSpec { Label = "Basınç", Value = "" });
        table.Specs.Add(new ProductSpec { Label = "Sıcaklık", Value = "" });
        table.Specs.Add(new ProductSpec { Label = "Kullanım", Value = "" });

        string[] headers = { "Kod", "DN", "Ölçü", "Kutu Adet", "Koli Adet", "FİYAT", "L mm", "H mm", "Gr." };
        foreach (var h in headers)
            table.Columns.Add(new ProductTableColumn { Header = h });

        for (int r = 0; r < 3; r++)
        {
            var row = new ProductTableRow();
            for (int c = 0; c < headers.Length; c++)
                row.Cells.Add(new ProductTableCell());
            table.Rows.Add(row);
        }
        return table;
    }

    public ProductTable Clone()
    {
        var clone = new ProductTable { Title = Title };
        foreach (var s in Specs) clone.Specs.Add(s.Clone());
        foreach (var c in Columns) clone.Columns.Add(c.Clone());
        foreach (var r in Rows) clone.Rows.Add(r.Clone());
        return clone;
    }
}
