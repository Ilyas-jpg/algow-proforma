using System.Windows;
using System.Windows.Input;

namespace AyTeknikKatalog.Views;

public partial class NameInputDialog : Window
{
    public string EnteredName { get; private set; } = string.Empty;

    public NameInputDialog(string title, string initialValue, string? helper = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        NameBox.Text = initialValue ?? string.Empty;
        if (helper is not null) HelperText.Text = helper;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        EnteredName = NameBox.Text?.Trim() ?? string.Empty;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
