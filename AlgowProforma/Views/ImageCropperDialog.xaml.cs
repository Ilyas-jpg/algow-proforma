using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AlgowProforma.Services;

namespace AlgowProforma.Views;

public partial class ImageCropperDialog : Window
{
    private readonly string _sourcePath;
    private readonly BitmapImage _bitmap;
    private double _aspectRatio;
    private bool _isDragging;
    private Point _dragStart;
    private Rect _initialCropRect;

    public string? OutputPath { get; private set; }

    public ImageCropperDialog(string sourcePath, double aspectRatio = 0)
    {
        InitializeComponent();
        _sourcePath = sourcePath;
        _aspectRatio = aspectRatio;

        _bitmap = new BitmapImage();
        _bitmap.BeginInit();
        _bitmap.CacheOption = BitmapCacheOption.OnLoad;
        // Preview için max 2048px downsample — büyük (6000×4000+) fotoğraflarda RAM patlamasını engeller.
        // Crop pixel koordinatları zaten ratio-bazlı (PixelWidth/PixelHeight) hesaplandığı için downsample sonucu bozmaz.
        _bitmap.DecodePixelWidth = 2048;
        _bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
        _bitmap.EndInit();
        _bitmap.Freeze();

        SourceImage.Source = _bitmap;
        Loaded += (_, _) => InitializeCrop();
    }

    private double DisplayLeft => GetDisplayedImageBounds().Left;
    private double DisplayTop => GetDisplayedImageBounds().Top;
    private double DisplayWidth => GetDisplayedImageBounds().Width;
    private double DisplayHeight => GetDisplayedImageBounds().Height;

    private Rect GetDisplayedImageBounds()
    {
        var areaW = WorkArea.ActualWidth;
        var areaH = WorkArea.ActualHeight;
        if (areaW <= 0 || areaH <= 0 || _bitmap.PixelWidth == 0)
            return new Rect(0, 0, areaW, areaH);
        var imgRatio = (double)_bitmap.PixelWidth / _bitmap.PixelHeight;
        var areaRatio = areaW / areaH;
        double w, h;
        if (imgRatio > areaRatio)
        {
            w = areaW;
            h = areaW / imgRatio;
        }
        else
        {
            h = areaH;
            w = areaH * imgRatio;
        }
        return new Rect((areaW - w) / 2, (areaH - h) / 2, w, h);
    }

    private void InitializeCrop()
    {
        var bounds = GetDisplayedImageBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var inset = Math.Min(bounds.Width, bounds.Height) * 0.08;
        var cropW = bounds.Width - inset * 2;
        var cropH = bounds.Height - inset * 2;
        if (_aspectRatio > 0)
        {
            if (cropW / cropH > _aspectRatio)
                cropW = cropH * _aspectRatio;
            else
                cropH = cropW / _aspectRatio;
        }
        var left = bounds.Left + (bounds.Width - cropW) / 2;
        var top = bounds.Top + (bounds.Height - cropH) / 2;
        SetCropRect(new Rect(left, top, cropW, cropH));
    }

    private void OnWorkAreaSizeChanged(object sender, SizeChangedEventArgs e) => InitializeCrop();

    private Rect CurrentRect => new(
        Canvas.GetLeft(CropRect), Canvas.GetTop(CropRect),
        CropRect.Width, CropRect.Height);

    private void SetCropRect(Rect r)
    {
        var bounds = GetDisplayedImageBounds();
        var minSize = 24;
        r.Width = Math.Max(minSize, r.Width);
        r.Height = Math.Max(minSize, r.Height);
        r.X = Math.Max(bounds.Left, Math.Min(r.X, bounds.Right - r.Width));
        r.Y = Math.Max(bounds.Top, Math.Min(r.Y, bounds.Bottom - r.Height));
        if (r.Right > bounds.Right) r.Width = bounds.Right - r.X;
        if (r.Bottom > bounds.Bottom) r.Height = bounds.Bottom - r.Y;

        Canvas.SetLeft(CropRect, r.X);
        Canvas.SetTop(CropRect, r.Y);
        CropRect.Width = r.Width;
        CropRect.Height = r.Height;

        UpdateHandles(r);
        UpdateShade(r, bounds);
        UpdateDims(r, bounds);
    }

    private void UpdateHandles(Rect r)
    {
        const double half = 7;
        Canvas.SetLeft(HandleNW, r.X - half);
        Canvas.SetTop(HandleNW, r.Y - half);
        Canvas.SetLeft(HandleNE, r.Right - half);
        Canvas.SetTop(HandleNE, r.Y - half);
        Canvas.SetLeft(HandleSW, r.X - half);
        Canvas.SetTop(HandleSW, r.Bottom - half);
        Canvas.SetLeft(HandleSE, r.Right - half);
        Canvas.SetTop(HandleSE, r.Bottom - half);
    }

    private void UpdateShade(Rect r, Rect bounds)
    {
        Canvas.SetLeft(ShadeTop, bounds.Left);
        Canvas.SetTop(ShadeTop, bounds.Top);
        ShadeTop.Width = bounds.Width;
        ShadeTop.Height = Math.Max(0, r.Top - bounds.Top);

        Canvas.SetLeft(ShadeBottom, bounds.Left);
        Canvas.SetTop(ShadeBottom, r.Bottom);
        ShadeBottom.Width = bounds.Width;
        ShadeBottom.Height = Math.Max(0, bounds.Bottom - r.Bottom);

        Canvas.SetLeft(ShadeLeft, bounds.Left);
        Canvas.SetTop(ShadeLeft, r.Top);
        ShadeLeft.Width = Math.Max(0, r.Left - bounds.Left);
        ShadeLeft.Height = r.Height;

        Canvas.SetLeft(ShadeRight, r.Right);
        Canvas.SetTop(ShadeRight, r.Top);
        ShadeRight.Width = Math.Max(0, bounds.Right - r.Right);
        ShadeRight.Height = r.Height;
    }

    private void UpdateDims(Rect r, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var px = (int)Math.Round(r.Width / bounds.Width * _bitmap.PixelWidth);
        var py = (int)Math.Round(r.Height / bounds.Height * _bitmap.PixelHeight);
        DimsText.Text = $"Çıktı: {px}×{py} px";
    }

    private void OnCropMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(OverlayCanvas);
        _initialCropRect = CurrentRect;
        CropRect.CaptureMouse();
    }

    private void OnCropMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var p = e.GetPosition(OverlayCanvas);
        var dx = p.X - _dragStart.X;
        var dy = p.Y - _dragStart.Y;
        SetCropRect(new Rect(_initialCropRect.X + dx, _initialCropRect.Y + dy,
            _initialCropRect.Width, _initialCropRect.Height));
    }

    private void OnCropMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        CropRect.ReleaseMouseCapture();
    }

    private void OnHandleDragNW(object sender, DragDeltaEventArgs e) => ResizeFromCorner(e, true, true);
    private void OnHandleDragNE(object sender, DragDeltaEventArgs e) => ResizeFromCorner(e, false, true);
    private void OnHandleDragSW(object sender, DragDeltaEventArgs e) => ResizeFromCorner(e, true, false);
    private void OnHandleDragSE(object sender, DragDeltaEventArgs e) => ResizeFromCorner(e, false, false);

    private void ResizeFromCorner(DragDeltaEventArgs e, bool moveLeft, bool moveTop)
    {
        var r = CurrentRect;
        double nx = r.X, ny = r.Y, nw = r.Width, nh = r.Height;
        var dx = e.HorizontalChange;
        var dy = e.VerticalChange;

        if (moveLeft) { nx += dx; nw -= dx; }
        else { nw += dx; }
        if (moveTop) { ny += dy; nh -= dy; }
        else { nh += dy; }

        if (_aspectRatio > 0)
        {
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                var newH = nw / _aspectRatio;
                if (moveTop) ny += nh - newH;
                nh = newH;
            }
            else
            {
                var newW = nh * _aspectRatio;
                if (moveLeft) nx += nw - newW;
                nw = newW;
            }
        }

        SetCropRect(new Rect(nx, ny, Math.Max(24, nw), Math.Max(24, nh)));
    }

    private void OnAspectFree(object sender, RoutedEventArgs e) => ApplyAspect(0);
    private void OnAspectSquare(object sender, RoutedEventArgs e) => ApplyAspect(1);
    private void OnAspect43(object sender, RoutedEventArgs e) => ApplyAspect(4.0 / 3);
    private void OnAspect169(object sender, RoutedEventArgs e) => ApplyAspect(16.0 / 9);

    private void ApplyAspect(double ratio)
    {
        _aspectRatio = ratio;
        InitializeCrop();
    }

    private void OnResetCrop(object sender, RoutedEventArgs e)
    {
        _aspectRatio = 0;
        var b = GetDisplayedImageBounds();
        SetCropRect(b);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var bounds = GetDisplayedImageBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                DialogResult = false;
                return;
            }

            var r = CurrentRect;
            var sx = (r.X - bounds.X) / bounds.Width * _bitmap.PixelWidth;
            var sy = (r.Y - bounds.Y) / bounds.Height * _bitmap.PixelHeight;
            var sw = r.Width / bounds.Width * _bitmap.PixelWidth;
            var sh = r.Height / bounds.Height * _bitmap.PixelHeight;

            var ix = (int)Math.Round(Math.Max(0, sx));
            var iy = (int)Math.Round(Math.Max(0, sy));
            var iw = (int)Math.Round(Math.Min(_bitmap.PixelWidth - ix, sw));
            var ih = (int)Math.Round(Math.Min(_bitmap.PixelHeight - iy, sh));
            if (iw < 4 || ih < 4) throw new InvalidOperationException("Seçili alan çok küçük.");

            var cropped = new CroppedBitmap(_bitmap, new Int32Rect(ix, iy, iw, ih));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(cropped));

            var path = ImageStorage.CreateNewPath(".png");
            using (var stream = File.Create(path))
                encoder.Save(stream);

            OutputPath = path;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kırpma başarısız", MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
