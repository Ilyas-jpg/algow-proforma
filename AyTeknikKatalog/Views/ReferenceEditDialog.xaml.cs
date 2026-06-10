using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Views;

public partial class ReferenceEditDialog : Window
{
    public Reference Reference { get; }

    public ReferenceEditDialog(Reference seed)
    {
        InitializeComponent();
        Reference = seed.Clone();
        DataContext = Reference;
        TitleText.Text = string.IsNullOrWhiteSpace(seed.Name) ? "Yeni Referans" : "Referansı Düzenle";
        Reference.PropertyChanged += OnReferenceChanged;
        UpdateSaveState();
    }

    private void OnReferenceChanged(object? sender, PropertyChangedEventArgs e) => UpdateSaveState();

    private void UpdateSaveState() => SaveBtn.IsEnabled =
        !string.IsNullOrWhiteSpace(Reference.Name) || !string.IsNullOrWhiteSpace(Reference.LogoPath);

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!SaveBtn.IsEnabled) return;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnBrowseLogo(object sender, RoutedEventArgs e)
    {
        var path = ImagePicker.PickAndCrop(this, "Firma logosu seç");
        if (path is not null) Reference.LogoPath = path;
    }
}
