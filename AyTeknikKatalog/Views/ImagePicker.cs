using System.Windows;
using Microsoft.Win32;

namespace AyTeknikKatalog.Views;

public static class ImagePicker
{
    private const string Filter = "Görseller|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

    public static string? PickAndCrop(Window? owner, string title, double aspectRatio = 0)
    {
        var dlg = new OpenFileDialog { Filter = Filter, Title = title };
        if (dlg.ShowDialog() != true) return null;

        var cropper = new ImageCropperDialog(dlg.FileName, aspectRatio) { Owner = owner };
        return cropper.ShowDialog() == true ? cropper.OutputPath : null;
    }
}
