using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace AlgowProforma.Views;

/// <summary>
/// Toplu gönderim öncesi GERÇEK örnek önizleme: ilk seçili alıcının adına üretilmiş PDF +
/// kişiselleştirilmiş gövde/konu/ek adı. Eski akış yalnız metinli MessageBox onayıydı —
/// KULLANIM'ın "Önizleme zorunludur" vaadi artık kodda. DialogResult=true → gönderim başlar.
/// </summary>
public partial class BulkPreviewDialog : Window
{
    private readonly string _samplePdfPath;

    public BulkPreviewDialog(string quoteTitle, string providerLabel, int recipientCount,
        string sampleRecipient, string subject, string body, string attachmentName, string samplePdfPath)
    {
        InitializeComponent();
        _samplePdfPath = samplePdfPath;

        QuoteText.Text = quoteTitle;
        ProviderText.Text = providerLabel;
        CountText.Text = $"{recipientCount} alıcı — her birine kendi adına PDF üretilir";
        SampleRecipientText.Text = sampleRecipient;
        AttachmentText.Text = attachmentName;
        SubjectBox.Text = subject;
        BodyBox.Text = body;
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnOpenSamplePdf(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_samplePdfPath)) return;
            Process.Start(new ProcessStartInfo(_samplePdfPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "PDF açılamadı: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
