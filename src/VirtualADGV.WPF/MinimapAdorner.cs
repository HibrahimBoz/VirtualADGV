using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VirtualADGV.Grid;

namespace VirtualADGV.WPF;

internal sealed class MinimapAdorner : Adorner, IDisposable
{
    private readonly VirtualDataGrid _grid;
    private readonly ScrollViewer _viewer;
    private readonly DispatcherTimer _timer;
    private readonly MinimapModel _model = new();
    private Rect _viewport;
    internal Rect MapBounds { get; private set; }
    internal Rect ViewportBounds => _viewport;
    internal IReadOnlyList<long> MarkBuckets { get { EnsureBuckets(); return _model.MarkBuckets; } }
    internal IReadOnlyList<long> SelectionBuckets { get { EnsureBuckets(); return _model.SelectionBuckets; } }
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
        _model.Selection = ordinals;
        InvalidateBuckets();
    }
    internal void InvalidateBuckets() { _model.InvalidateBuckets(); InvalidateVisual(); }
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
            _model.InvalidateBuckets();
            InvalidateVisual();
        }
        if (!shown) { CancelDrag(); return; }
        var viewport = CalculateViewport(_model.IsDragging ? TargetOffset(_model.TargetRatio) : _viewer.VerticalOffset);
        if (viewport != _viewport) { _viewport = viewport; InvalidateVisual(); }
    }
    private Rect CalculateViewport(double offset)
    {
        if (MapBounds.IsEmpty || _viewer.ExtentHeight <= 0) return Rect.Empty;
        var viewport = MinimapModel.CalculateViewport(offset, _viewer.ExtentHeight, _viewer.ViewportHeight, 6 / MapBounds.Height);
        return new Rect(MapBounds.X, MapBounds.Y + viewport.TopRatio * MapBounds.Height,
            MapBounds.Width, viewport.HeightRatio * MapBounds.Height);
    }
    internal static long[] Bucketize(IReadOnlyList<long> ordinals, int total, int height)
        => MinimapModel.Bucketize(ordinals, total, height);
    private void EnsureBuckets()
    {
        _model.SetGeometry(_grid.Items.Count, MapBounds.IsEmpty ? 0 : MapBounds.Y, MapBounds.IsEmpty ? 0 : MapBounds.Height);
        if (!ReferenceEquals(_model.Marks, _grid.MinimapMarks)) _model.Marks = _grid.MinimapMarks;
        if (!ReferenceEquals(_model.SearchHits, _grid.MinimapSearchHits)) _model.SearchHits = _grid.MinimapSearchHits;
    }
    protected override void OnRender(DrawingContext dc) => DrawMap(dc);
    // Test aynı çizim yolunu DrawingVisual üzerinde süreler.
    internal void DrawMap(DrawingContext dc)
    {
        if (Visibility != Visibility.Visible || MapBounds.IsEmpty) return;
        EnsureBuckets();
        dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        Brush text = Resource("ThemeText", Text);
        dc.PushOpacity(IsMouseOver || _model.IsDragging ? 0.85 : 0.35);
        dc.DrawRectangle(Resource("ThemeSurface", Resource("ThemeGridBg", Surface)), new Pen(text, 1), MapBounds);
        dc.Pop();
        // Üç katman ayrı şeritte (VS Code süslemeleri gibi): yoğun altın sarısı işaret (ör. %20 satır) seçimi ve aramayı örtmesin.
        DrawBuckets(dc, _model.MarkBuckets, Resource("ThemeHighlightBg", Gold), 0.04, 0.30);
        DrawBuckets(dc, _model.SelectionBuckets, Resource("ThemeAccent", Accent), 0.36, 0.28, 2);
        DrawBuckets(dc, _model.SearchBuckets, Resource("ThemeSearchHitBg", Search), 0.66, 0.30);
        if (!_viewport.IsEmpty)
        {
            dc.PushOpacity(0.2);
            dc.DrawRectangle(text, null, _viewport);
            dc.Pop();
            dc.DrawRectangle(null, new Pen(text, 1), _viewport);
        }
        if (_model.IsDragging)
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
    private void DrawBuckets(DrawingContext dc, IReadOnlyList<long> buckets, Brush brush, double left, double width, double thickness = 1)
    {
        double scale = MapBounds.Height / Math.Max(1, buckets.Count);
        for (int y = 0; y < buckets.Count; y++)
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
        return _model.RatioAt(y, snap);
    }
    internal double TargetOffset(double ratio) => MinimapModel.TargetOffset(ratio, _viewer.ExtentHeight, _viewer.ViewportHeight);
    internal void ClickAt(double y)
    {
        _grid.PrefetchAndScroll(_viewer, TargetOffset(RatioAt(y, true)));
    }
    internal void BeginDrag(double y)
    {
        EnsureBuckets();
        _model.BeginDrag(y);
        UpdateDrag();
    }
    internal void MoveDrag(double y)
    {
        EnsureBuckets();
        _model.MoveDrag(y);
        UpdateDrag();
    }
    private void UpdateDrag()
    {
        if (!_model.IsDragging || MapBounds.IsEmpty) return;
        _grid.CancelPrefetchScroll();
        RowHint = _model.FormatRowHint(_grid.Strings.MinimapRowFormat, CultureInfo.CurrentCulture);
        _timer.Stop();
        if (_model.NeedsDeferredScroll) _timer.Start();
        RefreshGeometry();
        InvalidateVisual();
    }
    internal void EndDrag()
    {
        if (!_model.IsDragging) return;
        _timer.Stop();
        _grid.PrefetchAndScroll(_viewer, TargetOffset(_model.TargetRatio));
        _model.EndDrag();
        InvalidateVisual();
    }
    private void OnDelay(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_model.ConsumeDeferredScroll()) _grid.PrefetchAndScroll(_viewer, TargetOffset(_model.TargetRatio));
    }
    private void CancelDrag()
    {
        _timer.Stop();
        if (_model.IsDragging) _grid.CancelPrefetchScroll();
        _model.CancelDrag();
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
        if (_model.IsDragging) { MoveDrag(e.GetPosition(this).Y); e.Handled = true; }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_model.IsDragging) return;
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
