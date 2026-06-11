using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AlgowProforma.Converters;
using AlgowProforma.Models;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class PagePlannerWindow : Window, INotifyPropertyChanged
{
    private readonly MainViewModel _vm;
    private Point _dragStart;
    private bool _isMouseDown;
    private bool _dragInProgress;
    private Product? _dragCandidate;

    public Catalog Catalog => _vm.Catalog;

    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex == value) return;
            _currentPageIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageNumberLabel));
            OnPropertyChanged(nameof(PageNumberFooter));
            RefreshCurrentPage();
        }
    }

    public string PageNumberLabel => Catalog.CustomPages.Count == 0
        ? "Sayfa yok"
        : $"Sayfa {CurrentPageIndex + 1} / {Catalog.CustomPages.Count}";

    public string PageNumberFooter => Catalog.CustomPages.Count == 0
        ? ""
        : $"{CurrentPageIndex + 1} / {Catalog.CustomPages.Count}";

    public string BrandName => string.IsNullOrWhiteSpace(_vm.Catalog.Brand.Name) ? "Algow PDF" : _vm.Catalog.Brand.Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public PagePlannerWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = this;
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        LayoutBox.ItemsSource = PageLayout.All;

        if (Catalog.CustomPages.Count == 0)
            Catalog.CustomPages.Add(new CustomPageEntry(PageLayout.Default.Id));

        Loaded += (_, _) => RefreshCurrentPage();
    }

    private CustomPageEntry? CurrentPage =>
        Catalog.CustomPages.Count == 0 ? null
        : Catalog.CustomPages[Math.Clamp(CurrentPageIndex, 0, Catalog.CustomPages.Count - 1)];

    // ============================================================
    //   Slot grid inşası — Grid + RowSpan/ColumnSpan ile
    // ============================================================

    private void RefreshCurrentPage()
    {
        SlotGrid.Children.Clear();
        SlotGrid.RowDefinitions.Clear();
        SlotGrid.ColumnDefinitions.Clear();

        if (CurrentPage is null)
        {
            UpdatePaletteBadges();
            UpdateStatus();
            return;
        }

        var page = CurrentPage!;
        var layout = PageLayout.GetById(page.LayoutId);

        for (int r = 0; r < layout.Rows; r++)
            SlotGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (int c = 0; c < layout.Columns; c++)
            SlotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        EnsureProductIdsLength(page, layout.Total);

        var byId = Catalog.Products.ToDictionary(p => p.Id, p => p);
        var covered = new HashSet<int>();

        for (int i = 0; i < layout.Total; i++)
        {
            if (covered.Contains(i)) continue;

            int row = i / layout.Columns;
            int col = i % layout.Columns;
            string pid = page.ProductIds[i];
            Product? product = !string.IsNullOrEmpty(pid) && byId.TryGetValue(pid, out var p) ? p : null;

            int rowSpan = 1, colSpan = 1;
            if (product is not null)
            {
                if (product.HasTable && product.Table is not null)
                {
                    rowSpan = Math.Min(2, layout.Rows);
                    colSpan = layout.Columns;
                }
                else if (product.IsFeatured && layout.Columns >= 2 && col + 2 <= layout.Columns)
                {
                    colSpan = 2;
                }
            }

            for (int dr = 0; dr < rowSpan; dr++)
                for (int dc = 0; dc < colSpan; dc++)
                    covered.Add((row + dr) * layout.Columns + (col + dc));

            var border = CreateSlotBorder(i, product);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            Grid.SetRowSpan(border, rowSpan);
            Grid.SetColumnSpan(border, colSpan);
            SlotGrid.Children.Add(border);
        }

        // ComboBox seçimi (event'i etkilemeden)
        LayoutBox.SelectionChanged -= OnLayoutChanged;
        LayoutBox.SelectedValue = page.LayoutId;
        LayoutBox.SelectionChanged += OnLayoutChanged;

        UpdatePaletteBadges();
        UpdateStatus();
    }

    private Border CreateSlotBorder(int slotIndex, Product? product)
    {
        var border = new Border
        {
            Margin = new Thickness(4),
            AllowDrop = true,
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC8)),
            BorderThickness = new Thickness(1),
            Tag = slotIndex,
            ToolTip = "Ürün sürükle / sağ tık = temizle",
        };
        border.Drop += OnSlotDrop;
        border.DragOver += OnSlotDragOver;
        border.MouseRightButtonUp += OnSlotClear;

        if (product is null)
        {
            border.Child = BuildEmptySlot();
        }
        else if (product.HasTable && product.Table is not null)
        {
            border.Background = System.Windows.Media.Brushes.White;
            border.Child = BuildTabloSlot(product);
        }
        else if (product.IsFeatured)
        {
            border.Background = System.Windows.Media.Brushes.White;
            border.Child = BuildFeaturedSlot(product);
        }
        else
        {
            border.Background = System.Windows.Media.Brushes.White;
            border.Child = BuildStandardSlot(product);
        }

        return border;
    }

    private static UIElement BuildEmptySlot()
    {
        var empty = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        empty.Children.Add(new TextBlock
        {
            Text = "+",
            FontSize = 32,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        empty.Children.Add(new TextBlock
        {
            Text = "Ürün sürükle",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });
        return empty;
    }

    // Tema renklerine erişim
    private static SolidColorBrush PrimaryBrush =>
        (Application.Current?.TryFindResource("PrimaryBrush") as SolidColorBrush)
        ?? new SolidColorBrush(Color.FromRgb(0xE8, 0xB8, 0x00));

    private static SolidColorBrush MutedTextBrush => new(Color.FromRgb(0x88, 0x8A, 0x95));
    private static SolidColorBrush DarkTextBrush => new(Color.FromRgb(0x1A, 0x1A, 0x1F));
    private static SolidColorBrush ImageBgBrush => new(Color.FromRgb(0xF2, 0xF2, 0xF5));

    private static UIElement BuildStandardSlot(Product product)
    {
        // PDF Standard card stilinde: görsel + ad + kod + fiyat + sarı/primary çizgi
        var content = new Grid { IsHitTestVisible = false };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Görsel alanı
        var imgBorder = new Border { Background = ImageBgBrush, Margin = new Thickness(2) };
        var img = new Image { Margin = new Thickness(6), Stretch = Stretch.Uniform };
        var bmp = LoadBitmap(product.ImagePath);
        if (bmp != null) img.Source = bmp;
        else imgBorder.Child = new TextBlock
        {
            Text = "Görsel yok", FontSize = 9, Foreground = MutedTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        if (bmp != null) imgBorder.Child = img;
        Grid.SetRow(imgBorder, 0);
        content.Children.Add(imgBorder);

        // Ad/kod/fiyat
        var info = new StackPanel { Margin = new Thickness(4, 4, 4, 4) };
        info.Children.Add(new TextBlock
        {
            Text = product.Name, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = DarkTextBrush, TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(product.Code))
            info.Children.Add(new TextBlock
            {
                Text = product.Code, FontSize = 8, Foreground = MutedTextBrush,
                Margin = new Thickness(0, 1, 0, 0),
            });
        info.Children.Add(new TextBlock
        {
            Text = FormatPrice(product), FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = DarkTextBrush, Margin = new Thickness(0, 3, 0, 0),
        });
        // Primary renk çizgi
        info.Children.Add(new Rectangle { Height = 1.2, Fill = PrimaryBrush, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetRow(info, 1);
        content.Children.Add(info);

        return content;
    }

    private static UIElement BuildFeaturedSlot(Product product)
    {
        // PDF Featured card stilinde: image | primary dolu panel (ÖNE ÇIKAN + ad + fiyat butonu)
        var grid = new Grid { IsHitTestVisible = false };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Sol: image
        var imgBorder = new Border { Background = ImageBgBrush };
        var bmp = LoadBitmap(product.ImagePath);
        if (bmp != null)
            imgBorder.Child = new Image { Source = bmp, Margin = new Thickness(6), Stretch = Stretch.Uniform };
        else
            imgBorder.Child = new TextBlock
            {
                Text = "Görsel yok", FontSize = 9, Foreground = MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
        Grid.SetColumn(imgBorder, 0);
        grid.Children.Add(imgBorder);

        // Sağ: primary dolu panel
        var panel = new Border { Background = PrimaryBrush, Padding = new Thickness(8) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "ÖNE ÇIKAN", FontSize = 7, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White, Opacity = 0.75,
        });
        stack.Children.Add(new TextBlock
        {
            Text = product.Name, FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 0, 0),
        });
        if (!string.IsNullOrWhiteSpace(product.Code))
            stack.Children.Add(new TextBlock
            {
                Text = product.Code, FontSize = 8, Foreground = System.Windows.Media.Brushes.White,
                Opacity = 0.7, Margin = new Thickness(0, 2, 0, 0),
            });
        // Fiyat butonu
        var priceBtn = new Border
        {
            Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
        };
        priceBtn.Child = new TextBlock
        {
            Text = FormatPrice(product), FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = PrimaryBrush, HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(priceBtn);
        panel.Child = stack;
        Grid.SetColumn(panel, 1);
        grid.Children.Add(panel);

        return grid;
    }

    private static UIElement BuildTabloSlot(Product product)
    {
        // PDF Tablo card stilinde: title bar (primary) + code strip + image+specs row + data table
        var root = new Grid { IsHitTestVisible = false };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Title bar
        var titleBar = new Border { Background = PrimaryBrush, Padding = new Thickness(8, 4, 8, 4) };
        titleBar.Child = new TextBlock
        {
            Text = (product.Table?.Title ?? product.Name).ToUpperInvariant(),
            FontSize = 9, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        // Code strip
        if (!string.IsNullOrWhiteSpace(product.Code))
        {
            var codeBar = new Border { Background = ImageBgBrush, Padding = new Thickness(8, 3, 8, 3) };
            codeBar.Child = new TextBlock
            {
                Text = product.Code, FontSize = 8, FontWeight = FontWeights.SemiBold,
                Foreground = PrimaryBrush,
            };
            Grid.SetRow(codeBar, 1);
            root.Children.Add(codeBar);
        }

        // Image + specs row
        var midRow = new Grid { Margin = new Thickness(6, 6, 6, 6) };
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var imgBorder = new Border { Background = ImageBgBrush, Margin = new Thickness(0, 0, 4, 0) };
        var bmp = LoadBitmap(product.ImagePath);
        if (bmp != null) imgBorder.Child = new Image { Source = bmp, Margin = new Thickness(4), Stretch = Stretch.Uniform };
        else imgBorder.Child = new TextBlock { Text = "Görsel yok", FontSize = 8, Foreground = MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imgBorder, 0);
        midRow.Children.Add(imgBorder);

        // Spec çizgileri (placeholder olarak ince çizgiler)
        var specsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var specs = product.Table?.Specs ?? new System.Collections.ObjectModel.ObservableCollection<ProductSpec>();
        for (int i = 0; i < Math.Min(4, specs.Count); i++)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            line.Children.Add(new TextBlock
            {
                Text = specs[i].Label, FontSize = 7, Foreground = MutedTextBrush, Width = 50,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            line.Children.Add(new TextBlock
            {
                Text = specs[i].Value, FontSize = 7, Foreground = DarkTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            specsStack.Children.Add(line);
        }
        Grid.SetColumn(specsStack, 1);
        midRow.Children.Add(specsStack);
        Grid.SetRow(midRow, 2);
        root.Children.Add(midRow);

        // Mini data table (header + 2-3 row outline)
        var colsCount = Math.Min(6, product.Table?.Columns.Count ?? 0);
        if (colsCount > 0)
        {
            var tableGrid = new Grid { Margin = new Thickness(6, 0, 6, 6) };
            for (int i = 0; i < colsCount; i++)
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 0; r < Math.Min(3, product.Table?.Rows.Count ?? 0); r++)
                tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            for (int c = 0; c < colsCount; c++)
            {
                var hdr = new Border { Background = PrimaryBrush, Padding = new Thickness(2) };
                hdr.Child = new TextBlock
                {
                    Text = product.Table?.Columns[c].Header ?? "",
                    FontSize = 6, FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(hdr, c);
                Grid.SetRow(hdr, 0);
                tableGrid.Children.Add(hdr);
            }
            // Veri satırları (placeholder)
            for (int r = 0; r < Math.Min(3, product.Table?.Rows.Count ?? 0); r++)
            {
                for (int c = 0; c < colsCount; c++)
                {
                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE7)),
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(2),
                    };
                    cell.Child = new TextBlock { Text = "·", FontSize = 6, Foreground = MutedTextBrush, HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(cell, c);
                    Grid.SetRow(cell, r + 1);
                    tableGrid.Children.Add(cell);
                }
            }
            Grid.SetRow(tableGrid, 3);
            root.Children.Add(tableGrid);
        }

        return root;
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string FormatPrice(Product product)
    {
        if (product.Price <= 0) return "Fiyat sorunuz";
        return $"{product.Price.ToString("N2", new System.Globalization.CultureInfo("tr-TR"))} {product.Currency}";
    }

    private static Border MakeBadge(string text, bool isYellow)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (isYellow)
        {
            var grad = new LinearGradientBrush();
            grad.StartPoint = new Point(0, 0); grad.EndPoint = new Point(1, 1);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xF8, 0xDC, 0x1A), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xE8, 0xB8, 0x00), 1));
            b.Background = grad;
        }
        else
        {
            var grad = new LinearGradientBrush();
            grad.StartPoint = new Point(0, 0); grad.EndPoint = new Point(0, 1);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xF5, 0xF6, 0xF7), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xD6, 0xD8, 0xDC), 0.55));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xA9, 0xAD, 0xB3), 1));
            b.Background = grad;
        }
        b.Child = new TextBlock
        {
            Text = text,
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1F)),
        };
        return b;
    }

    private static void EnsureProductIdsLength(CustomPageEntry page, int targetLength)
    {
        while (page.ProductIds.Count < targetLength) page.ProductIds.Add(string.Empty);
        while (page.ProductIds.Count > targetLength) page.ProductIds.RemoveAt(page.ProductIds.Count - 1);
    }

    // ============================================================
    //   Drag & Drop
    // ============================================================

    private void OnProductMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Product p)
        {
            _dragStart = e.GetPosition(this);
            _isMouseDown = true;
            _dragCandidate = p;
            StatusText.Text = $"Sürüklemeye hazır → {p.Name}";
        }
    }

    private void OnProductMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = false;
        _dragCandidate = null;
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragInProgress) return;
        if (!_isMouseDown || _dragCandidate is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isMouseDown = false;
            _dragCandidate = null;
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var product = _dragCandidate;
        _isMouseDown = false;
        _dragCandidate = null;
        _dragInProgress = true;
        try
        {
            StatusText.Text = $"Drag → {product.Name}";
            var data = new DataObject(DataFormats.StringFormat, product.Id);
            DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
        }
        finally { _dragInProgress = false; }
    }

    private void OnProductMouseMove(object sender, MouseEventArgs e) { /* Window seviyesinde yapılıyor */ }

    private void OnSlotDragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.StringFormat);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSlotDrop(object sender, DragEventArgs e)
    {
        if (CurrentPage is null) return;
        if (sender is not Border b || b.Tag is not int slotIndex) return;
        if (e.Data.GetData(DataFormats.StringFormat) is not string productId) return;

        var product = Catalog.Products.FirstOrDefault(p => p.Id == productId);
        if (product is null) return;

        var layout = PageLayout.GetById(CurrentPage.LayoutId);
        PlaceProductInPage(CurrentPage, layout, slotIndex, product);
        _vm.NotifyManualPagesChanged();
        RefreshCurrentPage();
    }

    private void OnSlotClear(object sender, MouseButtonEventArgs e)
    {
        if (CurrentPage is null) return;
        if (sender is not Border b || b.Tag is not int slotIndex) return;
        if (slotIndex < 0 || slotIndex >= CurrentPage.ProductIds.Count) return;
        CurrentPage.ProductIds[slotIndex] = string.Empty;
        _vm.NotifyManualPagesChanged();
        RefreshCurrentPage();
        e.Handled = true;
    }

    /// <summary>Ürünü doğru pozisyona yerleştir (span'a göre snap).</summary>
    private static void PlaceProductInPage(CustomPageEntry page, PageLayout layout, int dropIndex, Product product)
    {
        int cols = layout.Columns;
        int rows = layout.Rows;
        int dropRow = dropIndex / cols;
        int dropCol = dropIndex % cols;

        int rowSpan = 1, colSpan = 1;
        if (product.HasTable && product.Table is not null)
        {
            // Tablo ürünü daima üstte yerleşir, min(2, rows) satır × tüm cols
            rowSpan = Math.Min(2, rows);
            colSpan = cols;
            dropRow = 0;
            dropCol = 0;
        }
        else if (product.IsFeatured && cols >= 2)
        {
            colSpan = 2;
            // Satıra sığmıyorsa sola kaydır
            if (dropCol + colSpan > cols) dropCol = cols - colSpan;
        }

        EnsureProductIdsLength(page, layout.Total);

        // Önce: aynı ürün başka bir slotta varsa eski yerini temizle
        for (int i = 0; i < page.ProductIds.Count; i++)
            if (page.ProductIds[i] == product.Id) page.ProductIds[i] = string.Empty;

        // Hedef alanı temizle (üzerine düşen ürünleri kaldır)
        for (int dr = 0; dr < rowSpan; dr++)
            for (int dc = 0; dc < colSpan; dc++)
            {
                int idx = (dropRow + dr) * cols + (dropCol + dc);
                if (idx >= 0 && idx < page.ProductIds.Count)
                {
                    var existing = page.ProductIds[idx];
                    if (!string.IsNullOrEmpty(existing))
                    {
                        // O üründen başka yerlerde span'a dahil ham referansları da temizle
                        for (int k = 0; k < page.ProductIds.Count; k++)
                            if (page.ProductIds[k] == existing) page.ProductIds[k] = string.Empty;
                    }
                }
            }

        // Asıl yerleştirme — sadece başlangıç hücresine yaz, span hücreleri "" kalır
        int startIdx = dropRow * cols + dropCol;
        if (startIdx >= 0 && startIdx < page.ProductIds.Count)
            page.ProductIds[startIdx] = product.Id;
    }

    // ============================================================
    //   Palette badges (atanmış işareti)
    // ============================================================

    private void UpdatePaletteBadges()
    {
        var assigned = new HashSet<string>();
        foreach (var page in Catalog.CustomPages)
            foreach (var pid in page.ProductIds)
                if (!string.IsNullOrEmpty(pid)) assigned.Add(pid);

        ProductPalette.UpdateLayout();
        for (int i = 0; i < ProductPalette.Items.Count; i++)
        {
            if (ProductPalette.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp)
            {
                cp.ApplyTemplate();
                if (cp.ContentTemplate?.FindName("AssignedBadge", cp) is Border badge
                    && cp.DataContext is Product product)
                {
                    badge.Visibility = assigned.Contains(product.Id) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    private void UpdateStatus()
    {
        if (CurrentPage is null) { StatusText.Text = "Sayfa eklemek için + Sayfa düğmesine bas."; return; }
        int filled = CurrentPage.ProductIds.Count(s => !string.IsNullOrEmpty(s));
        StatusText.Text = $"Bu sayfada {filled} ürün atanmış · Toplam {Catalog.CustomPages.Count} sayfa";
    }

    // ============================================================
    //   Düzen / sayfa kontrolleri
    // ============================================================

    private void OnLayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentPage is null) return;
        if (LayoutBox.SelectedValue is not string newId) return;
        if (CurrentPage.LayoutId == newId) return;
        CurrentPage.LayoutId = newId;
        _vm.NotifyManualPagesChanged();
        RefreshCurrentPage();
    }

    private void OnAddPage(object sender, RoutedEventArgs e)
    {
        var defaultId = CurrentPage?.LayoutId ?? Catalog.LayoutId;
        Catalog.CustomPages.Add(new CustomPageEntry(defaultId));
        CurrentPageIndex = Catalog.CustomPages.Count - 1;
        _vm.NotifyManualPagesChanged();
        OnPropertyChanged(nameof(PageNumberLabel));
        OnPropertyChanged(nameof(PageNumberFooter));
    }

    private void OnDeletePage(object sender, RoutedEventArgs e)
    {
        if (CurrentPage is null) return;
        Catalog.CustomPages.Remove(CurrentPage);
        if (CurrentPageIndex >= Catalog.CustomPages.Count)
            CurrentPageIndex = Math.Max(0, Catalog.CustomPages.Count - 1);
        else
            OnPropertyChanged(nameof(PageNumberLabel));
        _vm.NotifyManualPagesChanged();
        RefreshCurrentPage();
    }

    private void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (CurrentPageIndex > 0) CurrentPageIndex--;
    }

    private void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (CurrentPageIndex < Catalog.CustomPages.Count - 1) CurrentPageIndex++;
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        Catalog.UseCustomPageLayouts = true;
        _vm.NotifyManualPagesChanged();
        Close();
    }

    // ============================================================
    //   Palette ürünü düzenleme — ProductEditDialog
    // ============================================================

    private void OnEditProductFromPalette(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Product product) return;

        var dialog = new ProductEditDialog(product) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            // Catalog.Products içinde aynı index'i güncelle
            var idx = Catalog.Products.IndexOf(product);
            if (idx >= 0) Catalog.Products[idx] = dialog.Product;
            _vm.NotifyManualPagesChanged();
            // Atamalar dialog.Product.Id eski Product.Id ile aynı (Clone Id'yi de kopyalar),
            // bu yüzden ProductIds listesi geçerli kalır. Sadece görseli yenile.
            RefreshCurrentPage();
        }
    }
}
