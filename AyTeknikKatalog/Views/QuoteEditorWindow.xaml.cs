using System.Windows;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.ViewModels;

namespace AyTeknikKatalog.Views;

public partial class QuoteEditorWindow : Window
{
    public QuoteEditorWindow(Quote? existing = null)
    {
        InitializeComponent();
        DataContext = new QuoteEditorViewModel(existing);
    }
}
