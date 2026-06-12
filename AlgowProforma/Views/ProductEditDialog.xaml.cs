using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlgowProforma.Models;

namespace AlgowProforma.Views;

public partial class ProductEditDialog : Window
{
    private static readonly Regex PriceAllowedRegex = new(@"^[0-9.,]+$", RegexOptions.Compiled);

    public Product Product { get; }

    public ProductEditDialog(Product seed)
    {
        InitializeComponent();
        Product = seed.Clone();
        if (Product.HasTable && Product.Table is null)
            Product.Table = ProductTable.CreateDefault();
        DataContext = Product;
        TitleText.Text = string.IsNullOrWhiteSpace(seed.Name) ? "Yeni Ürün" : "Ürünü Düzenle";
        Product.PropertyChanged += OnProductChanged;
        // Klon kaydedilince kataloğa GİRİYOR — handler sökülmezse kapanmış dialog (bitmap'leriyle)
        // ürün yaşadıkça bellekte pinli kalır.
        Closed += (_, _) => Product.PropertyChanged -= OnProductChanged;
        UpdateSaveState();
    }

    private void OnProductChanged(object? sender, PropertyChangedEventArgs e) => UpdateSaveState();

    private void UpdateSaveState() => SaveBtn.IsEnabled = !string.IsNullOrWhiteSpace(Product.Name);

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Product.Name)) return;
        if (!Product.HasTable) Product.Table = null;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnBrowseImage(object sender, RoutedEventArgs e)
    {
        var path = ImagePicker.PickAndCrop(this, "Ürün görseli seç");
        if (path is not null) Product.ImagePath = path;
    }

    private void OnPriceTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !PriceAllowedRegex.IsMatch(e.Text);
    }

    private void OnTableModeChecked(object sender, RoutedEventArgs e)
    {
        if (Product.Table is null)
            Product.Table = ProductTable.CreateDefault();
    }

    private ProductTable? Table => Product.Table;

    private void OnAddSpec(object sender, RoutedEventArgs e)
    {
        Table?.Specs.Add(new ProductSpec());
    }

    private void OnRemoveSpec(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is ProductSpec spec)
            Table?.Specs.Remove(spec);
    }

    private void OnAddColumn(object sender, RoutedEventArgs e)
    {
        if (Table is null) return;
        Table.Columns.Add(new ProductTableColumn { Header = "Yeni" });
        foreach (var row in Table.Rows)
            row.Cells.Add(new ProductTableCell());
    }

    private void OnRemoveColumn(object sender, RoutedEventArgs e)
    {
        if (Table is null) return;
        if (sender is not Button b || b.DataContext is not ProductTableColumn col) return;
        var idx = Table.Columns.IndexOf(col);
        if (idx < 0) return;
        Table.Columns.RemoveAt(idx);
        foreach (var row in Table.Rows)
        {
            if (idx < row.Cells.Count)
                row.Cells.RemoveAt(idx);
        }
    }

    private void OnAddRow(object sender, RoutedEventArgs e)
    {
        if (Table is null) return;
        var row = new ProductTableRow();
        for (int i = 0; i < Table.Columns.Count; i++)
            row.Cells.Add(new ProductTableCell());
        Table.Rows.Add(row);
    }

    private void OnRemoveRow(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is ProductTableRow row)
            Table?.Rows.Remove(row);
    }
}
