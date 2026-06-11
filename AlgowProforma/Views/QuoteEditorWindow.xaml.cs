using System.Windows;
using AlgowProforma.Models;
using AlgowProforma.ViewModels;

namespace AlgowProforma.Views;

public partial class QuoteEditorWindow : Window
{
    public QuoteEditorWindow(Quote? existing = null)
    {
        InitializeComponent();
        DataContext = new QuoteEditorViewModel(existing);
    }
}
