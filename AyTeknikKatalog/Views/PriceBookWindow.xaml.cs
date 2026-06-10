using System.Windows;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class PriceBookWindow : Window
{
    public PriceBookWindow()
    {
        InitializeComponent();
        DataContext = new PriceBookViewModel();
    }
}
