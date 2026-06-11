using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class CustomersWindow : Window
{
    public CustomersWindow()
    {
        InitializeComponent();
        DataContext = new CustomersViewModel();
    }
}
