using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlgowProforma.Models;
using AlgowProforma.Services;

namespace AlgowProforma.Views;

/// <summary>
/// Teklif editöründen ayrılmadan müşteri ekleme — e-postası CRM'de olmayan müşteriye teklif
/// gönderme akışı tıkanıyordu (Müşteriler penceresi ayrı modal, editör alanları snapshot).
/// Kaydedilen müşteri çağırana <see cref="Result"/> ile döner; CRM yazımı çağıranın işi
/// (VM listeyi de güncelleyecek).
/// </summary>
public partial class QuickCustomerDialog : Window
{
    public Customer? Result { get; private set; }

    public QuickCustomerDialog() => InitializeComponent();

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (SaveBtn is null) return;
        bool hasCompany = !string.IsNullOrWhiteSpace(CompanyBox.Text);
        var email = EmailBox.Text.Trim();
        bool emailOk = email.Length == 0 || EmailValidator.IsValid(email);
        SaveBtn.IsEnabled = hasCompany && emailOk;
        ValidationText.Visibility = emailOk ? Visibility.Collapsed : Visibility.Visible;
        ValidationText.Text = emailOk ? "" : "E-posta biçimi geçersiz görünüyor.";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Result = new Customer
        {
            CompanyName = CompanyBox.Text.Trim(),
            ContactName = ContactBox.Text.Trim(),
            Email = EmailBox.Text.Trim(),
            Phone = PhoneBox.Text.Trim(),
            Salutation = (SalutationCombo.SelectedIndex) switch
            {
                1 => Salutation.Bey,
                2 => Salutation.Hanim,
                _ => Salutation.Yok,
            },
        };
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
