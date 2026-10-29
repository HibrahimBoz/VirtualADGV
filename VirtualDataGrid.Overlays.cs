using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace VirtualADGV.WPF;

public partial class VirtualDataGrid
{
    private static readonly string[] ThemeKeys = { "ThemeCrosshairBg", "ThemeText", "ThemeSurface", "ThemeGridBg", "ThemeHighlightBg", "ThemeAccent", "ThemeSearchHitBg" };
    private static readonly DependencyProperty[] ThemeProperties = ThemeKeys.Select(key => DependencyProperty.Register(
        key + "Watch", typeof(Brush), typeof(VirtualDataGrid), new PropertyMetadata(null, ThemeChanged))).ToArray();
    private void WatchOverlayTheme()
    {
        // Kaynak kapsamı katmanın değil tablonun kapsamıdır.
        for (int i = 0; i < ThemeKeys.Length; i++) SetResourceReference(ThemeProperties[i], ThemeKeys[i]);
    }
    private static void ThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (VirtualDataGrid)d;
        grid.Crosshair?.InvalidateVisual();
        grid.Minimap?.InvalidateVisual();
    }
    /// <summary>Çapraz vurgunun açık olmasını belirler.</summary>
    public static readonly DependencyProperty IsCrosshairEnabledProperty = DependencyProperty.Register(
        nameof(IsCrosshairEnabled), typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(true, OverlayOptionChanged));
    /// <summary>Çapraz vurgu fırçası; null ise tema kaynağı kullanılır.</summary>
    public static readonly DependencyProperty CrosshairBrushProperty = DependencyProperty.Register(
        nameof(CrosshairBrush), typeof(Brush), typeof(VirtualDataGrid), new PropertyMetadata(null, OverlayOptionChanged));
    /// <summary>Mini haritanın görünürlüğünü belirler.</summary>
    public static readonly DependencyProperty IsMinimapVisibleProperty = DependencyProperty.Register(
        nameof(IsMinimapVisible), typeof(bool), typeof(VirtualDataGrid), new PropertyMetadata(true, OverlayOptionChanged));
    /// <summary>Mini haritanın genişliği.</summary>
    public static readonly DependencyProperty MinimapWidthProperty = DependencyProperty.Register(
        nameof(MinimapWidth), typeof(double), typeof(VirtualDataGrid), new PropertyMetadata(60.0, OverlayOptionChanged), ValidDimension);
    /// <summary>Mini haritanın en büyük yüksekliği.</summary>
    public static readonly DependencyProperty MinimapHeightProperty = DependencyProperty.Register(
        nameof(MinimapHeight), typeof(double), typeof(VirtualDataGrid), new PropertyMetadata(180.0, OverlayOptionChanged), ValidDimension);
    /// <summary>Seçili hücrelerin satır ve sütunlarını soluk boyar.</summary>
    public bool IsCrosshairEnabled { get => (bool)GetValue(IsCrosshairEnabledProperty); set => SetValue(IsCrosshairEnabledProperty, value); }
    /// <summary>Çapraz vurgu için isteğe bağlı fırça.</summary>
    public Brush? CrosshairBrush { get => (Brush?)GetValue(CrosshairBrushProperty); set => SetValue(CrosshairBrushProperty, value); }
    /// <summary>Dikey kaydırma varsa mini haritayı gösterir.</summary>
    public bool IsMinimapVisible { get => (bool)GetValue(IsMinimapVisibleProperty); set => SetValue(IsMinimapVisibleProperty, value); }
    /// <summary>Mini harita genişliği, cihazdan bağımsız piksel.</summary>
    public double MinimapWidth { get => (double)GetValue(MinimapWidthProperty); set => SetValue(MinimapWidthProperty, value); }
    /// <summary>Mini harita yükseklik sınırı, cihazdan bağımsız piksel.</summary>
    public double MinimapHeight { get => (double)GetValue(MinimapHeightProperty); set => SetValue(MinimapHeightProperty, value); }

    private Func<object, long?>? _ordinalResolver;
    /// <summary>Öğeyi sıfır tabanlı görsel sıraya çevirir; sanal kaynak bunu sağlamalıdır.</summary>
    public Func<object, long?>? OrdinalResolver
    {
        get => _ordinalResolver;
        set { _ordinalResolver = value; QueueSelection(); }
    }

    private long[] _marks = Array.Empty<long>(), _searchHits = Array.Empty<long>();
    internal IReadOnlyList<long> MinimapMarks => _marks;
    internal IReadOnlyList<long> MinimapSearchHits => _searchHits;
    private Dictionary<object, int>? _ordinalCache;
    private DispatcherOperation? _selectionOperation;
    private AdornerLayer? _overlayLayer;
    internal ScrollViewer? OverlayScrollViewer { get; private set; }
    internal ScrollContentPresenter? OverlayPresenter { get; private set; }
    internal CrosshairAdorner? Crosshair { get; private set; }
    internal MinimapAdorner? Minimap { get; private set; }
    private bool _selectionCleared;

    /// <summary>Altın sarısı işaretlerin sıfır tabanlı görsel sıralarını kopyalar ve sıralar.</summary>
    public void SetMinimapMarks(IReadOnlyList<long>? ordinals)
    {
        VerifyAccess();
        _marks = SortedCopy(ordinals);
        Minimap?.InvalidateBuckets();
    }
    /// <summary>Arama eşleşmelerinin sıfır tabanlı görsel sıralarını kopyalar ve sıralar.</summary>
    public void SetMinimapSearchHits(IReadOnlyList<long>? ordinals)
    {
        VerifyAccess();
        _searchHits = SortedCopy(ordinals);
        Minimap?.InvalidateBuckets();
    }
    private static long[] SortedCopy(IReadOnlyList<long>? source)
    {
        if (source == null || source.Count == 0) return Array.Empty<long>();
        var copy = new long[source.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
        Array.Sort(copy);
        return copy;
    }
    private static bool ValidDimension(object value) => value is double d && Compat.IsFinite(d) && d > 0;
    private static void OverlayOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (VirtualDataGrid)d;
        grid.Crosshair?.RefreshGeometry();
        grid.Crosshair?.InvalidateVisual();
        grid.Minimap?.RefreshGeometry();
    }
    private void OnOverlayLoaded(object sender, RoutedEventArgs e) => AttachOverlays();
    private void OnOverlayUnloaded(object sender, RoutedEventArgs e)
    {
        DetachOverlays();
        _searchNavigation?.Abort();
        _searchNavigation = null;
        _ordinalCache = null;
    }
    private void AttachOverlays()
    {
        if (_overlayLayer != null) return;
        // Açık arama kutusunun kendi ScrollViewer'ı tablo kaydırıcısı değildir.
        var viewer = VisualDescendants<ScrollViewer>(this).FirstOrDefault(v => v.TemplatedParent == this);
        viewer ??= VisualDescendants<ScrollViewer>(this).FirstOrDefault(v =>
            VisualDescendants<ItemsPresenter>(v).Any(p => p.TemplatedParent == this));
        if (viewer == null) return;
        viewer.ApplyTemplate();
        AttachThumbScroll(viewer);
        var presenter = VisualDescendants<ScrollContentPresenter>(viewer).FirstOrDefault();
        if (presenter == null) return;
        var layer = AdornerLayer.GetAdornerLayer(presenter);
        if (layer == null) return;
        OverlayScrollViewer = viewer;
        OverlayPresenter = presenter;
        _overlayLayer = layer;
        Crosshair = new CrosshairAdorner(this, presenter);
        Minimap = new MinimapAdorner(this, presenter, viewer);
        layer.Add(Crosshair);
        layer.Add(Minimap);
        AttachWindowScroll(layer, presenter, viewer);
        viewer.ScrollChanged += OnOverlayScroll;
        LayoutUpdated += OnOverlayLayout;
        QueueSelection();
    }
    private void DetachOverlays()
    {
        DetachThumbScroll();
        _selectionOperation?.Abort();
        _selectionOperation = null;
        LayoutUpdated -= OnOverlayLayout;
        if (OverlayScrollViewer != null) OverlayScrollViewer.ScrollChanged -= OnOverlayScroll;
        if (Crosshair != null) _overlayLayer?.Remove(Crosshair);
        if (Minimap != null) { Minimap.Dispose(); _overlayLayer?.Remove(Minimap); }
        DetachWindowScroll();
        Crosshair = null;
        Minimap = null;
        _overlayLayer = null;
        OverlayScrollViewer = null;
        OverlayPresenter = null;
    }
    private void OnOverlayScroll(object sender, ScrollChangedEventArgs e)
    {
        Crosshair?.RefreshGeometry();
        Minimap?.RefreshGeometry();
        OnWindowScroll();
    }
    private bool _overlayLayoutQueued;
    private void OnOverlayLayout(object? sender, EventArgs e)
    {
        // LayoutUpdated pencere genelinde sık tetiklenir; görsel ağaç taraması kare başına en çok bir kez yapılır.
        if (_overlayLayoutQueued) return;
        _overlayLayoutQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _overlayLayoutQueued = false;
            // Yalnız gerçekleşmiş geometri karşılaştırılır; aynı yerleşim tekrar çizilmez.
            Crosshair?.RefreshGeometry();
            Minimap?.RefreshGeometry();
        }));
    }
    /// <inheritdoc/>
    protected override void OnSelectedCellsChanged(SelectedCellsChangedEventArgs e)
    {
        base.OnSelectedCellsChanged(e);
        OnWindowSelectionChanged();
        _selectionCleared = false;
        QueueSelection();
    }
    /// <inheritdoc/>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        _ordinalCache = null;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            CancelThumbDrag();
            // Sanal satır eşitliği eski sırayı yeni veriyle eşitleyebilir.
            _selectionCleared = true;
            _selectionOperation?.Abort();
            _selectionOperation = null;
            Crosshair?.SetSelection(new(), new(), false);
            Minimap?.SetSelection(Array.Empty<long>());
        }
        else QueueSelection();
        ClearSearchResults();
        Minimap?.InvalidateBuckets();
    }
    private void QueueSelection()
    {
        if (Crosshair == null || _selectionOperation != null) return;
        _selectionOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _selectionOperation = null;
            if (_selectionCleared || Crosshair == null) return;
            bool columnsOnly = SelectedCells.Count > 50_000;
            var rows = new HashSet<object>();
            var columns = new HashSet<DataGridColumn>();
            foreach (var cell in SelectedCells)
            {
                columns.Add(cell.Column);
                if (!columnsOnly) rows.Add(cell.Item);
            }
            Crosshair.SetSelection(rows, columns, columnsOnly);
            if (columnsOnly)
            {
                // Hücre eşiği satır şeridini sınırlar; mini haritada seçili satırlar kalır.
                foreach (var cell in SelectedCells) rows.Add(cell.Item);
            }
            var ordinals = new List<long>();
            if (rows.Count > 0 && (_ordinalResolver != null || Items.Count <= 200_000))
            {
                if (_ordinalResolver == null && _ordinalCache == null)
                {
                    _ordinalCache = new Dictionary<object, int>();
                    int i = 0;
                    foreach (object item in Items) { if (item != null) _ordinalCache.TryAdd(item, i); i++; }
                }
                foreach (var row in rows)
                {
                    long? ordinal = _ordinalResolver != null ? _ordinalResolver(row) :
                        _ordinalCache!.TryGetValue(row, out int index) ? index : null;
                    if (ordinal >= 0 && ordinal < Items.Count) ordinals.Add(ordinal.Value);
                }
            }
            Minimap?.SetSelection(ordinals);
        }));
    }

    internal static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            else foreach (var descendant in VisualDescendants<T>(child)) yield return descendant;
        }
    }
}
