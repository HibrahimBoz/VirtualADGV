using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using VirtualADGV.Data;

namespace VirtualADGV.WPF;

public partial class VirtualDataGrid
{
    private CancellationTokenSource? _scrollPrefetch;
    private double _prefetchOffset;
    private ScrollViewer? _thumbViewer;
    private ScrollBar? _thumbBar;
    private Track? _thumbTrack;
    private Thumb? _scrollThumb;
    private ToolTip? _thumbHint;
    private DispatcherTimer? _thumbDelay;
    private bool _thumbDragging;
    private double _thumbTarget;
    private double _thumbRequestedTarget = double.NaN;

    // Mini harita ve tutamaç aynı beklemeyi paylaşır; yeni hareket eski hedefi geçersiz kılar.
    internal void CancelPrefetchScroll()
    {
        var pending = _scrollPrefetch;
        _scrollPrefetch = null;
        pending?.Cancel();
    }

    private double ScrollRowHeight(ScrollViewer viewer)
        => viewer.CanContentScroll && VirtualizingPanel.GetScrollUnit(this) == ScrollUnit.Item ? 1 : RowHeightEstimate();

    internal async void PrefetchAndScroll(ScrollViewer viewer, double offset)
    {
        offset = Compat.Clamp(offset, 0, viewer.ScrollableHeight);
        if (_scrollPrefetch != null && _prefetchOffset == offset) return;
        CancelPrefetchScroll();
        var source = ItemsSource;
        // Bellek içi listede eski, senkron yol korunur. Pencereli kip kendi kaydırmasını yönetir.
        if (source is not IVirtualPrefetchList list || IsWindowedSource || Items.Count == 0)
        {
            viewer.ScrollToVerticalOffset(offset);
            return;
        }
        var cts = _scrollPrefetch = new CancellationTokenSource();
        _prefetchOffset = offset;
        double height = ScrollRowHeight(viewer);
        int start = (int)Compat.Clamp(Math.Floor(offset / height), 0, Items.Count - 1);
        int count = (int)Math.Min(Items.Count - start, 2 * Math.Max(1, Math.Ceiling(viewer.ViewportHeight / height)));
        try
        {
            try { await list.PrefetchAsync(start, count, cts.Token); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PrefetchAsync: {ex.Message}"); }
            // Kaynağın kendi hatası/iptali senkron yedeğe düşer; yeni hedefin iptal ettiği istek kaydırmaz.
            if (!cts.IsCancellationRequested && ReferenceEquals(source, ItemsSource)) viewer.ScrollToVerticalOffset(offset);
        }
        finally
        {
            if (ReferenceEquals(_scrollPrefetch, cts)) _scrollPrefetch = null;
            cts.Dispose();
        }
    }

    private void AttachThumbScroll(ScrollViewer viewer)
    {
        DetachThumbScroll();
        var bar = viewer.Template?.FindName("PART_VerticalScrollBar", viewer) as ScrollBar;
        bar?.ApplyTemplate();
        var track = bar == null ? null : VisualDescendants<Track>(bar).FirstOrDefault();
        if (track?.Thumb == null) return;
        _thumbViewer = viewer;
        _thumbBar = bar;
        _thumbTrack = track;
        _scrollThumb = track.Thumb;
        // Tema tutamaca MinHeight verirse Track tutamacın yerini kendi hesapladığı (çok satırda birkaç px) boyla ayırır, tutamaç
        // ise MinHeight'a büyür: sonra gelen sayfa düğmesi tutamacın alt kısmını örter ve ortasına basış PageDown olur
        // (ölçüldü: 1 M satırda 24 px tutamacın ortası RepeatButton'a düştü). Görünen boy Track hesabına sokulur.
        // MinHeight vermeyen temada dönüştürücü değeri aynen geçirir; görünüm değişmez.
        BindThumbViewport(bar!, track);
        // Olay tutamaçta işlenir: ScrollBar'ın sınıf işleyicisi her adımda içeriği kaydıramaz.
        _scrollThumb.DragStarted += OnScrollDragStarted;
        _scrollThumb.DragDelta += OnScrollDragDelta;
        _scrollThumb.DragCompleted += OnScrollDragCompleted;
        _thumbDelay = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
        _thumbDelay.Tick += OnThumbDelay;
        _thumbHint = new ToolTip { PlacementTarget = _scrollThumb, Placement = PlacementMode.Left, StaysOpen = true };
    }

    private void BindThumbViewport(ScrollBar bar, Track track)
    {
        var viewport = new MultiBinding { Converter = ThumbViewportConverter.Instance };
        viewport.Bindings.Add(new Binding(nameof(ScrollBar.ViewportSize)) { Source = bar });
        viewport.Bindings.Add(new Binding(nameof(ScrollBar.Minimum)) { Source = bar });
        viewport.Bindings.Add(new Binding(nameof(ScrollBar.Maximum)) { Source = bar });
        viewport.Bindings.Add(new Binding(nameof(ActualHeight)) { Source = track });
        viewport.Bindings.Add(new Binding(nameof(MinHeight)) { Source = track.Thumb });
        track.SetBinding(Track.ViewportSizeProperty, viewport);
    }

    private void CancelThumbDrag()
    {
        _thumbDelay?.Stop();
        _thumbDragging = false;
        if (_thumbHint != null) _thumbHint.IsOpen = false;
        CancelPrefetchScroll();
    }

    private void DetachThumbScroll()
    {
        CancelThumbDrag();
        if (_scrollThumb != null)
        {
            _scrollThumb.DragStarted -= OnScrollDragStarted;
            _scrollThumb.DragDelta -= OnScrollDragDelta;
            _scrollThumb.DragCompleted -= OnScrollDragCompleted;
        }
        // WPF, ScrollBar içindeki Track'in ViewportSize'ını kendisi bağlar; ClearBinding onu da siler (NaN = sabit boylu
        // tutamaç). Ayrılırken aynı bağlama geri kurulur.
        if (_thumbTrack != null && _thumbBar != null)
            _thumbTrack.SetBinding(Track.ViewportSizeProperty, new Binding(nameof(ScrollBar.ViewportSize)) { Source = _thumbBar });
        if (_thumbDelay != null) _thumbDelay.Tick -= OnThumbDelay;
        _thumbDelay = null;
        _thumbHint = null;
        _scrollThumb = null;
        _thumbTrack = null;
        _thumbBar = null;
        _thumbViewer = null;
    }

    private void OnScrollDragStarted(object sender, DragStartedEventArgs e)
    {
        if (IsWindowedSource || _thumbViewer == null) return;
        CancelPrefetchScroll();
        _thumbDragging = true;
        _thumbTarget = _thumbViewer.VerticalOffset;
        _thumbRequestedTarget = double.NaN;
        ShowThumbHint();
        e.Handled = true;
    }

    private void OnScrollDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_thumbDragging || _thumbViewer == null || _thumbTrack == null || _thumbBar == null) return;
        e.Handled = true;
        double delta = _thumbTrack.ValueFromDistance(e.HorizontalChange, e.VerticalChange);
        if (!Compat.IsFinite(delta) || delta == 0) return;
        CancelPrefetchScroll();
        _thumbRequestedTarget = double.NaN;
        _thumbTarget = Compat.Clamp(_thumbTarget + delta, _thumbBar.Minimum, _thumbBar.Maximum);
        _thumbBar.SetCurrentValue(RangeBase.ValueProperty, _thumbTarget);
        ShowThumbHint();
        _thumbDelay!.Stop();
        _thumbDelay.Start();
    }

    private void ShowThumbHint()
    {
        if (_thumbHint == null || _thumbViewer == null) return;
        long row = (long)Compat.Clamp(Math.Floor(_thumbTarget / ScrollRowHeight(_thumbViewer)), 0, Math.Max(0, Items.Count - 1)) + 1;
        _thumbHint.Content = string.Format(CultureInfo.CurrentCulture, Strings.MinimapRowFormat, row);
        _thumbHint.IsOpen = true;
    }

    private void OnThumbDelay(object? sender, EventArgs e)
    {
        _thumbDelay?.Stop();
        if (_thumbDragging) RequestThumbTarget();
    }

    private void RequestThumbTarget()
    {
        if (_thumbViewer == null || _thumbRequestedTarget == _thumbTarget) return;
        _thumbRequestedTarget = _thumbTarget;
        PrefetchAndScroll(_thumbViewer, _thumbTarget);
    }

    private void OnScrollDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_thumbDragging || _thumbViewer == null) return;
        e.Handled = true;
        _thumbDelay?.Stop();
        _thumbDragging = false;
        if (_thumbHint != null) _thumbHint.IsOpen = false;
        if (e.Canceled)
        {
            CancelPrefetchScroll();
            _thumbBar?.SetCurrentValue(RangeBase.ValueProperty, _thumbViewer.VerticalOffset);
        }
        else RequestThumbTarget();
    }

    // Track tutamaç boyu = uzunluk · v / (aralık + v). Tutamacın en az boyu (MinHeight) için gereken v'yi verir.
    private sealed class ThumbViewportConverter : IMultiValueConverter
    {
        internal static readonly ThumbViewportConverter Instance = new();
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 5 || values[0] is not double viewport) return DependencyProperty.UnsetValue;
            if (values[1] is not double min || values[2] is not double max || values[3] is not double length || values[4] is not double minThumb)
                return viewport;
            double range = max - min;
            if (!Compat.IsFinite(viewport) || !Compat.IsFinite(range) || range <= 0 || !Compat.IsFinite(minThumb) || minThumb <= 0 || length <= minThumb)
                return viewport;
            return Math.Max(viewport, minThumb * range / (length - minThumb));
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
