using AlgowProforma.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlgowProforma.ViewModels;

/// <summary>
/// Tasarımlar sekmesindeki bir kapak-tasarım kartı. <see cref="Design"/> = meta (id/ad/açıklama);
/// <see cref="Thumbnail"/> = o tasarımın GERÇEK kapak önizlemesi (arka-thread render edilip bağlanır,
/// elle çizilmiş XAML mini'lerin yerini alır → drift yok, yeni kapak eklerken thumbnail authoring yok).
/// </summary>
public partial class DesignCardVM : ObservableObject
{
    public PdfDesign Design { get; }

    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? _thumbnail;

    public DesignCardVM(PdfDesign design) => Design = design;
}
