using System.Windows;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class CustomersWindow : Window
{
    public CustomersWindow()
    {
        InitializeComponent();
        DataContext = new CustomersViewModel();
    }
}
