using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class PriceBookWindow : Window
{
    public PriceBookWindow()
    {
        InitializeComponent();
        DataContext = new PriceBookViewModel();
    }
}
