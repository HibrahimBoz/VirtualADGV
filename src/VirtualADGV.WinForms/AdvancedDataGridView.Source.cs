// Licensed under Ms-PL; see LICENSE. Data and page-cache logic lives in VirtualADGV.Core.
#nullable enable
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualADGV.Data;

namespace Zuby.ADGV
{
    public partial class AdvancedDataGridView
    {
        // WinForms net8 x64, one shared text column, real Form (see tests --memory):
        // 10M: 201351512 managed bytes / 378720256 private bytes, 959.336 ms setup;
        // 50M: 805325232 / 1347067904 bytes, 3333.282 ms. VirtualMode still allocates
        // O(RowCount) row/state arrays. Cap the default at 1M; rebases use 100K rows.
        public const long DefaultWindowThreshold = 1_000_000;
        public const int DefaultWindowSize = 100_000;

        private VirtualSourceList? _sourceList;
        private bool _settingSource, _movingWindow;
        private bool _savedVirtualMode, _savedAllowAdd, _savedReadOnly, _savedAutoGenerate;
        private DataGridViewAutoSizeColumnsMode _savedColumnSize;
        private DataGridViewAutoSizeRowsMode _savedRowSize;
        private int _sourceVersion;
        private CancellationTokenSource? _navigationCts;
        private Task _navigationTask = Task.CompletedTask;
        private long _navigationTarget = -1;

        [Browsable(false)] public bool IsVirtualSource => _sourceList != null;
        [Browsable(false)] public bool IsWindowedSource => _sourceList?.IsWindowed == true;
        [Browsable(false)] public long FirstVisibleOrdinal => (_sourceList?.WindowStart ?? 0) + Math.Max(0, FirstDisplayedScrollingRowIndex);
        internal long TotalSourceRows => Math.Max(0, _sourceList?.TotalCount ?? (RowCount - (NewRowIndex >= 0 ? 1 : 0)));
        internal VirtualSourceList? SourceList => _sourceList;
        internal int PageInvalidationCount { get; private set; }
        internal int NavigationInstallCount { get; private set; }

        /// <summary>
        /// Binds a caller-owned source. Null detaches it and restores normal grid settings.
        /// Columns are installed before RowCount. Paint never waits on source I/O.
        /// Options are copied; a null options argument uses WinForms memory limits.
        /// </summary>
        public void SetVirtualSource(IVirtualTableSource? source, VirtualSourceListOptions? options = null)
        {
            if (InvokeRequired) throw new InvalidOperationException("SetVirtualSource must run on the UI thread.");
            VirtualSourceList? next = null;
            if (source != null)
            {
                var effective = new VirtualSourceListOptions
                {
                    PageSize = options?.PageSize ?? 2_000,
                    MaxCachedPages = options?.MaxCachedPages ?? 64,
                    SyncWait = TimeSpan.Zero, // A paint callback must never wait on a provider.
                    WindowThreshold = Math.Min(int.MaxValue, options?.WindowThreshold ?? DefaultWindowThreshold),
                    WindowSize = options?.WindowSize ?? DefaultWindowSize
                };
                next = new VirtualSourceList(source, effective);
            }
            bool hadSource = _sourceList != null;
            if (!hadSource && source != null)
            {
                _savedVirtualMode = VirtualMode;
                _savedAllowAdd = AllowUserToAddRows;
                _savedReadOnly = ReadOnly;
                _savedAutoGenerate = AutoGenerateColumns;
                _savedColumnSize = AutoSizeColumnsMode;
                _savedRowSize = AutoSizeRowsMode;
            }
            DetachSource();
            if (!hadSource && source == null) return;
            _settingSource = true;
            SuspendLayout();
            try
            {
                DataSource = null;
                AllowUserToAddRows = false;
                RowCount = 0;
                Columns.Clear();
                ResetGridFeatures();
                _sourceList = next;
                if (next != null)
                {
                    VirtualMode = true;
                    ReadOnly = true;
                    AutoGenerateColumns = false;
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                    AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                    foreach (var column in source!.Columns)
                        Columns.Add(new DataGridViewTextBoxColumn
                        {
                            Name = column.Name, DataPropertyName = column.Name, HeaderText = column.Name,
                            ValueType = column.Type, Width = 150, SortMode = DataGridViewColumnSortMode.Programmatic
                        });
                    next.PageLoaded += SourcePageLoaded;
                    next.CollectionChanged += SourceReset;
                    RowCount = ColumnCount == 0 ? 0 : next.Count;
                }
                else
                {
                    VirtualMode = _savedVirtualMode;
                    ReadOnly = _savedReadOnly;
                    AutoGenerateColumns = _savedAutoGenerate;
                    AutoSizeColumnsMode = _savedColumnSize;
                    AutoSizeRowsMode = _savedRowSize;
                    AllowUserToAddRows = _savedAllowAdd;
                }
            }
            finally { _settingSource = false; ResumeLayout(); }
            ClearSelection();
            RefreshOverlaySelection();
            LayoutOverlays();
            InvalidateVisibleArea();
        }

        private void DetachSource()
        {
            _sourceVersion++;
            CancelNavigation();
            CancelSearch();
            if (_sourceList == null) return;
            _sourceList.PageLoaded -= SourcePageLoaded;
            _sourceList.CollectionChanged -= SourceReset;
            _sourceList.Dispose(); // Does not dispose the caller's source.
            _sourceList = null;
        }

        protected override void OnCellValueNeeded(DataGridViewCellValueEventArgs e)
        {
            var list = _sourceList;
            if (list == null) { base.OnCellValueNeeded(e); return; }
            if (e.RowIndex < 0 || e.RowIndex >= list.Count || e.ColumnIndex < 0 || e.ColumnIndex >= ColumnCount) return;
            IVirtualRow row = list.GetRow(list.WindowStart + e.RowIndex);
            e.Value = row.IsLoaded ? row[Columns[e.ColumnIndex].DataPropertyName] : null;
        }

        private void SourcePageLoaded(object? sender, VirtualPageLoadedEventArgs e)
        {
            // Always post: even a synchronous provider must not reenter CellValueNeeded.
            PostToGrid(() =>
            {
                if (!ReferenceEquals(sender, _sourceList) || _settingSource || RowCount == 0) return;
                long first = FirstVisibleOrdinal;
                int visible = DisplayedRowCount(true);
                if (e.Start >= first + visible || e.Start + e.Count <= first) return;
                PageInvalidationCount++;
                Invalidate(VisibleDataRectangle); // One rectangle; no per-row invalidations.
            });
        }

        private void SourceReset(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_movingWindow) return;
            PostToGrid(() =>
            {
                if (!ReferenceEquals(sender, _sourceList) || _sourceList == null) return;
                _sourceVersion++;
                CancelNavigation();
                ResetGridFeatures();
                _movingWindow = true;
                try
                {
                    _sourceList.MoveWindow(_sourceList.WindowStart);
                    RowCount = ColumnCount == 0 ? 0 : _sourceList.Count;
                }
                finally { _movingWindow = false; }
                LayoutOverlays();
                InvalidateVisibleArea();
            });
        }

        private void PostToGrid(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed && !Disposing) action(); })); }
            catch (InvalidOperationException) { /* Handle destroyed while posting. */ }
        }

        /// <summary>Positions an absolute ordinal at the top, rebasing the window if necessary.</summary>
        public void ScrollToOrdinal(long ordinal)
        {
            if (TotalSourceRows == 0 || RowCount == 0) return;
            ordinal = Math.Max(0, Math.Min(TotalSourceRows - 1, ordinal));
            var list = _sourceList;
            _movingWindow = true;
            try
            {
                if (list?.IsWindowed == true)
                {
                    int index = list.WindowIndexOf(ordinal);
                    int margin = Math.Max(1, list.Count / 5);
                    if (index < margin || index >= list.Count - margin)
                    {
                        if (list.MoveWindow(ordinal - list.WindowSize / 2))
                        {
                            ResetOverlaySelection();
                            if (RowCount != list.Count) RowCount = list.Count;
                        }
                    }
                }
                int target = list == null ? (int)ordinal : list.WindowIndexOf(ordinal);
                // A shared row has no Frozen property; its state comes from the collection.
                if (target >= 0 && target < RowCount && (Rows.GetRowState(target) & DataGridViewElementStates.Frozen) == 0)
                    FirstDisplayedScrollingRowIndex = target;
            }
            finally { _movingWindow = false; }
            _minimap?.RefreshMap();
            InvalidateVisibleArea();
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            // While the thumb is dragged the mouse stays put: rebasing on every ThumbTrack would recenter the
            // window under the same thumb position again and again (runaway scroll). Rebase when it ends.
            if (!_settingSource && !_movingWindow && _sourceList?.IsWindowed == true && e.ScrollOrientation == ScrollOrientation.VerticalScroll
                && e.Type != ScrollEventType.ThumbTrack)
                ScrollToOrdinal(FirstVisibleOrdinal);
            _minimap?.RefreshMap();
        }

        internal void CancelNavigation()
        {
            // Not disposed here: a running prefetch/seek may still register on its token.
            _navigationCts?.Cancel();
            _navigationCts = null;
            _navigationTarget = -1;
        }

        internal Task NavigateMinimapAsync(double ratio)
        {
            long target = (long)VirtualADGV.Grid.MinimapModel.TargetOffset(ratio, TotalSourceRows, Math.Max(1, DisplayedRowCount(false)));
            if (target == _navigationTarget) return _navigationTask; // Timer and release share one installation.
            CancelNavigation();
            _navigationTarget = target;
            _navigationCts = new CancellationTokenSource();
            return _navigationTask = NavigateAsync(target, ratio, _navigationCts.Token);
        }

        private async Task NavigateAsync(long target, double? ratio, CancellationToken token)
        {
            var list = _sourceList;
            int version = _sourceVersion;
            try
            {
                int visible = Math.Max(1, DisplayedRowCount(true));
                if (list != null)
                {
                    if (ratio.HasValue && !list.Source.SupportsExactOrdinals)
                        target = Math.Max(0, await list.Source.SeekAsync(ratio.Value, token) - visible / 2);
                    await list.PrefetchAsync(target, visible, token);
                }
                else if (DataSource is IVirtualPrefetchList prefetch)
                    await prefetch.PrefetchAsync((int)target, visible, token);
                token.ThrowIfCancellationRequested();
                if (IsDisposed || version != _sourceVersion || !ReferenceEquals(list, _sourceList)) return;
                NavigationInstallCount++;
                ScrollToOrdinal(target);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetSearchStatus(string.Format(Strings.SearchErrorFormat, ex.Message)); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachSource();
                DetachSearchToolBar();
                _minimap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
