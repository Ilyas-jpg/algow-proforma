using System.Windows;
using System.Windows.Controls;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Views;

public class DesignPreviewSelector : DataTemplateSelector
{
    public DataTemplate? Klasik { get; set; }
    public DataTemplate? Egri { get; set; }
    public DataTemplate? Cerceve { get; set; }
    public DataTemplate? Geometri { get; set; }
    public DataTemplate? Madalya { get; set; }
    public DataTemplate? Katman { get; set; }
    public DataTemplate? Mozaik { get; set; }
    public DataTemplate? Diyagonal { get; set; }
    public DataTemplate? Minimalist { get; set; }
    public DataTemplate? Sertifika { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is not PdfDesign d ? null : d.Id switch
        {
            "klasik" => Klasik,
            "egri" => Egri,
            "cerceve" => Cerceve,
            "geometri" => Geometri,
            "madalya" => Madalya,
            "katman" => Katman,
            "mozaik" => Mozaik,
            "diyagonal" => Diyagonal,
            "minimalist" => Minimalist,
            "sertifika" => Sertifika,
            _ => Klasik,
        };
}
