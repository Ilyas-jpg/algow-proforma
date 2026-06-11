using System.Windows;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class BulkSendWindow : Window
{
    public BulkSendWindow()
    {
        InitializeComponent();
        DataContext = new BulkSendViewModel();
    }
}
