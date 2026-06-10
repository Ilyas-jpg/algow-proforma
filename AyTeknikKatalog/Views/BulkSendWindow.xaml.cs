using System.Windows;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class BulkSendWindow : Window
{
    public BulkSendWindow()
    {
        InitializeComponent();
        DataContext = new BulkSendViewModel();
    }
}
