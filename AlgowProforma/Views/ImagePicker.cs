using System.Windows;
using Microsoft.Win32;
using AlgowProforma.Services;

namespace AlgowProforma.Views;

public static class ImagePicker
{
    private const string Filter = "Görseller|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

    public static string? PickAndCrop(Window? owner, string title, double aspectRatio = 0)
    {
        var dlg = new OpenFileDialog { Filter = Filter, Title = title };
        if (dlg.ShowDialog() != true) return null;

        if (!ImportLimits.IsImageSizeOk(dlg.FileName, out var error))
        {
            MessageBox.Show(error, "Görsel çok büyük", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var cropper = new ImageCropperDialog(dlg.FileName, aspectRatio) { Owner = owner };
        return cropper.ShowDialog() == true ? cropper.OutputPath : null;
    }
}
