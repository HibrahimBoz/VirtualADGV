// Licensed under Ms-PL; see LICENSE.
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Zuby.ADGV
{
    public partial class AdvancedDataGridView
    {
        private MinimapControl? _minimap;
        private bool _showMinimap = true, _crosshairEnabled = true, _overlayDark;
        private int _minimapWidth = 60, _minimapHeight = 180;
        private Color _crosshairColor = Color.Empty;
        private Func<object, long?>? _ordinalResolver;
        private readonly HashSet<int> _crosshairRows = new HashSet<int>();
        private readonly HashSet<int> _crosshairColumns = new HashSet<int>();
        private bool _crosshairAllRows, _crosshairAllColumns, _resettingSelection;
        private long[] _marks = Array.Empty<long>(), _searchHits = Array.Empty<long>();

        [DefaultValue(true)] public bool IsMinimapVisible
        {
            get => _showMinimap;
            set { _showMinimap = value; LayoutOverlays(); }
        }
        [DefaultValue(60)] public int MinimapWidth
        {
            get => _minimapWidth;
            set { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _minimapWidth = value; LayoutOverlays(); }
        }
        [DefaultValue(180)] public int MinimapHeight
        {
            get => _minimapHeight;
            set { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); _minimapHeight = value; LayoutOverlays(); }
        }
        [DefaultValue(true)] public bool IsCrosshairEnabled
        {
            get => _crosshairEnabled;
            set { _crosshairEnabled = value; InvalidateVisibleArea(); }
        }
        /// <summary>Empty blends the selection color at low alpha over each cell, so it reads on any cell color. Alpha colors blend.</summary>
        public Color CrosshairColor
        {
            get => _crosshairColor.IsEmpty ? Color.FromArgb(48, DefaultCellStyle.SelectionBackColor.IsEmpty ? SystemColors.Highlight : DefaultCellStyle.SelectionBackColor) : _crosshairColor;
            set { _crosshairColor = value; InvalidateVisibleArea(); }
        }
        /// <summary>Resolves a bound item (or an unbound DataGridViewRow) to an absolute ordinal.</summary>
        [Browsable(false)] public Func<object, long?>? OrdinalResolver
        {
            get => _ordinalResolver;
            set { _ordinalResolver = value; RefreshOverlaySelection(); }
        }
        [Browsable(false)] public IReadOnlyList<long> MinimapSearchHits => Array.AsReadOnly(_searchHits);
        internal MinimapControl? Minimap => _minimap;
        internal bool OverlayDark => _overlayDark;
        internal Rectangle VisibleDataRectangle
        {
            get
            {
                int left = RowHeadersVisible ? RowHeadersWidth : 1;
                int top = ColumnHeadersVisible ? ColumnHeadersHeight : 1;
                return Rectangle.FromLTRB(left, top, Math.Max(left, ClientSize.Width - 1), Math.Max(top, ClientSize.Height - 1));
            }
        }

        public void SetMinimapMarks(IReadOnlyList<long>? ordinals)
        {
            _marks = CopyOrdinals(ordinals);
            if (_minimap != null) { _minimap.Model.Marks = _marks; _minimap.RefreshMap(); }
        }
        public void SetMinimapSearchHits(IReadOnlyList<long>? ordinals)
        {
            _searchHits = CopyOrdinals(ordinals);
            if (_minimap != null) { _minimap.Model.SearchHits = _searchHits; _minimap.RefreshMap(); }
        }
        private static long[] CopyOrdinals(IReadOnlyList<long>? input)
        {
            if (input == null) return Array.Empty<long>();
            var result = new long[input.Count];
            for (int i = 0; i < result.Length; i++) result[i] = input[i];
            Array.Sort(result);
            return result;
        }
        private void InitializeOverlays()
        {
            DoubleBuffered = true;
            _minimap = new MinimapControl(this);
            Controls.Add(_minimap);
        }
        internal void InvalidateVisibleArea()
        {
            if (!IsDisposed && IsHandleCreated) Invalidate(VisibleDataRectangle);
        }
        private void LayoutOverlays()
        {
            if (_minimap == null || IsDisposed) return;
            int rightInset = Controls.OfType<VScrollBar>().Any(s => s.Visible) ? SystemInformation.VerticalScrollBarWidth : 0;
            int top = (ColumnHeadersVisible ? ColumnHeadersHeight : 0) + 5;
            int width = Math.Min(_minimapWidth, Math.Max(1, ClientSize.Width - 10));
            int height = Math.Min(_minimapHeight, Math.Max(1, ClientSize.Height - top - SystemInformation.HorizontalScrollBarHeight - 5));
            _minimap.SetBounds(Math.Max(1, ClientSize.Width - rightInset - width - 5), top, width, height);
            _minimap.Visible = _showMinimap && TotalSourceRows > Math.Max(1, DisplayedRowCount(false)) && ClientSize.Height > top + 10;
            _minimap.BringToFront();
            _minimap.RefreshMap();
            LayoutSearchPanel();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutOverlays();
        }
        protected override void OnSelectionChanged(EventArgs e)
        {
            base.OnSelectionChanged(e);
            RefreshOverlaySelection();
        }
        protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
        {
            base.OnDataBindingComplete(e);
            if (e.ListChangedType == ListChangedType.Reset) ResetGridFeatures();
            LayoutOverlays();
        }
        protected override void OnSorted(EventArgs e)
        {
            base.OnSorted(e);
            ResetGridFeatures();
        }

        private void ResetGridFeatures()
        {
            CancelSearch();
            _minimap?.CancelDrag();
            ResetOverlaySelection();
            SetMinimapSearchHits(null);
            SetSearchStatus("");
        }
        private void ResetOverlaySelection()
        {
            if (_crosshairRows == null) return; // DataGridView constructor can raise events.
            _resettingSelection = true;
            try
            {
                ClearSelection();
                _isAllCellsSelected = false;
                _virtualSelectedRows.Clear();
                _virtualSelectedColumns.Clear();
                _virtualSelectedCells.Clear();
                _crosshairRows.Clear();
                _crosshairColumns.Clear();
                _crosshairAllRows = _crosshairAllColumns = false;
                if (_minimap != null) { _minimap.Model.Selection = Array.Empty<long>(); _minimap.AllRowsSelected = false; }
            }
            finally { _resettingSelection = false; }
            InvalidateVisibleArea();
        }
        private void RefreshOverlaySelection()
        {
            if (_crosshairRows == null || _resettingSelection || _settingSource || IsDisposed) return;
            _crosshairRows.Clear();
            _crosshairColumns.Clear();
            _crosshairAllRows = _crosshairAllColumns = false;
            // In virtual selection mode native selection is off; SelectedCells is not read (it can walk every row).
            if (!VirtualModeSelectionEnabled)
                foreach (DataGridViewCell cell in SelectedCells)
                {
                    _crosshairRows.Add(cell.RowIndex);
                    _crosshairColumns.Add(cell.ColumnIndex);
                }
            if (VirtualModeSelectionEnabled)
            {
                _crosshairAllRows = _isAllCellsSelected || _virtualSelectedColumns.Count != 0;
                _crosshairAllColumns = _isAllCellsSelected || _virtualSelectedRows.Count != 0;
                _crosshairRows.UnionWith(_virtualSelectedRows);
                _crosshairColumns.UnionWith(_virtualSelectedColumns);
                foreach (var pair in _virtualSelectedCells)
                {
                    _crosshairRows.Add(pair.Key);
                    _crosshairColumns.UnionWith(pair.Value);
                }
            }
            if (_minimap != null)
            {
                var ordinals = new List<long>();
                foreach (int index in _crosshairRows)
                {
                    if (index < 0 || index >= RowCount) continue;
                    long? ordinal = _ordinalResolver == null ? (_sourceList?.WindowStart ?? 0) + index :
                        _ordinalResolver(Rows[index].DataBoundItem ?? (object)Rows[index]);
                    if (ordinal >= 0 && ordinal < TotalSourceRows) ordinals.Add(ordinal.Value);
                }
                _minimap.Model.Selection = ordinals;
                _minimap.AllRowsSelected = _crosshairAllRows;
                _minimap.RefreshMap();
            }
            InvalidateVisibleArea();
        }
        private bool PaintCrosshair(DataGridViewCellPaintingEventArgs e)
        {
            if (!_crosshairEnabled || e.RowIndex < 0 || e.ColumnIndex < 0 ||
                (e.State & DataGridViewElementStates.Selected) != 0 ||
                !(_crosshairAllRows || _crosshairAllColumns || _crosshairRows.Contains(e.RowIndex) || _crosshairColumns.Contains(e.ColumnIndex))) return false;
            // CellBounds already accounts for frozen columns, horizontal scrolling and clipping.
            base.OnCellPainting(e);
            if (e.Handled) return true;
            if (e.CellStyle == null) return false;
            Color old = e.CellStyle.BackColor;
            Color tint = CrosshairColor;
            int alpha = tint.A;
            try
            {
                e.CellStyle.BackColor = Color.FromArgb((tint.R * alpha + old.R * (255 - alpha)) / 255,
                    (tint.G * alpha + old.G * (255 - alpha)) / 255, (tint.B * alpha + old.B * (255 - alpha)) / 255);
                e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
            }
            finally { e.CellStyle.BackColor = old; } // The style object can be shared by many cells.
            e.Handled = true;
            return true;
        }
        private void ApplyOverlayTheme(bool dark)
        {
            _overlayDark = dark;
            // 1.x SetTheme only themed the filter menus; cell and header colors stay the caller's.
            _minimap?.Invalidate();
            ThemeSearchPanel();
            InvalidateVisibleArea();
        }
        internal void ForwardMinimapWheel(MouseEventArgs e) => OnMouseWheel(e);
    }
}
