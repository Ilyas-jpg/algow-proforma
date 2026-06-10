using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AyTeknikKatalog.Models;
using AyTeknikKatalog.Services;
using AyTeknikKatalog.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AyTeknikKatalog.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LibraryService _libraryService = new();
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private bool _brandDirty;   // kullanıcı marka alanı düzenledi mi → autosave kalıcı marka profilini yazar

    private void StartAutoSaveTimer()
    {
        // Anlık autosave (debounce): son değişiklikten 2 sn sonra session'a yazılır.
        // 5 dk beklemek yerine her düzenleme korunur — kapanış/çökme sonrası iş kaybolmaz.
        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _autoSaveTimer.Tick += (_, _) => { _autoSaveTimer?.Stop(); TryAutoSave(); };
    }

    /// <summary>Bir değişiklik olunca debounce sayacını sıfırlar (2 sn sessizlikten sonra autosave).</summary>
    private void ScheduleAutoSave()
    {
        if (_autoSaveTimer is null) return;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    /// <summary>Pencere kapanırken çağrılır — bekleyen değişikliği anında session'a yazar.</summary>
    public void SaveSessionNow()
    {
        _autoSaveTimer?.Stop();
        TryAutoSave();
    }

    private static string GetAutoSavePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlgowProforma", "autosave.json");

    private void TryDeleteAutoSave()
    {
        try { var p = GetAutoSavePath(); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    /// <summary>
    /// Window Loaded'da çağrılır. Autosave dosyası varsa kullanıcıya yükleme/atma sorusu sorar.
    /// </summary>
    public void CheckCrashRecovery()
    {
        try
        {
            var path = GetAutoSavePath();
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            var ageMin = (int)(DateTime.Now - info.LastWriteTime).TotalMinutes;

            var result = MessageBox.Show(
                $"Önceki oturumdan otomatik kaydedilmiş bir katalog bulundu " +
                $"({ageMin} dakika önce, {info.Length / 1024} KB).\n\n" +
                "Yüklemek ister misin?\n\n" +
                "Hayır seçerseniz autosave silinecek.",
                "Kurtarılan Veri Bulundu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Catalog = new CatalogService().Load(path);
                ActiveName = "Kurtarılan oturum";
                IsDirty = true;
                StatusMessage = "Önceki oturum kurtarıldı — kaydetmek için Ctrl+S.";
                RefreshCoverPreview();
            }
            else
            {
                try { File.Delete(path); } catch { }
                StatusMessage = "Önceki oturum atıldı.";
            }
        }
        catch (Exception ex)
        {
            // Kurtarma başarısız olduysa kullanıcıyı sessiz bırakma, basit log
            StatusMessage = $"Kurtarma başarısız: {ex.Message}";
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<Models.CatalogEntry> RecentEntries { get; } = new();

    /// <summary>
    /// "Son Kataloglar" listesini library'den okuyup günceller. Library açılışında + her save sonrası çağrılır.
    /// </summary>
    public void RefreshRecentEntries()
    {
        RecentEntries.Clear();
        foreach (var e in GetRecentEntries(8))
            RecentEntries.Add(e);
    }

    /// <summary>
    /// Library'deki tüm katalog entries, son değişiklik tarihine göre azalan sıralı.
    /// </summary>
    public System.Collections.Generic.List<Models.CatalogEntry> GetRecentEntries(int max = 8)
    {
        try
        {
            return _libraryService.ListEntries()
                .OrderByDescending(e => e.CreatedAt)
                .Take(max)
                .ToList();
        }
        catch
        {
            return new System.Collections.Generic.List<Models.CatalogEntry>();
        }
    }

    [RelayCommand]
    private void LoadRecentCatalog(Models.CatalogEntry? entry)
    {
        if (entry is null) return;
        if (!ConfirmDiscardChanges()) return;
        try
        {
            var loaded = new CatalogService().Load(entry.JsonPath);
            Catalog = loaded;
            ActiveName = entry.Name;
            IsDirty = false;
            StatusMessage = $"Yüklendi: {entry.Name}";
            RefreshCoverPreview();
        }
        catch (Exception ex)
        {
            ShowError("Katalog yüklenemedi", ex.Message);
        }
    }

    private void TryAutoSave()
    {
        if (!IsDirty) return;
        try
        {
            var path = GetAutoSavePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            new CatalogService().Save(path, Catalog);
            // Marka profili: kullanıcı marka alanlarını düzenlediyse kalıcı sakla.
            // (data\ klasörü = kurulum DIŞI → güncellemede kaybolmaz; yeni kataloglara uygulanır.)
            if (_brandDirty && !string.IsNullOrWhiteSpace(Catalog.Brand.Name))
            {
                BrandProfileService.Save(Catalog.Brand);
                _brandDirty = false;
            }
            StatusMessage = $"Otomatik kaydedildi: {DateTime.Now:HH:mm}";
        }
        catch { /* sessiz — auto-save UI kesintisi yapmaz */ }
    }

    [ObservableProperty] private Catalog _catalog = Catalog.CreateDefault();
    [ObservableProperty] private string _statusMessage = "Hazır.";

    public System.Collections.Generic.IReadOnlyList<PdfTheme> AvailableThemes => PdfTheme.All;
    public System.Collections.Generic.IReadOnlyList<PdfDesign> AvailableDesigns => PdfDesign.All;
    public System.Collections.Generic.IReadOnlyList<PageLayout> AvailableLayouts => PageLayout.All;

    [RelayCommand]
    private void SelectTheme(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;
        Catalog.ThemeId = themeId;
        var theme = PdfTheme.GetById(themeId);
        StatusMessage = $"Tema seçildi: {theme.Name}";
        RefreshCoverPreview();
    }

    [RelayCommand]
    private void SelectDesign(string? designId)
    {
        if (string.IsNullOrWhiteSpace(designId)) return;
        Catalog.DesignId = designId;
        var design = PdfDesign.GetById(designId);
        StatusMessage = $"Tasarım seçildi: {design.Name}";
        RefreshCoverPreview();
    }

    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? _coverPreviewImage;
    [ObservableProperty] private bool _isCoverPreviewLoading;

    /// <summary>
    /// Background thread'de kapak sayfası PNG render eder, UI thread'de Image control'e bind eder.
    /// Tasarım değişince + kapak görseli yüklenince/silinince çağrılır.
    /// </summary>
    public void RefreshCoverPreview()
    {
        IsCoverPreviewLoading = true;
        var catalogSnapshot = Catalog; // closure capture
        System.Threading.Tasks.Task.Run(() =>
        {
            var bytes = PdfService.GenerateCoverPreviewBytes(catalogSnapshot);
            System.Windows.Media.Imaging.BitmapSource? bmp = null;
            if (bytes is { Length: > 0 })
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    bmp = bi;
                }
                catch { }
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CoverPreviewImage = bmp;
                IsCoverPreviewLoading = false;
            });
        });
    }

    [RelayCommand]
    private void SelectLayout(string? layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId)) return;
        Catalog.LayoutId = layoutId;
        IsDirty = true;
        OnPropertyChanged(nameof(CalculatedPageCount));
        OnPropertyChanged(nameof(ProductsPerLayoutPage));
        var layout = PageLayout.GetById(layoutId);
        StatusMessage = $"Sayfa düzeni: {layout.Name} ({layout.Total} ürün/sayfa)";
        RefreshCoverPreview();
    }

    [RelayCommand]
    private void OpenCoversFolder()
    {
        try
        {
            Directory.CreateDirectory(ImageStorage.CoversDirectory);
            Process.Start(new ProcessStartInfo(ImageStorage.CoversDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Klasör açılamadı", ex.Message); }
    }

    [ObservableProperty] private int _designBackgroundsRefresh;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _activeName = "Yeni Katalog";

    public string WindowTitle => $"{ActiveName}{(IsDirty ? " *" : "")} — Algow Proforma PDF";

    public int CalculatedPageCount
    {
        get
        {
            int productPages;
            if (Catalog.UseCustomPageLayouts && Catalog.CustomPages.Count > 0)
            {
                int remaining = Catalog.Products.Count(p => !p.HasTable)
                    + Catalog.Products.Count(p => p.HasTable) * 2;
                productPages = 0;
                int idx = 0;
                while (remaining > 0 && productPages < 9999)
                {
                    var entry = idx < Catalog.CustomPages.Count
                        ? Catalog.CustomPages[idx]
                        : Catalog.CustomPages[Catalog.CustomPages.Count - 1];
                    var l = PageLayout.GetById(entry.LayoutId);
                    remaining -= Math.Max(1, l.Total);
                    productPages++;
                    idx++;
                }
            }
            else
            {
                var layout = PageLayout.GetById(Catalog.LayoutId);
                var perPage = Math.Max(1, layout.Total);
                var regularCount = Catalog.Products.Count(p => !p.HasTable);
                var tableCount = Catalog.Products.Count(p => p.HasTable);
                var tableSlotWeight = Math.Max(2, perPage / 2);
                var effective = regularCount + tableCount * tableSlotWeight;
                productPages = effective == 0 ? 0 : (int)Math.Ceiling(effective / (double)perPage);
            }
            var refPages = (Catalog.SkipReferencesPage || Catalog.References.Count == 0)
                ? 0
                : (int)Math.Ceiling(Catalog.References.Count / 25.0);
            return 1 + refPages + productPages;
        }
    }

    public int ProductsPerLayoutPage => PageLayout.GetById(Catalog.LayoutId).Total;

    [RelayCommand]
    private void AddCustomPage(string? layoutId)
    {
        var id = string.IsNullOrWhiteSpace(layoutId) ? Catalog.LayoutId : layoutId;
        Catalog.CustomPages.Add(new CustomPageEntry(id));
        IsDirty = true;
        OnPropertyChanged(nameof(CalculatedPageCount));
    }

    [RelayCommand]
    private void RemoveCustomPage(CustomPageEntry? entry)
    {
        if (entry is null) return;
        Catalog.CustomPages.Remove(entry);
        IsDirty = true;
        OnPropertyChanged(nameof(CalculatedPageCount));
    }

    [RelayCommand]
    private void MoveCustomPageUp(CustomPageEntry? entry)
    {
        if (entry is null) return;
        var i = Catalog.CustomPages.IndexOf(entry);
        if (i <= 0) return;
        Catalog.CustomPages.Move(i, i - 1);
        IsDirty = true;
    }

    [RelayCommand]
    private void MoveCustomPageDown(CustomPageEntry? entry)
    {
        if (entry is null) return;
        var i = Catalog.CustomPages.IndexOf(entry);
        if (i < 0 || i >= Catalog.CustomPages.Count - 1) return;
        Catalog.CustomPages.Move(i, i + 1);
        IsDirty = true;
    }

    [RelayCommand]
    private void OpenPagePlanner()
    {
        var window = new PagePlannerWindow(this);
        window.ShowDialog();
    }

    /// <summary>Henüz hiçbir manuel sayfaya atanmamış ürünler.</summary>
    public System.Collections.Generic.IEnumerable<Product> UnassignedProducts
    {
        get
        {
            var assigned = new System.Collections.Generic.HashSet<string>();
            foreach (var page in Catalog.CustomPages)
                foreach (var id in page.ProductIds)
                    assigned.Add(id);
            return Catalog.Products.Where(p => !assigned.Contains(p.Id)).ToList();
        }
    }

    public void NotifyManualPagesChanged()
    {
        OnPropertyChanged(nameof(UnassignedProducts));
        OnPropertyChanged(nameof(CalculatedPageCount));
        IsDirty = true;
    }

    public void AssignProductToPage(CustomPageEntry page, Product product)
    {
        if (page is null || product is null) return;
        if (page.ProductIds.Contains(product.Id)) return;
        page.ProductIds.Add(product.Id);
        NotifyManualPagesChanged();
    }

    public void UnassignProductFromPage(CustomPageEntry page, string productId)
    {
        if (page is null || string.IsNullOrEmpty(productId)) return;
        page.ProductIds.Remove(productId);
        NotifyManualPagesChanged();
    }

    public MainViewModel()
    {
        StartAutoSaveTimer();
        // Kayıtlı marka profilini uygula (kurulum markası yeni kataloğa taşınır).
        // Catalog set'i OnCatalogChanged'i tetikler → SubscribeToCatalog otomatik çalışır.
        Catalog = NewCatalogWithProfile();
        RefreshRecentEntries();
    }

    /// <summary>Yeni boş katalog + (varsa) kayıtlı kalıcı marka profili uygulanmış.</summary>
    private static Catalog NewCatalogWithProfile()
    {
        var c = Catalog.CreateDefault();
        var profile = BrandProfileService.Load();
        if (profile is not null) c.Brand = profile;
        return c;
    }

    /// <summary>Kullanıcı marka alanı düzenleyince işaretle — autosave bunu kalıcı profile yazar.</summary>
    private void OnBrandChanged(object? sender, PropertyChangedEventArgs e) => _brandDirty = true;

    partial void OnCatalogChanged(Catalog? oldValue, Catalog newValue)
    {
        if (oldValue is not null) UnsubscribeFromCatalog(oldValue);
        SubscribeToCatalog(newValue);
        OnPropertyChanged(nameof(CalculatedPageCount));
    }

    private void SubscribeToCatalog(Catalog catalog)
    {
        catalog.PropertyChanged += OnGraphChanged;
        catalog.Brand.PropertyChanged += OnGraphChanged;
        catalog.Brand.PropertyChanged += OnBrandChanged;
        catalog.Cover.PropertyChanged += OnGraphChanged;
        catalog.Products.CollectionChanged += OnProductsChanged;
        catalog.References.CollectionChanged += OnReferencesChanged;
        foreach (var p in catalog.Products) p.PropertyChanged += OnGraphChanged;
        foreach (var r in catalog.References) r.PropertyChanged += OnGraphChanged;
    }

    private void UnsubscribeFromCatalog(Catalog catalog)
    {
        catalog.PropertyChanged -= OnGraphChanged;
        catalog.Brand.PropertyChanged -= OnGraphChanged;
        catalog.Brand.PropertyChanged -= OnBrandChanged;
        catalog.Cover.PropertyChanged -= OnGraphChanged;
        catalog.Products.CollectionChanged -= OnProductsChanged;
        catalog.References.CollectionChanged -= OnReferencesChanged;
        foreach (var p in catalog.Products) p.PropertyChanged -= OnGraphChanged;
        foreach (var r in catalog.References) r.PropertyChanged -= OnGraphChanged;
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        ScheduleAutoSave();
        OnPropertyChanged(nameof(CalculatedPageCount));
    }

    private void OnProductsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null) foreach (Product p in e.NewItems) p.PropertyChanged += OnGraphChanged;
        if (e.OldItems is not null)
        {
            foreach (Product p in e.OldItems)
            {
                p.PropertyChanged -= OnGraphChanged;
                // Silinen ürünü manuel sayfa atamalarından da kaldır
                foreach (var page in Catalog.CustomPages) page.ProductIds.Remove(p.Id);
            }
        }
        IsDirty = true;
        ScheduleAutoSave();
        OnPropertyChanged(nameof(CalculatedPageCount));
        OnPropertyChanged(nameof(UnassignedProducts));
    }

    private void OnReferencesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null) foreach (Reference r in e.NewItems) r.PropertyChanged += OnGraphChanged;
        if (e.OldItems is not null) foreach (Reference r in e.OldItems) r.PropertyChanged -= OnGraphChanged;
        IsDirty = true;
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void NewCatalog()
    {
        if (!ConfirmDiscardChanges()) return;
        Catalog = NewCatalogWithProfile();   // kayıtlı marka profili otomatik uygulanır
        ActiveName = "Yeni Katalog";
        IsDirty = false;
        StatusMessage = "Yeni katalog — kayıtlı marka bilgileri uygulandı.";
    }

    [RelayCommand]
    private void OpenLatestPdf()
    {
        var latest = _libraryService.GetMostRecent();
        if (latest is null)
        {
            MessageBox.Show("Henüz oluşturulmuş bir PDF yok. Önce bir katalog hazırlayıp 'PDF Üret' ile oluşturun.",
                "PDF Bulunamadı", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(latest.PdfPath) { UseShellExecute = true });
            StatusMessage = $"Açıldı: {latest.BrandName} ({latest.CreatedAt:dd MMM yyyy HH:mm})";
        }
        catch (Exception ex) { ShowError("PDF açılamadı", ex.Message); }
    }

    [RelayCommand]
    private void GeneratePdf()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Catalog.Brand.Name))
            {
                ShowError("Marka adı gerekli",
                    "PDF üretmek için önce 'Marka Bilgileri' sekmesinden marka adını girmelisiniz.");
                return;
            }

            // PRD-03 fix: Featured ürünler + multi-grid layout (2x2, 3x3, 4x4) QuestPDF layout exception yaratıyor.
            // Kullanıcıya seçenekleri sun: layout=1x1 yap, featured'ları kapat, veya iptal.
            if (Catalog.Products.Any(p => p.IsFeatured) &&
                !string.Equals(Catalog.LayoutId, "1x1", StringComparison.OrdinalIgnoreCase) &&
                !Catalog.UseCustomPageLayouts)
            {
                var r = MessageBox.Show(
                    "Bazı ürünler 'Öne Çıkan' (Featured) olarak işaretli. " +
                    "Bu özellik şu an sadece '1 Ürün' düzeninde sorunsuz çalışır; " +
                    "çoklu ürün düzenlerinde PDF üretimi başarısız olabilir.\n\n" +
                    "EVET → Düzeni '1 Ürün'e geçir, devam et\n" +
                    "HAYIR → Tüm ürünlerin 'Öne Çıkan' işaretini kaldır, mevcut düzenle devam et\n" +
                    "İPTAL → Hiçbir şey yapma, geri dön (manuel düzeltebilirsin)",
                    "Düzen Uyumsuzluğu",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (r == MessageBoxResult.Cancel) return;
                if (r == MessageBoxResult.Yes) Catalog.LayoutId = "1x1";
                else { foreach (var p in Catalog.Products) p.IsFeatured = false; }
            }

            var defaultLabel = string.IsNullOrWhiteSpace(Catalog.LibraryLabel)
                ? $"{Catalog.Brand.Name} - {DateTime.Now:dd MMM yyyy}"
                : Catalog.LibraryLabel;

            var dialog = new NameInputDialog(
                "Katalog Adı",
                defaultLabel,
                "Bu isim sadece kütüphanede görünür, üretilen PDF'in içeriğine yazılmaz. Boş bırakırsanız marka adı kullanılır.")
            { Owner = ActiveWindow() };

            if (dialog.ShowDialog() != true) return;

            Catalog.LibraryLabel = dialog.EnteredName;

            var entry = _libraryService.Save(Catalog);
            ActiveName = string.IsNullOrWhiteSpace(entry.LibraryLabel) ? entry.BrandName : entry.LibraryLabel;
            IsDirty = false;
            TryDeleteAutoSave(); // iş artık kütüphanede güvende — eski oturum geri-yükleme sorusunu önle
            StatusMessage = $"PDF üretildi ve kütüphaneye eklendi: {entry.DisplayTitle}";
            Process.Start(new ProcessStartInfo(entry.PdfPath) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("PDF üretilemedi", ex.Message); }
    }

    [RelayCommand]
    private void OpenPdfFolder()
    {
        try
        {
            Directory.CreateDirectory(_libraryService.LibraryPath);
            Process.Start(new ProcessStartInfo(_libraryService.LibraryPath) { UseShellExecute = true });
            StatusMessage = $"Klasör açıldı: {_libraryService.LibraryPath}";
        }
        catch (Exception ex) { ShowError("Klasör açılamadı", ex.Message); }
    }

    /// <summary>
    /// Açık çalışma kataloğunun kullandığı tüm görsel yolları — orphan temizliği bunları ASLA silmez.
    /// Kullanıcı henüz kütüphaneye kaydetmemiş olsa bile yüklediği görseller korunur (hafıza bug fix).
    /// </summary>
    private System.Collections.Generic.IReadOnlyCollection<string> CurrentSessionImagePaths()
    {
        var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? p) { if (!string.IsNullOrWhiteSpace(p)) paths.Add(p!); }
        Add(Catalog.Brand.LogoPath);
        Add(Catalog.Cover.CustomCoverImagePath);
        foreach (var el in Catalog.Cover.Elements)
            if (el.Type == CoverElementType.Image) Add(el.Content);
        foreach (var p in Catalog.Products) Add(p.ImagePath);
        foreach (var r in Catalog.References) Add(r.LogoPath);
        Add(BrandProfileService.Load()?.LogoPath);   // kalıcı marka profili logosu da korunur
        return paths;
    }

    [RelayCommand]
    private void OpenLibrary()
    {
        // Library açılırken orphan görselleri temizle — AMA açık oturumun görsellerini ASLA silme (hafıza bug fix)
        try { var n = _libraryService.CleanupOrphanImages(CurrentSessionImagePaths()); if (n > 0) StatusMessage = $"{n} kullanılmayan görsel temizlendi."; }
        catch { /* sessiz */ }

        var window = new LibraryWindow(_libraryService) { Owner = ActiveWindow() };
        window.ShowDialog();
        if (window.LoadedCatalog is not null)
        {
            if (!ConfirmDiscardChanges()) return;
            Catalog = window.LoadedCatalog;
            ActiveName = window.LoadedEntryName ?? "Düzenlenen Katalog";
            IsDirty = false;
            StatusMessage = $"Düzenlemeye yüklendi: {ActiveName}";
        }
    }

    [RelayCommand]
    private void BrowseBrandLogo()
    {
        var path = ImagePicker.PickAndCrop(ActiveWindow(), "Marka logosu seç");
        if (path is not null) Catalog.Brand.LogoPath = path;
    }

    [RelayCommand]
    private void BrowseCoverImage()
    {
        // Mod kullanıcı tarafından UI'dan seçilmiş — picker aspect ratio'yu o mode'a göre kilitle
        var (aspect, title) = Catalog.Cover.CustomCoverImageMode switch
        {
            CoverImageMode.TopWithBrandBar       => (595.0 / 560.0, "Kapak görseli seç (üst alan, alt marka bandı eklenir)"),
            CoverImageMode.BackgroundWithOverlay => (595.0 / 842.0, "Kapak arka plan görseli seç (üstüne metin overlay gelecek)"),
            _                                    => (595.0 / 842.0, "Kapak görseli seç (tam sayfa)"),
        };
        var path = ImagePicker.PickAndCrop(ActiveWindow(), title, aspect);
        if (path is not null)
        {
            Catalog.Cover.CustomCoverImagePath = path;
            RefreshCoverPreview();
        }
    }

    [RelayCommand]
    private void ClearCoverImage()
    {
        Catalog.Cover.CustomCoverImagePath = string.Empty;
        RefreshCoverPreview();
    }

    [RelayCommand]
    private void RefreshPreview() => RefreshCoverPreview();

    [RelayCommand]
    private void OpenCoverDesigner()
    {
        var window = new CoverDesignerWindow(Catalog.Cover.Elements) { Owner = ActiveWindow() };
        var ok = window.ShowDialog();
        if (ok == true && window.Saved)
        {
            Catalog.Cover.Elements.Clear();
            foreach (var el in window.ResultElements)
                Catalog.Cover.Elements.Add(el);
            IsDirty = true;
            StatusMessage = $"Kapak tasarımı kaydedildi: {window.ResultElements.Count} element.";
            RefreshCoverPreview();
        }
    }

    [RelayCommand]
    private void ClearCoverDesign()
    {
        Catalog.Cover.Elements.Clear();
        IsDirty = true;
        StatusMessage = "Kapak tasarımı temizlendi — varsayılan tasarım kullanılır.";
        RefreshCoverPreview();
    }

    [RelayCommand]
    private void GenerateSingleProductPdf(Product? product)
    {
        if (product is null) return;
        try
        {
            var defaultLabel = string.IsNullOrWhiteSpace(product.Name) ? "Ürün Bilgi Formu" : product.Name;
            var dialog = new NameInputDialog(
                "Ürün PNG Adı",
                defaultLabel,
                "Bu isim sadece dosya adı için kullanılır. Çıktı PNG görseli olarak kaydedilir.")
            { Owner = ActiveWindow() };
            if (dialog.ShowDialog() != true) return;

            var pngPath = _libraryService.SaveSingleProduct(Catalog, product, dialog.EnteredName);
            StatusMessage = $"Tek ürün PNG üretildi: {Path.GetFileName(pngPath)}";
            Process.Start(new ProcessStartInfo(pngPath) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Tek ürün PNG üretilemedi", ex.Message); }
    }

    [RelayCommand]
    private void CreateStandaloneProductPdf()
    {
        try
        {
            var editor = new ProductEditDialog(new Product()) { Owner = ActiveWindow() };
            if (editor.ShowDialog() != true) return;
            var product = editor.Product;

            var defaultLabel = string.IsNullOrWhiteSpace(product.Name) ? "Ürün Bilgi Formu" : product.Name;
            var nameDialog = new NameInputDialog(
                "Tekli Ürün PNG Adı",
                defaultLabel,
                "Bu görsel kataloğa eklenmez, sadece PNG olarak kaydedilir.")
            { Owner = ActiveWindow() };
            if (nameDialog.ShowDialog() != true) return;

            var pngPath = _libraryService.SaveSingleProduct(Catalog, product, nameDialog.EnteredName);
            StatusMessage = $"Tekli ürün PNG üretildi: {Path.GetFileName(pngPath)}";
            Process.Start(new ProcessStartInfo(pngPath) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Tekli ürün PNG üretilemedi", ex.Message); }
    }

    [RelayCommand]
    private void AddProduct()
    {
        var dialog = new ProductEditDialog(new Product()) { Owner = ActiveWindow() };
        if (dialog.ShowDialog() == true)
        {
            Catalog.Products.Add(dialog.Product);
            StatusMessage = $"Ürün eklendi: {dialog.Product.Name}";
        }
    }

    [RelayCommand]
    private void ImportProductsFromExcel()
    {
        var dialog = new ExcelImportDialog { Owner = ActiveWindow() };
        if (dialog.ShowDialog() != true) return;

        int added = 0;
        foreach (var product in dialog.ImportedProducts)
        {
            Catalog.Products.Add(product);
            added++;
        }
        StatusMessage = added > 0
            ? $"{added} ürün Excel'den içe aktarıldı."
            : "Excel'den ürün eklenmedi.";
    }

    [RelayCommand]
    private void EditProduct(Product? product)
    {
        if (product is null) return;
        var dialog = new ProductEditDialog(product) { Owner = ActiveWindow() };
        if (dialog.ShowDialog() == true)
        {
            var idx = Catalog.Products.IndexOf(product);
            if (idx >= 0) Catalog.Products[idx] = dialog.Product;
            StatusMessage = $"Ürün güncellendi: {dialog.Product.Name}";
        }
    }

    [RelayCommand]
    private void DeleteProduct(Product? product)
    {
        if (product is null) return;
        if (MessageBox.Show($"\"{product.Name}\" ürünü silinsin mi?", "Ürünü Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Catalog.Products.Remove(product);
        StatusMessage = "Ürün silindi.";
    }

    [RelayCommand]
    private void MoveProductUp(Product? product)
    {
        if (product is null) return;
        var idx = Catalog.Products.IndexOf(product);
        if (idx > 0) Catalog.Products.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveProductDown(Product? product)
    {
        if (product is null) return;
        var idx = Catalog.Products.IndexOf(product);
        if (idx >= 0 && idx < Catalog.Products.Count - 1) Catalog.Products.Move(idx, idx + 1);
    }

    [RelayCommand]
    private void AddReference()
    {
        var dialog = new ReferenceEditDialog(new Reference()) { Owner = ActiveWindow() };
        if (dialog.ShowDialog() == true)
        {
            Catalog.References.Add(dialog.Reference);
            StatusMessage = $"Referans eklendi: {ReferenceLabel(dialog.Reference)}";
        }
    }

    [RelayCommand]
    private void EditReference(Reference? reference)
    {
        if (reference is null) return;
        var dialog = new ReferenceEditDialog(reference) { Owner = ActiveWindow() };
        if (dialog.ShowDialog() == true)
        {
            var idx = Catalog.References.IndexOf(reference);
            if (idx >= 0) Catalog.References[idx] = dialog.Reference;
            StatusMessage = $"Referans güncellendi: {ReferenceLabel(dialog.Reference)}";
        }
    }

    private static string ReferenceLabel(Reference reference) =>
        string.IsNullOrWhiteSpace(reference.Name) ? "logolu referans" : reference.Name;

    [RelayCommand]
    private void DeleteReference(Reference? reference)
    {
        if (reference is null) return;
        if (MessageBox.Show($"\"{ReferenceLabel(reference)}\" referansı silinsin mi?", "Referansı Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Catalog.References.Remove(reference);
        StatusMessage = "Referans silindi.";
    }

    public bool ConfirmDiscardChanges()
    {
        if (!IsDirty) return true;
        return MessageBox.Show(
            "Kaydedilmemiş değişiklikler var. PDF üretmeden devam ederseniz değişiklikler kaybolur. Devam edilsin mi?",
            "Kaydedilmemiş Değişiklikler", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static Window? ActiveWindow() => Application.Current?.MainWindow;
    private static void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
