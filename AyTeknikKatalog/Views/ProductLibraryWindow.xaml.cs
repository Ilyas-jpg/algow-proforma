using System.Windows;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class ProductLibraryWindow : Window
{
    public ProductLibraryWindow(ProductLibraryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
