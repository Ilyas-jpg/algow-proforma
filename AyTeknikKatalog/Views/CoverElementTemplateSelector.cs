using System.Windows;
using System.Windows.Controls;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Views;

/// <summary>
/// CoverElement.Type'a göre DataTemplate seçer. App.xaml'da template'ler tanımlanır.
/// </summary>
public class CoverElementTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? TitleTemplate { get; set; }
    public DataTemplate? TextBoxTemplate { get; set; }
    public DataTemplate? RectangleTemplate { get; set; }
    public DataTemplate? LineTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is not CoverElement el ? null : el.Type switch
        {
            CoverElementType.Image => ImageTemplate,
            CoverElementType.Title => TitleTemplate,
            CoverElementType.TextBox => TextBoxTemplate,
            CoverElementType.Rectangle => RectangleTemplate,
            CoverElementType.Line => LineTemplate,
            _ => null,
        };
}
