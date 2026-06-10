using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class CoverDesignerWindow : Window
{
    private const double Scale = 2.5;  // px per mm

    private readonly CoverDesignerViewModel _vm;
    private CoverElement? _draggingElement;
    private Point _dragStartPoint;
    private double _dragStartElX;
    private double _dragStartElY;

    public ObservableCollection<CoverElement> ResultElements => _vm.Elements;
    public bool Saved { get; private set; }

    public CoverDesignerWindow(ObservableCollection<CoverElement> existing)
    {
        InitializeComponent();
        _vm = new CoverDesignerViewModel(existing);
        DataContext = _vm;

        _vm.PropertyChanged += OnVmPropertyChanged;
        UpdateResizeThumb();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CoverDesignerViewModel.SelectedElement))
        {
            UpdateResizeThumb();
            if (_vm.SelectedElement is not null)
                _vm.SelectedElement.PropertyChanged += OnSelectedElementPropertyChanged;
        }
    }

    private void OnSelectedElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateResizeThumb();
    }

    /// <summary>Resize thumb'u seçili elementin sağ-alt köşesine konumlandırır</summary>
    private void UpdateResizeThumb()
    {
        var el = _vm.SelectedElement;
        if (el is null)
        {
            ResizeThumb.Visibility = Visibility.Collapsed;
            return;
        }
        ResizeThumb.Visibility = Visibility.Visible;
        Canvas.SetLeft(ResizeThumb, (el.XMm + el.WidthMm) * Scale - 7);
        Canvas.SetTop(ResizeThumb, (el.YMm + el.HeightMm) * Scale - 7);
        Panel.SetZIndex(ResizeThumb, 10000);
    }

    /// <summary>Canvas'a boş alana tıklama → seçimi kaldır</summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == DesignCanvas || e.OriginalSource is Canvas)
            _vm.SelectedElement = null;
    }

    /// <summary>Bir element border'ına basıldı → select + drag başlat</summary>
    private void OnElementMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.Tag is not CoverElement el) return;
        _vm.SelectedElement = el;
        _draggingElement = el;
        _dragStartPoint = e.GetPosition(DesignCanvas);
        _dragStartElX = el.XMm;
        _dragStartElY = el.YMm;
        b.CaptureMouse();
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_draggingElement is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _draggingElement = null;
            return;
        }
        var pos = e.GetPosition(DesignCanvas);
        var dxMm = (pos.X - _dragStartPoint.X) / Scale;
        var dyMm = (pos.Y - _dragStartPoint.Y) / Scale;

        var newX = _dragStartElX + dxMm;
        var newY = _dragStartElY + dyMm;

        // Sınırlar: A4 içinde tut (210×297 mm)
        newX = Math.Max(0, Math.Min(210 - _draggingElement.WidthMm, newX));
        newY = Math.Max(0, Math.Min(297 - _draggingElement.HeightMm, newY));

        _draggingElement.XMm = newX;
        _draggingElement.YMm = newY;
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (_draggingElement is not null)
        {
            Mouse.Capture(null);
            _draggingElement = null;
        }
    }

    /// <summary>Sağ-alt köşe Thumb'tan drag → resize</summary>
    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        var el = _vm.SelectedElement;
        if (el is null) return;
        var dxMm = e.HorizontalChange / Scale;
        var dyMm = e.VerticalChange / Scale;
        el.WidthMm = Math.Max(5, Math.Min(210 - el.XMm, el.WidthMm + dxMm));
        el.HeightMm = Math.Max(5, Math.Min(297 - el.YMm, el.HeightMm + dyMm));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_vm.SelectedElement is null) return;

        // Klavye kısayolları (focus textbox'ta değilken)
        if (FocusManager.GetFocusedElement(this) is TextBox) return;

        switch (e.Key)
        {
            case Key.Delete:
                _vm.DeleteElementCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D when Keyboard.Modifiers == ModifierKeys.Control:
                _vm.DuplicateCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Saved = true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Saved = false;
        DialogResult = false;
        Close();
    }
}
