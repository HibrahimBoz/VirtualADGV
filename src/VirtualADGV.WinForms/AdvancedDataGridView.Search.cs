// Licensed under Ms-PL; see LICENSE.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VirtualADGV.Data;
using VirtualADGV.Grid;

namespace Zuby.ADGV
{
    public sealed class SearchEventArgs : EventArgs
    {
        public string? SearchText { get; set; }
        public string? ColumnName { get; set; }
        public bool IsCaseSensitive { get; set; }
        public bool SearchNext { get; set; }
        public bool Backward { get; set; }
    }

    public partial class AdvancedDataGridView
    {
        private VirtualGridTexts? _texts;
        private CancellationTokenSource? _searchCts;
        private FlowLayoutPanel? _searchPanel;
        private TextBox? _searchBox;
        private ComboBox? _searchColumn;
        private CheckBox? _searchCase;
        private Label? _searchStatusLabel;
        private AdvancedDataGridViewSearchToolBar? _searchToolBar;
        private string _searchStatus = "", _lastSearchText = "";
        private string? _lastSearchColumn;
        private bool _lastSearchCase;
        private long _lastSearchOrdinal = -1;

        /// <summary>A subscriber owns the search. Without one, the grid searches Core rows or the source.</summary>
        public event EventHandler<SearchEventArgs>? SearchRequested;
        public VirtualGridTexts Strings { get => _texts ?? (_texts = CreateGridTexts()); set => _texts = value ?? throw new ArgumentNullException(nameof(value)); }
        public string SearchStatus => _searchStatus;
        public bool IsSearchVisible
        {
            get => _searchPanel?.Visible == true || _searchToolBar?.Visible == true;
            set
            {
                if (value) OpenSearch();
                else
                {
                    if (_searchPanel != null) _searchPanel.Visible = false;
                    if (_searchToolBar != null) _searchToolBar.Visible = false;
                    CancelSearch();
                    SetMinimapSearchHits(null);
                    SetSearchStatus("");
                }
            }
        }

        private static VirtualGridTexts CreateGridTexts()
        {
            var texts = VirtualGridTexts.CreateTurkish();
            foreach (var property in typeof(VirtualGridTexts).GetProperties())
                if (Translations.TryGetValue("ADGV" + property.Name, out string? value)) property.SetValue(texts, value, null);
            return texts;
        }

        /// <summary>In-memory matching of displayed cell text. Source mode never enumerates Rows.</summary>
        public IReadOnlyList<long> FindAll(string text, string? columnName = null, bool caseSensitive = false)
        {
            var search = CreateRowSearch(columnName, caseSensitive);
            if (search == null) return Array.Empty<long>();
            return search.FindAll(text).Select(i => (long)i).ToArray();
        }
        public int FindNext(string text, int startIndex, string? columnName = null, bool caseSensitive = false)
            => CreateRowSearch(columnName, caseSensitive)?.FindNext(text, startIndex) ?? -1;

        private RowSearch? CreateRowSearch(string? columnName, bool caseSensitive)
        {
            if (IsVirtualSource) return null;
            if (VirtualMode || TotalSourceRows > RowSearch.InMemoryRowLimit)
            {
                SetSearchStatus(Strings.SearchLimitExceeded);
                return null;
            }
            var selected = Columns.Cast<DataGridViewColumn>().Where(c => c.Visible &&
                (string.IsNullOrEmpty(columnName) || c.Name == columnName || c.DataPropertyName == columnName)).ToArray();
            // Bound mode reads the list item, not Rows[i]: indexing Rows unshares every row (one object per row).
            System.Collections.IList? bound = null;
            if (DataSource != null && BindingContext != null)
                try { bound = (BindingContext[DataSource, DataMember] as CurrencyManager)?.List; } catch (ArgumentException) { }
            var properties = bound == null ? null : ListBindingHelper.GetListItemProperties(bound);
            var columns = new List<RowSearchColumn>();
            foreach (var c in selected)
            {
                var descriptor = string.IsNullOrEmpty(c.DataPropertyName) ? null : properties?.Find(c.DataPropertyName, true);
                if (bound != null && descriptor != null)
                    columns.Add(new RowSearchColumn(row => descriptor.GetValue(bound[(int)row]),
                        string.IsNullOrEmpty(c.DefaultCellStyle.Format) ? DefaultCellStyle.Format : c.DefaultCellStyle.Format));
                else
                {
                    int index = c.Index;
                    columns.Add(new RowSearchColumn(row =>
                        Convert.ToString(Rows[(int)row].Cells[index].FormattedValue, CultureInfo.CurrentCulture) ?? ""));
                }
            }
            if (columns.Count == 0) return null;
            return new RowSearch((int)TotalSourceRows, i => i, columns.ToArray(), CultureInfo.CurrentCulture, caseSensitive);
        }

        /// <summary>Searches, publishes minimap hits and navigates. Next/previous do not wrap.</summary>
        public async Task<long?> SearchAsync(string text, string? columnName = null, bool caseSensitive = false,
            bool searchNext = false, bool backward = false)
        {
            var args = new SearchEventArgs { SearchText = text, ColumnName = columnName,
                IsCaseSensitive = caseSensitive, SearchNext = searchNext, Backward = backward };
            if (SearchRequested != null) { SearchRequested(this, args); return null; }
            CancelSearch(false);
            CancelNavigation();
            var cts = _searchCts = new CancellationTokenSource();
            var token = cts.Token;
            var list = _sourceList;
            int version = _sourceVersion;
            bool same = text == _lastSearchText && columnName == _lastSearchColumn && caseSensitive == _lastSearchCase;
            long start = searchNext && same && _lastSearchOrdinal >= 0 ? _lastSearchOrdinal + (backward ? -1 : 1) :
                backward ? TotalSourceRows - 1 : 0;
            _lastSearchText = text;
            _lastSearchColumn = columnName;
            _lastSearchCase = caseSensitive;
            try
            {
                if (string.IsNullOrEmpty(text)) { SetMinimapSearchHits(null); SetSearchStatus(""); return null; }
                if (start < 0 || start >= TotalSourceRows) { SetSearchStatus(Strings.SearchEndReached); return null; }
                IReadOnlyList<long> hits;
                if (list != null)
                {
                    SetSearchStatus(Strings.SearchLoading);
                    hits = await list.Source.FindAsync(new VirtualSearchRequest(text,
                        string.IsNullOrEmpty(columnName) ? null : columnName, caseSensitive, backward), start, 10_000, token);
                }
                else
                {
                    var search = CreateRowSearch(columnName, caseSensitive);
                    if (search == null) return null;
                    hits = search.FindAll(text).Select(i => (long)i).ToArray();
                }
                if (token.IsCancellationRequested || version != _sourceVersion || IsDisposed) return null;
                var valid = hits.Where(i => i >= 0 && i < TotalSourceRows).Distinct().OrderBy(i => i).ToArray();
                SetMinimapSearchHits(valid);
                long? match = backward ? valid.Where(i => i <= start).Select(i => (long?)i).LastOrDefault() :
                    valid.Where(i => i >= start).Select(i => (long?)i).FirstOrDefault();
                if (!match.HasValue)
                {
                    SetSearchStatus(searchNext ? Strings.SearchEndReached : Strings.SearchNoMatch);
                    return null;
                }
                _lastSearchOrdinal = match.Value;
                SetSearchStatus(string.Format(CultureInfo.CurrentCulture, Strings.SearchMatchCountFormat, valid.Length));
                await NavigateAsync(match.Value, null, token);
                if (token.IsCancellationRequested || IsDisposed || version != _sourceVersion) return null;
                int index = list == null ? (int)match.Value : list.WindowIndexOf(match.Value);
                int column = Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible &&
                    (string.IsNullOrEmpty(columnName) || c.Name == columnName || c.DataPropertyName == columnName))?.Index ?? -1;
                if (index >= 0 && column >= 0)
                {
                    ClearSelection();
                    CurrentCell = Rows[index].Cells[column];
                    CurrentCell.Selected = true;
                }
                return match;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && !IsDisposed) SetSearchStatus(string.Format(Strings.SearchErrorFormat, ex.Message));
                return null;
            }
        }

        private void CancelSearch(bool clearPosition = true)
        {
            // Not disposed here: the running FindAsync/prefetch may still register on its token.
            _searchCts?.Cancel();
            _searchCts = null;
            if (clearPosition) _lastSearchOrdinal = -1;
        }
        internal void SetSearchStatus(string status)
        {
            _searchStatus = status;
            if (_searchStatusLabel != null) _searchStatusLabel.Text = status;
            _searchToolBar?.SetSearchStatus(status);
        }
        private async void StartSourceFind(string text, string? columnName, int rowIndex, bool caseSensitive)
        {
            _lastSearchText = text;
            _lastSearchColumn = columnName;
            _lastSearchCase = caseSensitive;
            _lastSearchOrdinal = (_sourceList?.WindowStart ?? 0) + Math.Max(0, rowIndex) - 1;
            await SearchAsync(text, columnName, caseSensitive, _lastSearchOrdinal >= 0);
        }

        /// <summary>Connects an existing search toolbar. Passing null detaches it.</summary>
        public void AttachSearchToolBar(AdvancedDataGridViewSearchToolBar? toolbar)
        {
            DetachSearchToolBar();
            _searchToolBar = toolbar;
            if (toolbar == null) return;
            toolbar.SetColumns(Columns);
            toolbar.Search += ToolbarSearch;
            toolbar.Disposed += ToolbarDisposed;
        }
        private void DetachSearchToolBar()
        {
            if (_searchToolBar == null) return;
            _searchToolBar.Search -= ToolbarSearch;
            _searchToolBar.Disposed -= ToolbarDisposed;
            _searchToolBar = null;
        }
        private void ToolbarDisposed(object? sender, EventArgs e) => DetachSearchToolBar();
        private async void ToolbarSearch(object sender, AdvancedDataGridViewSearchToolBarSearchEventArgs e)
        {
            // Preserve the toolbar's legacy whole-cell matching in ordinary bound mode.
            if (e.WholeWord && !IsVirtualSource && SearchRequested == null)
            {
                var cell = FindCell(e.ValueToSearch, e.ColumnToSearch?.Name,
                    e.FromBegin ? 0 : Math.Max(0, (CurrentCell?.RowIndex ?? -1) + 1), 0, true, e.CaseSensitive);
                if (cell != null) { CurrentCell = cell; ScrollToOrdinal(cell.RowIndex); SetMinimapSearchHits(new[] { (long)cell.RowIndex }); }
                SetSearchStatus(cell == null ? Strings.SearchNoMatch : string.Format(Strings.SearchMatchCountFormat, 1));
                return;
            }
            await SearchAsync(e.ValueToSearch, e.ColumnToSearch?.DataPropertyName, e.CaseSensitive, !e.FromBegin);
        }

        public void OpenSearch()
        {
            if (SearchRequested != null)
            {
                SearchRequested(this, new SearchEventArgs { SearchText = _searchBox?.Text ?? "", ColumnName = "" });
                return;
            }
            if (_searchToolBar == null && Parent != null)
                AttachSearchToolBar(Parent.Controls.OfType<AdvancedDataGridViewSearchToolBar>().FirstOrDefault());
            if (_searchToolBar != null) { _searchToolBar.Show(); _searchToolBar.FocusSearch(); return; }
            if (_searchPanel == null) BuildSearchPanel();
            _searchPanel!.Visible = true;
            LayoutSearchPanel();
            _searchBox!.Focus();
        }
        private void BuildSearchPanel()
        {
            _searchPanel = new FlowLayoutPanel { Height = 64, WrapContents = true, Padding = new Padding(4), AutoSize = false };
            _searchBox = new TextBox { Width = 160, AccessibleName = Strings.SearchPlaceholder };
            _searchColumn = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _searchColumn.Items.Add(Strings.AllColumns);
            foreach (DataGridViewColumn col in Columns) if (col.Visible) _searchColumn.Items.Add(col.Name);
            _searchColumn.SelectedIndex = 0;
            _searchCase = new CheckBox { Text = Strings.CaseSensitive, AutoSize = true };
            _searchStatusLabel = new Label { AutoSize = true, Text = _searchStatus, Padding = new Padding(4, 6, 0, 0) };
            _searchPanel.Controls.Add(new Label { Text = Strings.SearchLabel, AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
            _searchPanel.Controls.Add(_searchBox);
            _searchPanel.Controls.Add(_searchColumn);
            _searchPanel.Controls.Add(_searchCase);
            AddSearchButton(Strings.FindNext, () => RunPanelSearch(true, false));
            AddSearchButton(Strings.FindPrevious, () => RunPanelSearch(true, true));
            AddSearchButton(Strings.FindReset, () => RunPanelSearch(false, false));
            AddSearchButton(Strings.CloseSearch, () => IsSearchVisible = false);
            _searchPanel.Controls.Add(_searchStatusLabel);
            _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunPanelSearch(true, e.Shift); } };
            _searchBox.TextChanged += (_, __) => { CancelSearch(); SetMinimapSearchHits(null); SetSearchStatus(""); };
            Controls.Add(_searchPanel);
            ThemeSearchPanel();
        }
        private void AddSearchButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 25 };
            button.Click += (_, __) => action();
            _searchPanel!.Controls.Add(button);
        }
        private async void RunPanelSearch(bool next, bool backward) => await SearchAsync(_searchBox!.Text,
            _searchColumn!.SelectedIndex <= 0 ? null : _searchColumn.SelectedItem?.ToString(), _searchCase!.Checked, next, backward);
        private void LayoutSearchPanel()
        {
            if (_searchPanel == null) return;
            _searchPanel.SetBounds(1, Math.Max(1, ClientSize.Height - _searchPanel.Height - SystemInformation.HorizontalScrollBarHeight - 1),
                Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2), _searchPanel.Height);
            _searchPanel.BringToFront();
        }
        private void ThemeSearchPanel()
        {
            if (_searchPanel == null) return;
            // Follows SetTheme (the grid's own cell colors stay the caller's).
            Color back = OverlayDark ? Color.FromArgb(36, 49, 67) : Color.FromArgb(237, 242, 248);
            Color field = OverlayDark ? Color.FromArgb(24, 34, 48) : Color.White;
            Color fore = OverlayDark ? Color.FromArgb(225, 232, 240) : Color.FromArgb(25, 35, 48);
            _searchPanel.BackColor = back;
            _searchPanel.ForeColor = fore;
            foreach (Control control in _searchPanel.Controls)
            {
                control.BackColor = control is Label or CheckBox ? back : field;
                control.ForeColor = fore;
            }
        }
    }
}
