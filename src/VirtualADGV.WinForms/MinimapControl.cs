// Licensed under Ms-PL; see LICENSE. All ordinal, bucket and drag mathematics come from Core.
#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using VirtualADGV.Grid;

namespace Zuby.ADGV
{
    internal sealed class MinimapControl : Control
    {
        private readonly AdvancedDataGridView _grid;
        private readonly Timer _defer = new Timer { Interval = 100 };
        private readonly ToolTip _hint = new ToolTip();
        internal MinimapModel Model { get; } = new MinimapModel();
        internal bool AllRowsSelected { get; set; }
        internal Color SurfaceColor => _grid.OverlayDark ? Color.FromArgb(36, 49, 67) : Color.FromArgb(237, 242, 248);

        internal MinimapControl(AdvancedDataGridView grid)
        {
            _grid = grid;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            _defer.Tick += async (_, __) =>
            {
                _defer.Stop();
                if (Model.ConsumeDeferredScroll()) await _grid.NavigateMinimapAsync(Model.TargetRatio);
            };
        }
        internal void RefreshMap()
        {
            Model.SetGeometry(_grid.TotalSourceRows, 3, Math.Max(0, Height - 6));
            Invalidate();
        }
        internal void BeginDrag(double y)
        {
            RefreshMap();
            _grid.CancelNavigation();
            Model.BeginDrag(y);
            RestartTimer();
        }
        internal void MoveDrag(double y)
        {
            if (!Model.IsDragging) return;
            _grid.CancelNavigation();
            Model.MoveDrag(y);
            RestartTimer();
        }
        internal async System.Threading.Tasks.Task EndDragAsync()
        {
            _defer.Stop();
            if (Model.EndDrag()) await _grid.NavigateMinimapAsync(Model.TargetRatio);
            Invalidate();
        }
        internal void CancelDrag()
        {
            _defer.Stop();
            Model.CancelDrag();
            _grid.CancelNavigation();
        }
        private void RestartTimer()
        {
            _defer.Stop();
            _defer.Start();
            _hint.SetToolTip(this, Model.FormatRowHint(_grid.Strings.MinimapRowFormat, CultureInfo.CurrentCulture));
            Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { Capture = true; BeginDrag(e.Y); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Model.IsDragging) MoveDrag(e.Y);
        }
        protected override async void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            var completion = EndDragAsync();
            Capture = false;
            await completion;
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && Model.IsDragging) CancelDrag();
        }
        protected override void OnMouseWheel(MouseEventArgs e) => _grid.ForwardMinimapWheel(e);
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SurfaceColor);
            using (var border = new Pen(_grid.OverlayDark ? Color.FromArgb(109, 128, 152) : Color.FromArgb(117, 139, 163)))
                e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            int lane = Math.Max(1, (Width - 6) / 3);
            DrawBuckets(e.Graphics, Model.MarkBuckets, 3, lane, Color.Goldenrod);
            if (AllRowsSelected)
            {
                using (var selection = new SolidBrush(Color.DodgerBlue)) e.Graphics.FillRectangle(selection, 3 + lane, 3, lane, Math.Max(0, Height - 6));
            }
            else DrawBuckets(e.Graphics, Model.SelectionBuckets, 3 + lane, lane, Color.DodgerBlue);
            DrawBuckets(e.Graphics, Model.SearchBuckets, 3 + lane * 2, lane, Color.DarkOrange);
            var viewport = MinimapModel.CalculateViewport(_grid.FirstVisibleOrdinal, _grid.TotalSourceRows,
                Math.Max(1, _grid.DisplayedRowCount(false)), 4d / Math.Max(1, Height - 6));
            using (var pen = new Pen(_grid.OverlayDark ? Color.WhiteSmoke : Color.FromArgb(40, 67, 98), 1))
                e.Graphics.DrawRectangle(pen, 1, (float)(3 + viewport.TopRatio * Model.MapHeight), Width - 3,
                    (float)Math.Max(2, viewport.HeightRatio * Model.MapHeight));
            if (Model.IsDragging)
            {
                float y = (float)(3 + Model.TargetRatio * Model.MapHeight);
                using (var pen = new Pen(Color.DeepSkyBlue, 2)) e.Graphics.DrawLine(pen, 1, y, Width - 2, y);
            }
        }
        private static void DrawBuckets(Graphics graphics, System.Collections.Generic.IReadOnlyList<long> buckets, int x, int width, Color color)
        {
            using (var brush = new SolidBrush(color))
                for (int y = 0; y < buckets.Count; y++)
                    if (buckets[y] >= 0) graphics.FillRectangle(brush, x, 3 + y, width, 2);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { CancelDrag(); _defer.Dispose(); _hint.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
