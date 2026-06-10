using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;
using AyTeknikKatalog.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AyTeknikKatalog.ViewModels;

/// <summary>
/// Free-form Kapak Tasarım Stüdyosu için ViewModel.
/// Element ekleme/silme, seçim, z-order yönetimi. Drag-drop view tarafında yapılır.
/// </summary>
public partial class CoverDesignerViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<CoverElement> _elements = new();
    [ObservableProperty] private CoverElement? _selectedElement;

    public CoverDesignerViewModel(ObservableCollection<CoverElement> existingElements)
    {
        // Mevcut elementleri klonlayarak başla (cancel olursa orijinal etkilenmez)
        foreach (var e in existingElements)
            Elements.Add(e.Clone());
    }

    [RelayCommand]
    private void AddImage()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Görseller|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Kapak görseli seç"
        };
        if (dlg.ShowDialog() != true) return;

        // Resmi ImageStorage'a kopyala (göreceli olmayan kalıcı path)
        var ext = Path.GetExtension(dlg.FileName);
        var targetPath = ImageStorage.CreateNewPath(ext);
        File.Copy(dlg.FileName, targetPath, overwrite: true);

        var el = new CoverElement
        {
            Type = CoverElementType.Image,
            XMm = 30, YMm = 30, WidthMm = 150, HeightMm = 100,
            Content = targetPath,
            ZIndex = NextZ(),
        };
        Elements.Add(el);
        SelectedElement = el;
    }

    [RelayCommand]
    private void AddTitle()
    {
        var el = new CoverElement
        {
            Type = CoverElementType.Title,
            XMm = 25, YMm = 150, WidthMm = 160, HeightMm = 30,
            Content = "BAŞLIK",
            FontSize = 32, Bold = true,
            ForegroundHex = "#2E338F",
            ZIndex = NextZ(),
        };
        Elements.Add(el);
        SelectedElement = el;
    }

    [RelayCommand]
    private void AddText()
    {
        var el = new CoverElement
        {
            Type = CoverElementType.TextBox,
            XMm = 25, YMm = 200, WidthMm = 160, HeightMm = 15,
            Content = "Buraya metin yazın",
            FontSize = 12,
            ForegroundHex = "#1A1A1F",
            ZIndex = NextZ(),
        };
        Elements.Add(el);
        SelectedElement = el;
    }

    [RelayCommand]
    private void AddRect()
    {
        var el = new CoverElement
        {
            Type = CoverElementType.Rectangle,
            XMm = 25, YMm = 25, WidthMm = 160, HeightMm = 100,
            BackgroundHex = "#2E338F",
            Opacity = 0.15,
            ZIndex = 0,  // arka plan
        };
        Elements.Add(el);
        SelectedElement = el;
    }

    [RelayCommand]
    private void AddLine()
    {
        var el = new CoverElement
        {
            Type = CoverElementType.Line,
            XMm = 30, YMm = 145, WidthMm = 150, HeightMm = 1,
            BackgroundHex = "#2E338F",
            ZIndex = NextZ(),
        };
        Elements.Add(el);
        SelectedElement = el;
    }

    [RelayCommand]
    private void DeleteElement()
    {
        if (SelectedElement is null) return;
        Elements.Remove(SelectedElement);
        SelectedElement = null;
    }

    [RelayCommand]
    private void BringForward()
    {
        if (SelectedElement is null) return;
        SelectedElement.ZIndex = NextZ();
    }

    [RelayCommand]
    private void SendBackward()
    {
        if (SelectedElement is null) return;
        var minZ = Elements.Count == 0 ? 0 : Elements.Min(e => e.ZIndex);
        SelectedElement.ZIndex = minZ - 1;
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (SelectedElement is null) return;
        var copy = SelectedElement.Clone();
        copy.XMm += 5; copy.YMm += 5;
        copy.ZIndex = NextZ();
        Elements.Add(copy);
        SelectedElement = copy;
    }

    [RelayCommand]
    private void ClearAll()
    {
        Elements.Clear();
        SelectedElement = null;
    }

    private int NextZ() => Elements.Count == 0 ? 1 : Elements.Max(e => e.ZIndex) + 1;
}
