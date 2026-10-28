using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace VirtualADGV.WPF;

internal sealed class CrosshairAdorner : Adorner
{
    private readonly VirtualDataGrid _grid;
    private HashSet<object> _rows = new();
    private HashSet<DataGridColumn> _columns = new();
    private bool _columnsOnly;
    internal IReadOnlyList<Rect> RowStrips { get; private set; } = Array.Empty<Rect>();
    internal IReadOnlyList<Rect> ColumnStrips { get; private set; } = Array.Empty<Rect>();
    private static readonly Brush DefaultBrush = FrozenBrush(Color.FromArgb(0x26, 0x25, 0x63, 0xEB));

    internal CrosshairAdorner(VirtualDataGrid grid, UIElement presenter) : base(presenter)
    {
        _grid = grid;
        IsHitTestVisible = false;
        ClipToBounds = true;
    }
    internal static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
    internal void SetSelection(HashSet<object> rows, HashSet<DataGridColumn> columns, bool columnsOnly)
    {
        _rows = rows;
        _columns = columns;
        _columnsOnly = columnsOnly;
        RefreshGeometry();
    }
    internal void RefreshGeometry()
    {
        try { RefreshGeometryCore(); }
        // Şablon kurulurken ya da sekme değişirken başlık ile içerik ortak ataya henüz bağlı olmayabilir:
        // TranslatePoint InvalidOperationException atar. Bir sonraki yerleşimde yeniden denenir.
        catch (InvalidOperationException) { }
    }
    private void RefreshGeometryCore()
    {
        var rows = new List<Rect>();
        var columns = new List<Rect>();
        var bounds = new Rect(AdornedElement.RenderSize);
        if (_grid.IsCrosshairEnabled && bounds.Width > 0 && bounds.Height > 0)
        {
            if (!_columnsOnly && _rows.Count > 0)
                foreach (var row in VirtualDataGrid.VisualDescendants<DataGridRow>(_grid))
                    if (_rows.Contains(row.Item) && row.IsVisible)
                    {
                        var point = row.TranslatePoint(new Point(), AdornedElement);
                        var rect = new Rect(0, point.Y, bounds.Width, row.ActualHeight);
                        rect.Intersect(bounds);
                        if (!rect.IsEmpty && rect.Height > 0) rows.Add(rect);
                    }
            if (_columns.Count > 0 && _grid.SelectionUnit != DataGridSelectionUnit.FullRow &&
                !_grid.Columns.Where(c => c.Visibility == Visibility.Visible).All(_columns.Contains))
            {
                var headers = VirtualDataGrid.VisualDescendants<DataGridColumnHeadersPresenter>(_grid).FirstOrDefault();
                if (headers != null)
                {
                    var visibleHeaders = VirtualDataGrid.VisualDescendants<DataGridColumnHeader>(headers)
                        .Where(h => h.Column != null && h.IsVisible).ToArray();
                    double frozenRight = visibleHeaders.Where(h => h.Column.IsFrozen)
                        .Select(h => h.TranslatePoint(new Point(), AdornedElement).X + h.ActualWidth).DefaultIfEmpty(0).Max();
                    foreach (var header in visibleHeaders)
                        if (_columns.Contains(header.Column))
                        {
                            double x = header.TranslatePoint(new Point(), AdornedElement).X;
                            double left = header.Column.IsFrozen ? x : Math.Max(x, frozenRight);
                            var rect = new Rect(left, 0, Math.Max(0, x + header.ActualWidth - left), bounds.Height);
                            rect.Intersect(bounds);
                            if (!rect.IsEmpty && rect.Width > 0) columns.Add(rect);
                        }
                }
            }
        }
        if (!RowStrips.SequenceEqual(rows) || !ColumnStrips.SequenceEqual(columns))
        {
            RowStrips = rows;
            ColumnStrips = columns;
            InvalidateVisual();
        }
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        var brush = _grid.CrosshairBrush ?? _grid.TryFindResource("ThemeCrosshairBg") as Brush ?? DefaultBrush;
        foreach (var rect in RowStrips) dc.DrawRectangle(brush, null, rect);
        foreach (var rect in ColumnStrips) dc.DrawRectangle(brush, null, rect);
        dc.Pop();
    }
}
