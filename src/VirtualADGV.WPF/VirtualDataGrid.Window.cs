using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using VirtualADGV.Data;

namespace VirtualADGV.WPF;

// Pencereli kip: kaynak int.MaxValue satırı aşınca DataGrid kaynağın yalnız bir penceresini görür
// (WPF satırları int ile sayar). Konumu tablonun sağındaki 64 bit kaydırma çubuğu gösterir; kullanıcı pencerenin
// kenarına yaklaşınca pencere kayar, görünen satır ve seçim görsel sıraya (ordinal) göre korunur.
// Kaynak eşiği aşmıyorsa bu dosyadaki hiçbir şey devreye girmez.
public partial class VirtualDataGrid
{
    private const int MaxRestoredCells = 100_000;
    private VirtualSourceList? _windowList;
    private WindowScrollAdorner? _windowScroll;
    private AdornerLayer? _windowLayer;
    private ScrollContentPresenter? _windowPresenter;
    private ScrollViewer? _windowViewer;
    // Pencere kayması sürüyor (Reset → satır kapları kuruluyor → kaydırma + seçim geri konuyor). Bu sürede kullanıcı
    // seçimi sayılmaz, kenar denetimi yeniden kaydırmaz.
    private bool _windowSyncing;
    private bool _restorePending;
    // Pencere kayınca DataGrid seçimi sıfırlar. Seçim görsel sıra + sütun olarak burada yaşar ve pencereye girdikçe
    // geri konur; pencere dışında kalan seçim kaybolmaz. Kullanıcı yeni seçim yapınca bırakılır.
    private List<KeyValuePair<long, DataGridColumn?>>? _savedSelection;
    private bool _restoringSelection;
    private long _anchorOrdinal = -1;
    private CancellationTokenSource? _seekCts;
    private object? _savedVerticalScrollBar;

    internal ScrollBar? WindowScrollBarForTest => _windowScroll?.Bar;
    internal bool WindowRestorePendingForTest => _restorePending;

    /// <summary>Kaynak pencereli kipte mi (yalnız <see cref="VirtualSourceList"/> kaynağında olabilir)?</summary>
    public bool IsWindowedSource => _windowList?.IsWindowed == true;

    /// <summary>Görünen ilk satırın kaynağın tamamındaki görsel sırası; satır yoksa −1.</summary>
    public long FirstVisibleOrdinal
    {
        get
        {
            int index = FirstVisibleWindowIndex();
            return index < 0 ? -1 : (_windowList?.WindowStart ?? 0) + index;
        }
    }

    /// <inheritdoc/>
    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        CancelThumbDrag();
        base.OnItemsSourceChanged(oldValue, newValue);
        if (_windowList != null) _windowList.WindowMoving -= OnWindowMoving;
        _windowList = newValue as VirtualSourceList;
        if (_windowList != null) _windowList.WindowMoving += OnWindowMoving;
        // Eski kaynağa ait bekleyen konum araması ve seçim yeni kaynağa uygulanmaz.
        _seekCts?.Cancel();
        _savedSelection = null;
        _anchorOrdinal = -1;
        UpdateWindowScrollAdorner();
        Minimap?.RefreshGeometry();
    }

    /// <summary>
    /// Görsel sıraya kaydırır (64 bit). Sıra pencere dışındaysa pencere önce oraya taşınır.
    /// Pencereli olmayan kaynakta <see cref="DataGrid.ScrollIntoView(object)"/> ile aynıdır.
    /// </summary>
    public void ScrollToOrdinal(long ordinal)
    {
        VerifyAccess();
        var list = _windowList;
        if (list == null || !list.IsWindowed)
        {
            if (ordinal >= 0 && ordinal < Items.Count) ScrollIntoView(Items[(int)ordinal]);
            return;
        }
        if (ordinal < 0 || ordinal >= list.TotalCount) return;
        int index = list.WindowIndexOf(ordinal);
        int margin = WindowMargin(list);
        if (!_restorePending && index >= margin && index < list.Count - margin) { ScrollToWindowIndex(index); return; }
        MoveWindowAround(ordinal);
    }

    // Kenar payı: pencerenin %10'u, ama küçük pencerede görünen satırlar payların arasına sığsın (kayma döngüsü olmaz).
    private int WindowMargin(VirtualSourceList list)
        => Math.Max(1, Math.Min(list.WindowSize / 10, Math.Max(0, (list.Count - VisibleRowCount()) / 3)));

    private void AttachWindowScroll(AdornerLayer layer, ScrollContentPresenter presenter, ScrollViewer viewer)
    {
        _windowLayer = layer;
        _windowPresenter = presenter;
        _windowViewer = viewer;
        UpdateWindowScrollAdorner();
    }

    // Katman yalnız pencereli kipte eklenir: pencereli olmayan tabloda görsel ağaç 1.1.0 ile aynı kalır.
    private void UpdateWindowScrollAdorner()
    {
        if (_windowLayer == null || _windowPresenter == null) return;
        if (IsWindowedSource && _windowScroll == null)
        {
            // Tablonun kendi çubuğu yalnız pencereyi gösterir. Görünür tutulur (yerini ayırır, içerik altına girmez) ve
            // 64 bit çubuk tam üstüne oturur. Kip kapanınca uygulamanın kendi ayarı geri konur.
            _savedVerticalScrollBar = ReadLocalValue(VerticalScrollBarVisibilityProperty);
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            _windowViewer?.ApplyTemplate();
            var native = _windowViewer?.Template?.FindName("PART_VerticalScrollBar", _windowViewer) as ScrollBar;
            UIElement host = native ?? (UIElement)_windowPresenter;
            var layer = AdornerLayer.GetAdornerLayer(host) ?? _windowLayer;
            _windowScroll = new WindowScrollAdorner(this, host, overlay: native != null);
            _windowLayer = layer;
            layer.Add(_windowScroll);
        }
        else if (!IsWindowedSource && _windowScroll != null)
        {
            _windowLayer.Remove(_windowScroll);
            _windowScroll = null;
            RestoreVerticalScrollBar();
        }
        _windowScroll?.Refresh();
    }

    private void RestoreVerticalScrollBar()
    {
        if (_savedVerticalScrollBar == null) return;
        if (_savedVerticalScrollBar == DependencyProperty.UnsetValue) ClearValue(VerticalScrollBarVisibilityProperty);
        else SetValue(VerticalScrollBarVisibilityProperty, _savedVerticalScrollBar);
        _savedVerticalScrollBar = null;
    }

    private void DetachWindowScroll()
    {
        if (_windowScroll != null) { _windowLayer?.Remove(_windowScroll); RestoreVerticalScrollBar(); }
        _windowScroll = null;
        _windowLayer = null;
        _windowPresenter = null;
        _windowViewer = null;
        _seekCts?.Cancel();
    }

    // Kullanıcının kendi seçim değişikliği saklanan seçimi bırakır; pencere kayması ve geri koyma bırakmaz.
    private void OnWindowSelectionChanged()
    {
        if (!_windowSyncing && !_restoringSelection && IsWindowedSource) _savedSelection = null;
    }

    private void OnWindowScroll()
    {
        var list = _windowList;
        if (list == null || !list.IsWindowed) return;
        int index = FirstVisibleWindowIndex();
        if (index >= 0) _windowScroll?.SetPosition(list.WindowStart + index, VisibleRowCount());
        if (_windowSyncing || index < 0) return;
        int margin = WindowMargin(list);
        bool nearTop = index < margin && list.WindowStart > 0;
        bool nearBottom = index + VisibleRowCount() > list.Count - margin && list.WindowStart + list.Count < list.TotalCount;
        if (nearTop || nearBottom) MoveWindowAround(list.WindowStart + index);
    }

    private void MoveWindowAround(long anchor)
    {
        var list = _windowList;
        if (list == null) return;
        _anchorOrdinal = anchor;
        // Pencere değişirse OnWindowMoving kayma sonrası işi kuyruğa koyar (dışarıdan MoveWindow çağrısında da).
        if (!list.MoveWindow(anchor - list.WindowSize / 2) && !_restorePending)
        {
            _anchorOrdinal = -1;
            int index = list.WindowIndexOf(anchor);
            if (index >= 0) ScrollToWindowIndex(index);
        }
    }

    private void OnWindowMoving(object? sender, EventArgs e)
    {
        var list = _windowList;
        if (list == null) return;
        if (!_restorePending)
        {
            // Ekrandaki seçim + önceki kaymalardan kalan, şimdiki pencerede görünmeyen seçim saklanır.
            var saved = new List<KeyValuePair<long, DataGridColumn?>>();
            if (_savedSelection != null)
                foreach (var entry in _savedSelection)
                    if (list.WindowIndexOf(entry.Key) < 0 && saved.Count < MaxRestoredCells) saved.Add(entry);
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                foreach (object item in SelectedItems)
                    if (item is IVirtualRow row && saved.Count < MaxRestoredCells) saved.Add(new KeyValuePair<long, DataGridColumn?>(row.Ordinal, null));
            }
            else
            {
                foreach (var cell in SelectedCells)
                    if (cell.Item is IVirtualRow row && saved.Count < MaxRestoredCells) saved.Add(new KeyValuePair<long, DataGridColumn?>(row.Ordinal, cell.Column));
            }
            _savedSelection = saved;
            if (_anchorOrdinal < 0) _anchorOrdinal = FirstVisibleOrdinal;
        }
        // Geri koyma beklerken ikinci kayma: saklanan seçim henüz ekrana konmadı, olduğu gibi korunur (süzülmez).
        _windowSyncing = true;
        if (_restorePending) return;
        _restorePending = true;
        // Reset sonrası satır kapları yeniden kurulur; kaydırma ve seçim o kurulumdan sonra, tek seferde yapılır.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            try
            {
                var current = _windowList;
                if (current == null) return;
                int index = _anchorOrdinal >= 0 ? current.WindowIndexOf(_anchorOrdinal) : -1;
                if (index >= 0) ScrollToWindowIndex(index);
                RestoreSelection(current);
            }
            finally
            {
                _anchorOrdinal = -1;
                _restorePending = false;
                _windowSyncing = false;
                OnWindowScroll();
            }
        }));
    }

    private void RestoreSelection(VirtualSourceList list)
    {
        var saved = _savedSelection;
        if (saved == null || saved.Count == 0) return;
        _restoringSelection = true;
        try
        {
            foreach (var entry in saved)
            {
                int index = list.WindowIndexOf(entry.Key);
                if (index < 0) continue;
                object? item = Items[index];
                if (item == null) continue;
                if (entry.Value == null) SelectedItems.Add(item);
                else if (Columns.Contains(entry.Value)) SelectedCells.Add(new DataGridCellInfo(item, entry.Value));
            }
        }
        finally { _restoringSelection = false; }
    }

    // Görünen ilk satırın penceredeki indeksi: gerçekleşmiş satır kaplarından okunur (ScrollUnit Item ya da Pixel).
    private int FirstVisibleWindowIndex()
    {
        var presenter = OverlayPresenter;
        if (presenter == null || Items.Count == 0) return -1;
        int best = -1;
        double bestTop = double.MaxValue;
        foreach (var row in VisualDescendants<DataGridRow>(presenter))
        {
            if (!row.IsVisible || row.ActualHeight <= 0) continue;
            Point top = row.TranslatePoint(new Point(0, 0), presenter);
            if (top.Y + row.ActualHeight <= 0 || top.Y >= presenter.ActualHeight) continue;
            if (top.Y < bestTop)
            {
                int index = ItemContainerGenerator.IndexFromContainer(row);
                if (index >= 0) { bestTop = top.Y; best = index; }
            }
        }
        return best;
    }

    private int VisibleRowCount()
    {
        double height = RowHeightEstimate();
        var presenter = OverlayPresenter;
        return presenter == null || height <= 0 ? 1 : Math.Max(1, (int)(presenter.ActualHeight / height));
    }

    private double RowHeightEstimate()
    {
        if (OverlayPresenter != null)
            foreach (var row in VisualDescendants<DataGridRow>(OverlayPresenter))
                if (row.ActualHeight > 0) return row.ActualHeight;
        return !double.IsNaN(RowHeight) && RowHeight > 0 ? RowHeight : Math.Max(MinRowHeight, 20);
    }

    private void ScrollToWindowIndex(int index)
    {
        var viewer = OverlayScrollViewer;
        if (viewer == null || index < 0 || index >= Items.Count) return;
        if (VirtualizingPanel.GetScrollUnit(this) == ScrollUnit.Item && viewer.CanContentScroll)
            viewer.ScrollToVerticalOffset(index);
        else
            viewer.ScrollToVerticalOffset(index * RowHeightEstimate());
        UpdateLayout();
    }

    // 64 bit kaydırma çubuğundan gelen kullanıcı hareketi.
    internal async void OnWindowScrollBar(double value)
    {
        var list = _windowList;
        if (list == null || !list.IsWindowed) return;
        long last = Math.Max(0, list.TotalCount - 1);
        long target = (long)Compat.Clamp(value, 0, last);
        if (!list.Source.SupportsExactOrdinals)
        {
            // Uzak kaynak konumu örneklemeyle bulur; yeni hareket eskisini iptal eder.
            _seekCts?.Cancel();
            var cts = _seekCts = new CancellationTokenSource();
            double fraction = last == 0 ? 0 : (double)target / last;
            try { target = await list.Source.SeekAsync(fraction, cts.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SeekAsync: {ex.Message}"); return; }
            if (cts.IsCancellationRequested || !ReferenceEquals(_windowList, list)) return;
            target = Compat.Clamp(target, 0, last);
        }
        ScrollToOrdinal(target);
    }
}

/// <summary>Pencereli kipte tablonun sağında duran 64 bit kaydırma çubuğu.</summary>
internal sealed class WindowScrollAdorner : Adorner
{
    private readonly VirtualDataGrid _grid;
    private readonly ScrollBar _bar;
    private readonly VisualCollection _children;
    private bool _updating;

    private readonly bool _overlay;

    internal WindowScrollAdorner(VirtualDataGrid grid, UIElement host, bool overlay) : base(host)
    {
        _grid = grid;
        _overlay = overlay;
        _bar = new ScrollBar { Orientation = Orientation.Vertical, Minimum = 0, SmallChange = 1 };
        _bar.Scroll += (_, e) => { if (!_updating) _grid.OnWindowScrollBar(e.NewValue); };
        _children = new VisualCollection(this) { _bar };
        Visibility = Visibility.Collapsed;
    }

    internal ScrollBar Bar => _bar;

    internal void Refresh()
    {
        bool shown = _grid.IsWindowedSource;
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        if (shown) InvalidateArrange();
    }

    internal void SetPosition(long firstVisible, int visibleRows)
    {
        if (_grid.ItemsSource is not VirtualSourceList list) return;
        _updating = true;
        try
        {
            // double 2^53'e kadar tamsayıyı kesin taşır: 20 milyar satır sorunsuz.
            _bar.Maximum = Math.Max(0, list.TotalCount - visibleRows);
            _bar.ViewportSize = visibleRows;
            _bar.LargeChange = Math.Max(1, visibleRows);
            _bar.Value = Math.Min(firstVisible, _bar.Maximum);
        }
        finally { _updating = false; }
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size MeasureOverride(Size constraint)
    {
        _bar.Measure(AdornedElement.RenderSize);
        return AdornedElement.RenderSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = AdornedElement.RenderSize;
        // Tablonun kendi çubuğunun üstündeyse onu tamamen örter; değilse içeriğin sağ kenarında durur.
        double width = _overlay ? size.Width : SystemParameters.VerticalScrollBarWidth;
        _bar.Arrange(new Rect(Math.Max(0, size.Width - width), 0, width, size.Height));
        return finalSize;
    }
}
