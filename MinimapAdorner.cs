using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VirtualADGV.WPF;

internal sealed class MinimapAdorner : Adorner, IDisposable
{
    private readonly VirtualDataGrid _grid;
    private readonly ScrollViewer _viewer;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<long> _selected = Array.Empty<long>();
    private long[] _markBuckets = Array.Empty<long>(), _selectionBuckets = Array.Empty<long>(), _searchBuckets = Array.Empty<long>();
    private bool _bucketsDirty = true, _dragging;
    private double _pendingRatio;
    private int _bucketCount, _totalRows;
    private Rect _viewport;
    internal Rect MapBounds { get; private set; }
    internal Rect ViewportBounds => _viewport;
    internal IReadOnlyList<long> MarkBuckets { get { EnsureBuckets(); return _markBuckets; } }
    internal IReadOnlyList<long> SelectionBuckets { get { EnsureBuckets(); return _selectionBuckets; } }
    internal string RowHint { get; private set; } = "";
    private static readonly Brush Surface = CrosshairAdorner.FrozenBrush(Colors.Gray);
    private static readonly Brush Gold = CrosshairAdorner.FrozenBrush(Colors.Gold);
    private static readonly Brush Accent = CrosshairAdorner.FrozenBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly Brush Search = CrosshairAdorner.FrozenBrush(Colors.DarkOrange);
    private static readonly Brush Text = CrosshairAdorner.FrozenBrush(Colors.Black);

    internal MinimapAdorner(VirtualDataGrid grid, UIElement presenter, ScrollViewer viewer) : base(presenter)
    {
        _grid = grid;
        _viewer = viewer;
        ClipToBounds = true;
        _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnDelay;
        RefreshGeometry();
    }
    internal void SetSelection(IReadOnlyList<long> ordinals)
    {
        _selected = ordinals;
        InvalidateBuckets();
    }
    internal void InvalidateBuckets() { _bucketsDirty = true; InvalidateVisual(); }
    internal void RefreshGeometry()
    {
        var size = AdornedElement.RenderSize;
        // Pencereli kipte (kaynak > int.MaxValue) DataGrid kaynağın yalnız bir penceresini görür; konumu 64 bit kaydırma çubuğu gösterir.
        bool shown = _grid.IsMinimapVisible && !_grid.IsWindowedSource && _viewer.ComputedVerticalScrollBarVisibility == Visibility.Visible &&
            size.Width > 8 && size.Height > 8 && _grid.Items.Count > 0;
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        Rect bounds = shown ? new Rect(Math.Max(4, size.Width - _grid.MinimapWidth - 4), 4,
            Math.Min(_grid.MinimapWidth, size.Width - 8), Math.Min(_grid.MinimapHeight, size.Height - 8)) : Rect.Empty;
        if (MapBounds != bounds)
        {
            MapBounds = bounds;
            _bucketsDirty = true;
            InvalidateVisual();
        }
        if (!shown) { CancelDrag(); return; }
        var viewport = CalculateViewport(_dragging ? TargetOffset(_pendingRatio) : _viewer.VerticalOffset);
        if (viewport != _viewport) { _viewport = viewport; InvalidateVisual(); }
    }
    private Rect CalculateViewport(double offset)
    {
        if (MapBounds.IsEmpty || _viewer.ExtentHeight <= 0) return Rect.Empty;
        double height = Math.Min(MapBounds.Height, Math.Max(6, _viewer.ViewportHeight / _viewer.ExtentHeight * MapBounds.Height));
        double top = Compat.Clamp(offset / _viewer.ExtentHeight * MapBounds.Height, 0, MapBounds.Height - height);
        return new Rect(MapBounds.X, MapBounds.Y + top, MapBounds.Width, height);
    }
    internal static long[] Bucketize(IReadOnlyList<long> ordinals, int total, int height)
    {
        var buckets = new long[height];
        Compat.Fill(buckets, -1L);
        if (total <= 0 || height == 0) return buckets;
        for (int i = 0; i < ordinals.Count; i++)
        {
            long ordinal = ordinals[i];
            if (ordinal < 0 || ordinal >= total) continue;
            // Kayan nokta yuvarlaması çok büyük toplamlarda y = height verebilir: sınırla.
            int y = Math.Min(height - 1, (int)(ordinal * (double)height / total));
            if (buckets[y] < 0 || ordinal < buckets[y]) buckets[y] = ordinal;
        }
        return buckets;
    }
    private void EnsureBuckets()
    {
        int height = MapBounds.IsEmpty ? 0 : Math.Max(1, (int)Math.Ceiling(MapBounds.Height));
        int total = _grid.Items.Count;
        if (!_bucketsDirty && height == _bucketCount && total == _totalRows) return;
        _bucketCount = height;
        _totalRows = total;
        _markBuckets = Bucketize(_grid.MinimapMarks, total, height);
        _selectionBuckets = Bucketize(_selected, total, height);
        _searchBuckets = Bucketize(_grid.MinimapSearchHits, total, height);
        _bucketsDirty = false;
    }
    protected override void OnRender(DrawingContext dc) => DrawMap(dc);
    // Test aynı çizim yolunu DrawingVisual üzerinde süreler.
    internal void DrawMap(DrawingContext dc)
    {
        if (Visibility != Visibility.Visible || MapBounds.IsEmpty) return;
        EnsureBuckets();
        dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        Brush text = Resource("ThemeText", Text);
        dc.PushOpacity(IsMouseOver || _dragging ? 0.85 : 0.35);
        dc.DrawRectangle(Resource("ThemeSurface", Resource("ThemeGridBg", Surface)), new Pen(text, 1), MapBounds);
        dc.Pop();
        // Üç katman ayrı şeritte (VS Code süslemeleri gibi): yoğun altın sarısı işaret (ör. %20 satır) seçimi ve aramayı örtmesin.
        DrawBuckets(dc, _markBuckets, Resource("ThemeHighlightBg", Gold), 0.04, 0.30);
        DrawBuckets(dc, _selectionBuckets, Resource("ThemeAccent", Accent), 0.36, 0.28, 2);
        DrawBuckets(dc, _searchBuckets, Resource("ThemeSearchHitBg", Search), 0.66, 0.30);
        if (!_viewport.IsEmpty)
        {
            dc.PushOpacity(0.2);
            dc.DrawRectangle(text, null, _viewport);
            dc.Pop();
            dc.DrawRectangle(null, new Pen(text, 1), _viewport);
        }
        if (_dragging)
        {
            var label = new FormattedText(RowHint, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, text, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double x = Math.Max(0, MapBounds.Left - label.Width - 10);
            double y = Math.Min(AdornedElement.RenderSize.Height - label.Height - 4, MapBounds.Bottom + 4);
            dc.DrawRectangle(Resource("ThemeSurface", Surface), null, new Rect(x, y, label.Width + 8, label.Height + 4));
            dc.DrawText(label, new Point(x + 4, y + 2));
        }
        dc.Pop();
    }
    private Brush Resource(string key, Brush fallback) => _grid.TryFindResource(key) as Brush ?? fallback;
    private void DrawBuckets(DrawingContext dc, long[] buckets, Brush brush, double left, double width, double thickness = 1)
    {
        double scale = MapBounds.Height / Math.Max(1, buckets.Length);
        for (int y = 0; y < buckets.Length; y++)
            if (buckets[y] >= 0)
                dc.DrawRectangle(brush, null, new Rect(MapBounds.X + MapBounds.Width * left,
                    MapBounds.Y + y * scale, MapBounds.Width * width, Math.Min(MapBounds.Height - y * scale, Math.Max(thickness, scale))));
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
        => Visibility == Visibility.Visible && MapBounds.Contains(parameters.HitPoint) ? new PointHitTestResult(this, parameters.HitPoint) : null;
    private double RatioAt(double y, bool snap)
    {
        if (MapBounds.IsEmpty || MapBounds.Height <= 0) return 0;   // 0'a bölme NaN üretip ScrollToVerticalOffset'e giderdi
        EnsureBuckets();
        double ratio = Compat.Clamp((y - MapBounds.Y) / MapBounds.Height, 0, 1);
        if (snap)
        {
            int bucket = Compat.Clamp((int)(ratio * _bucketCount), 0, Math.Max(0, _bucketCount - 1));
            // En yakın dolu kova; eşit uzaklıkta üstteki ilk sıra kazanır.
            for (int distance = 0; distance <= 2; distance++)
                foreach (int index in distance == 0 ? new[] { bucket } : new[] { bucket - distance, bucket + distance })
                    if (index >= 0 && index < _markBuckets.Length && _markBuckets[index] >= 0)
                        return _markBuckets[index] / (double)Math.Max(1, _grid.Items.Count);
        }
        return ratio;
    }
    internal double TargetOffset(double ratio) => Compat.Clamp(ratio * _viewer.ExtentHeight - _viewer.ViewportHeight / 2,
        0, Math.Max(0, _viewer.ExtentHeight - _viewer.ViewportHeight));
    internal void ClickAt(double y)
    {
        _grid.PrefetchAndScroll(_viewer, TargetOffset(RatioAt(y, true)));
    }
    internal void BeginDrag(double y)
    {
        _dragging = true;
        UpdateDrag(y, true);
    }
    internal void MoveDrag(double y) => UpdateDrag(y, false);
    private void UpdateDrag(double y, bool snap)
    {
        if (!_dragging || MapBounds.IsEmpty) return;
        _grid.CancelPrefetchScroll();
        _pendingRatio = RatioAt(y, snap);
        long row = Math.Min(Math.Max(0, _grid.Items.Count - 1), (long)(_pendingRatio * _grid.Items.Count)) + 1;
        RowHint = string.Format(CultureInfo.CurrentCulture, _grid.Strings.MinimapRowFormat, row);
        _timer.Stop();
        _timer.Start();
        RefreshGeometry();
        InvalidateVisual();
    }
    internal void EndDrag()
    {
        if (!_dragging) return;
        _timer.Stop();
        _grid.PrefetchAndScroll(_viewer, TargetOffset(_pendingRatio));
        _dragging = false;
        InvalidateVisual();
    }
    private void OnDelay(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_dragging) _grid.PrefetchAndScroll(_viewer, TargetOffset(_pendingRatio));
    }
    private void CancelDrag()
    {
        _timer.Stop();
        if (_dragging) _grid.CancelPrefetchScroll();
        _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!MapBounds.Contains(e.GetPosition(this))) return;
        CaptureMouse();
        BeginDrag(e.GetPosition(this).Y);
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) { MoveDrag(e.GetPosition(this).Y); e.Handled = true; }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        EndDrag();
        ReleaseMouseCapture();
        e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { CancelDrag(); InvalidateVisual(); }
    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); InvalidateVisual(); }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); InvalidateVisual(); }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        // Adorner, ScrollViewer'ın çocuğu değildir; tekerleği içerik yoluna ilet.
        _viewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            { RoutedEvent = Mouse.MouseWheelEvent, Source = _viewer });
        // İşlenmemiş bırakılır: dış kaplar (ör. IF alt grafik paneli) tekerleği görmeye devam eder. İki kat kaydırma
        // ölçülerek denendi ve oluşmadı; VirtualAdgvTests "tekerlek ... bir adim" kontrolü bunu kilitler.
        e.Handled = false;
    }
    public void Dispose() { CancelDrag(); _grid.CancelPrefetchScroll(); _timer.Tick -= OnDelay; }
}
