#region License
// Advanced DataGridView
//
// Copyright (c), 2014 Davide Gironi <davide.gironi@gmail.com>
// Original work Copyright (c), 2013 Zuby <zuby@me.com>
//
// Please refer to LICENSE file for licensing information.
#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NETFRAMEWORK
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif
using System.Windows.Forms;

namespace Zuby.ADGV
{

    [System.ComponentModel.DesignerCategory("")]
    public class AdvancedDataGridView : DataGridView
    {

        #region public enum

        /// <summary>
        /// Filter builder mode
        /// </summary>
        public enum FilterBuilerMode : byte
        {
            And = 0,
            Or = 1
        }

        #endregion


        #region public events

        public class SortEventArgs : EventArgs
        {
            public string SortString { get; set; }
            public bool Cancel { get; set; }

            public SortEventArgs()
            {
                SortString = null;
                Cancel = false;
            }
        }

        public class FilterEventArgs : EventArgs
        {
            public string FilterString { get; set; }
            public bool Cancel { get; set; }

            public FilterEventArgs()
            {
                FilterString = null;
                Cancel = false;
            }
        }

        public event EventHandler<SortEventArgs> SortStringChanged;

        public event EventHandler<FilterEventArgs> FilterStringChanged;

        /// <summary>
        /// Callback for asynchronous filter checklist loading
        /// </summary>
        public Func<DataGridViewColumn, string, Task<Dictionary<string, int>>> FilterAsyncLoadingNeeded;

        #endregion


        #region translations

        /// <summary>
        /// Available translation keys
        /// </summary>
        public enum TranslationKey
        {
            ADGVSortDateTimeASC,
            ADGVSortDateTimeDESC,
            ADGVSortBoolASC,
            ADGVSortBoolDESC,
            ADGVSortNumASC,
            ADGVSortNumDESC,
            ADGVSortTextASC,
            ADGVSortTextDESC,
            ADGVAddCustomFilter,
            ADGVCustomFilter,
            ADGVClearFilter,
            ADGVClearSort,
            ADGVButtonFilter,
            ADGVButtonUndofilter,
            ADGVNodeSelectAll,
            ADGVNodeSelectEmpty,
            ADGVNodeSelectTrue,
            ADGVNodeSelectFalse,
            ADGVFilterChecklistDisable,
            ADGVEquals,
            ADGVDoesNotEqual,
            ADGVEarlierThan,
            ADGVEarlierThanOrEqualTo,
            ADGVLaterThan,
            ADGVLaterThanOrEqualTo,
            ADGVBetween,
            ADGVGreaterThan,
            ADGVGreaterThanOrEqualTo,
            ADGVLessThan,
            ADGVLessThanOrEqualTo,
            ADGVBeginsWith,
            ADGVDoesNotBeginWith,
            ADGVEndsWith,
            ADGVDoesNotEndWith,
            ADGVContains,
            ADGVDoesNotContain,
            ADGVIncludeNullValues,
            ADGVInvalidValue,
            ADGVFilterStringDescription,
            ADGVFormTitle,
            ADGVLabelColumnNameText,
            ADGVLabelAnd,
            ADGVButtonOk,
            ADGVButtonCancel,
            ADGVTextFilters,
            ADGVNumberFilters,
            ADGVDateFilters
        }

        /// <summary>
        /// Internationalization strings
        /// </summary>
        public static Dictionary<string, string> Translations = new Dictionary<string, string>()
        {
            { TranslationKey.ADGVSortDateTimeASC.ToString(), "Eskiden Yeniye Sırala" },
            { TranslationKey.ADGVSortDateTimeDESC.ToString(), "Yeniden Eskiye Sırala" },
            { TranslationKey.ADGVSortBoolASC.ToString(), "Yanlış/Doğru Sırala" },
            { TranslationKey.ADGVSortBoolDESC.ToString(), "Doğru/Yanlış Sırala" },
            { TranslationKey.ADGVSortNumASC.ToString(), "Küçükten Büyüğe Sırala" },
            { TranslationKey.ADGVSortNumDESC.ToString(), "Büyükten Küçüğe Sırala" },
            { TranslationKey.ADGVSortTextASC.ToString(), "A'dan Z'ye Sırala" },
            { TranslationKey.ADGVSortTextDESC.ToString(), "Z'den A'ya Sırala" },
            { TranslationKey.ADGVCustomFilter.ToString(), "Özel Filtre..." },
            { TranslationKey.ADGVClearFilter.ToString(), "Filtreyi Temizle" },
            { TranslationKey.ADGVClearSort.ToString(), "Sıralamayı Temizle" },
            { TranslationKey.ADGVButtonFilter.ToString(), "Tamam" },
            { TranslationKey.ADGVButtonUndofilter.ToString(), "İptal" },
            { TranslationKey.ADGVNodeSelectAll.ToString(), "(Tümünü Seç)" },
            { TranslationKey.ADGVNodeSelectEmpty.ToString(), "(Boşlar)" },
            { TranslationKey.ADGVNodeSelectTrue.ToString(), "True" },
            { TranslationKey.ADGVNodeSelectFalse.ToString(), "False" },
            { TranslationKey.ADGVFilterChecklistDisable.ToString(), "Filtre listesi devre dışı" },
            { TranslationKey.ADGVEquals.ToString(), "eşittir" },
            { TranslationKey.ADGVDoesNotEqual.ToString(), "eşit değil" },
            { TranslationKey.ADGVEarlierThan.ToString(), "önce" },
            { TranslationKey.ADGVEarlierThanOrEqualTo.ToString(), "önce veya eşit" },
            { TranslationKey.ADGVLaterThan.ToString(), "sonra"},
            { TranslationKey.ADGVLaterThanOrEqualTo.ToString(), "sonra veya eşit" },
            { TranslationKey.ADGVBetween.ToString(), "arasında" },
            { TranslationKey.ADGVGreaterThan.ToString(), "büyüktür" },
            { TranslationKey.ADGVGreaterThanOrEqualTo.ToString(), "büyük veya eşittir" },
            { TranslationKey.ADGVLessThan.ToString(), "küçüktür" },
            { TranslationKey.ADGVLessThanOrEqualTo.ToString(), "küçük veya eşittir" },
            { TranslationKey.ADGVBeginsWith.ToString(), "başlangıcı" },
            { TranslationKey.ADGVDoesNotBeginWith.ToString(), "ile başlamayan" },
            { TranslationKey.ADGVEndsWith.ToString(), "sonu" },
            { TranslationKey.ADGVDoesNotEndWith.ToString(), "ile bitmeyen" },
            { TranslationKey.ADGVContains.ToString(), "içerir" },
            { TranslationKey.ADGVDoesNotContain.ToString(), "içermez" },
            { TranslationKey.ADGVIncludeNullValues.ToString(), "boş değerleri dahil et" },
            { TranslationKey.ADGVInvalidValue.ToString(), "Geçersiz Değer" },
            { TranslationKey.ADGVFilterStringDescription.ToString(), "Değerin {0} \"{1}\" olduğu satırları göster" },
            { TranslationKey.ADGVFormTitle.ToString(), "Özel Otomatik Filtre" },
            { TranslationKey.ADGVLabelColumnNameText.ToString(), "Filtre ölçütü:" },
            { TranslationKey.ADGVLabelAnd.ToString(), "Ve" },
            { TranslationKey.ADGVButtonOk.ToString(), "Tamam" },
            { TranslationKey.ADGVButtonCancel.ToString(), "İptal" },
            { TranslationKey.ADGVTextFilters.ToString(), "Metin Filtreleri" },
            { TranslationKey.ADGVNumberFilters.ToString(), "Sayı Filtreleri" },
            { TranslationKey.ADGVDateFilters.ToString(), "Tarih Filtreleri" },
            { TranslationKey.ADGVAddCustomFilter.ToString(), "Özel Filtre..." }
        };

        #endregion


        #region class properties and fields

        private List<string> _sortOrderList = new List<string>();
        private List<string> _filterOrderList = new List<string>();
        private List<string> _filteredColumns = new List<string>();
        private List<MenuStrip> _menuStripToDispose = new List<MenuStrip>();

        private bool _loadedFilter = false;
        private string _sortString = null;
        private string _filterString = null;

        private bool _sortStringChangedInvokeBeforeDatasourceUpdate = true;
        private bool _filterStringChangedInvokeBeforeDatasourceUpdate = true;

        private FilterBuilerMode _filterBuilerMode = FilterBuilerMode.And;

        internal int _maxFilterButtonImageHeight = ColumnHeaderCell.FilterButtonImageDefaultSize;

        internal int _maxAllCellHeight = ColumnHeaderCell.FilterButtonImageDefaultSize;

        #endregion

        #region Virtual Selection Properties & Fields

        /// <summary>
        /// Gets or sets whether Virtual Selection Mode is enabled.
        /// If true, native selection is disabled and a lightweight virtual selection logic is used.
        /// Default is false (Standard Native Selection).
        /// </summary>
        public bool VirtualModeSelectionEnabled { get; set; } = false;

        private HashSet<int> _virtualSelectedRows = new HashSet<int>();
        private HashSet<int> _virtualSelectedColumns = new HashSet<int>();
        private Dictionary<int, HashSet<int>> _virtualSelectedCells = new Dictionary<int, HashSet<int>>();
        private bool _isAllCellsSelected = false;
        private int _anchorRowIndex = -1;
        private int _anchorColumnIndex = -1;
        private int _lastMouseMoveRowIndex = -1;
        private int _lastMouseMoveColumnIndex = -1;

        public event EventHandler VirtualSelectionChanged;

        #endregion


        #region constructors

        /// <summary>
        /// AdvancedDataGridView constructor
        /// </summary>
        public AdvancedDataGridView()
        {
            RightToLeft = RightToLeft.No;
        }

        /// <summary>
        /// Handle the dispose methods
        /// </summary>
        /// <param name="e"></param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            foreach (DataGridViewColumn column in Columns)
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SortChanged -= Cell_SortChanged;
                    cell.FilterChanged -= Cell_FilterChanged;
                    cell.FilterPopup -= Cell_FilterPopup;
                }
            }
            foreach (MenuStrip menustrip in _menuStripToDispose)
            {
                menustrip.Dispose();
            }
            _menuStripToDispose.Clear();

            base.OnHandleDestroyed(e);
        }

        /// <summary>
        /// Handle the DataSource change
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDataSourceChanged(EventArgs e)
        {
            //dispose unactive menustrips
            foreach (DataGridViewColumn column in Columns)
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                _menuStripToDispose = _menuStripToDispose.Where(f => f != cell.MenuStrip).ToList();
            }
            foreach (MenuStrip menustrip in _menuStripToDispose)
            {
                menustrip.Dispose();
            }
            _menuStripToDispose.Clear();

            //update datatype for active menustrips
            foreach (DataGridViewColumn column in Columns)
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                cell.MenuStrip.SetDataType(column.ValueType);
            }

            base.OnDataSourceChanged(e);
        }
        #endregion


        #region translations methods

        /// <summary>
        /// Set translation dictionary
        /// </summary>
        /// <param name="translations"></param>
        public static void SetTranslations(IDictionary<string, string> translations)
        {
            //set localization strings
            if (translations != null)
            {
                foreach (KeyValuePair<string, string> translation in translations)
                {
                    if (Translations.ContainsKey(translation.Key))
                        Translations[translation.Key] = translation.Value;
                }
            }
        }

        /// <summary>
        /// Get translation dictionary
        /// </summary>
        /// <returns></returns>
        public static IDictionary<string, string> GetTranslations()
        {
            return Translations;
        }

        /// <summary>
        /// Load translations from file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static IDictionary<string, string> LoadTranslationsFromFile(string filename)
        {
            IDictionary<string, string> ret = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(filename))
            {
                //deserialize the file
                try
                {
                    string jsontext = File.ReadAllText(filename);
#if NETFRAMEWORK
                    Dictionary<string, string> translations = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(jsontext);
#else
                    Dictionary<string, string> translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsontext);
#endif
                    foreach (KeyValuePair<string, string> translation in translations)
                    {
                        if (!ret.ContainsKey(translation.Key) && Translations.ContainsKey(translation.Key))
                            ret.Add(translation.Key, translation.Value);
                    }
                }
                catch { }
            }

            //add default translations if not in files
            foreach (KeyValuePair<string, string> translation in GetTranslations())
            {
                if (!ret.ContainsKey(translation.Key))
                    ret.Add(translation.Key, translation.Value);
            }

            return ret;
        }

        #endregion


        #region public Helper methods

        /// <summary>
        /// Set AdvancedDataGridView the Double Buffered
        /// </summary>
        public void SetDoubleBuffered()
        {
            this.DoubleBuffered = true;
        }

        /// <summary>
        /// Set the theme for all filter menus
        /// </summary>
        /// <param name="isDarkMode"></param>
        public void SetTheme(bool isDarkMode)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
            {
                if (c.MenuStrip != null)
                    c.MenuStrip.SetTheme(isDarkMode);
            }
        }

        #endregion


        #region public Filter and Sort methods

        /// <summary>
        /// SortStringChanged event called before DataSource update after sort changed is triggered
        /// </summary>
        public bool SortStringChangedInvokeBeforeDatasourceUpdate
        {
            get
            {
                return _sortStringChangedInvokeBeforeDatasourceUpdate;
            }
            set
            {
                _sortStringChangedInvokeBeforeDatasourceUpdate = value;
            }
        }

        /// <summary>
        /// FilterStringChanged event called before DataSource update after sort changed is triggered
        /// </summary>
        public bool FilterStringChangedInvokeBeforeDatasourceUpdate
        {
            get
            {
                return _filterStringChangedInvokeBeforeDatasourceUpdate;
            }
            set
            {
                _filterStringChangedInvokeBeforeDatasourceUpdate = value;
            }
        }

        /// <summary>
        /// Disable a Filter and Sort on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void DisableFilterAndSort(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    if (cell.FilterAndSortEnabled == true && (cell.SortString.Length > 0 || cell.FilterString.Length > 0))
                    {
                        CleanFilter(true);
                        cell.FilterAndSortEnabled = false;
                    }
                    else
                        cell.FilterAndSortEnabled = false;
                    _filterOrderList.Remove(column.Name);
                    _sortOrderList.Remove(column.Name);
                    _filteredColumns.Remove(column.Name);
                }
            }
        }

        /// <summary>
        /// Enable a Filter and Sort on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void EnableFilterAndSort(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    if (!cell.FilterAndSortEnabled && (cell.FilterString.Length > 0 || cell.SortString.Length > 0))
                        CleanFilter(true);

                    cell.FilterAndSortEnabled = true;
                    _filteredColumns.Remove(column.Name);

                    SetFilterDateAndTimeEnabled(column, cell.IsFilterDateAndTimeEnabled);
                    SetSortEnabled(column, cell.IsSortEnabled);
                    SetFilterEnabled(column, cell.IsFilterEnabled);
                }
                else
                {
                    column.SortMode = DataGridViewColumnSortMode.Programmatic;
                    cell = new ColumnHeaderCell(this, column.HeaderCell, true);
                    cell.SortChanged += new ColumnHeaderCellEventHandler(Cell_SortChanged);
                    cell.FilterChanged += new ColumnHeaderCellEventHandler(Cell_FilterChanged);
                    cell.FilterPopup += new ColumnHeaderCellEventHandler(Cell_FilterPopup);
                    column.MinimumWidth = cell.MinimumSize.Width;
                    if (ColumnHeadersHeight < cell.MinimumSize.Height)
                        ColumnHeadersHeight = cell.MinimumSize.Height;
                    column.HeaderCell = cell;
                }
            }
        }

        /// <summary>
        /// Enabled or disable Filter and Sort capabilities on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetFilterAndSortEnabled(DataGridViewColumn column, bool enabled)
        {
            if (enabled)
                EnableFilterAndSort(column);
            else
                DisableFilterAndSort(column);
        }

        /// <summary>
        /// Disable a Filter checklist on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void DisableFilterChecklist(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterChecklistEnabled(false);
                }
            }
        }

        /// <summary>
        /// Enable a Filter checklist on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void EnableFilterChecklist(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterChecklistEnabled(true);
                }
            }
        }

        /// <summary>
        /// Enabled or disable Filter checklist capabilities on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetFilterChecklistEnabled(DataGridViewColumn column, bool enabled)
        {
            if (enabled)
                EnableFilterChecklist(column);
            else
                DisableFilterChecklist(column);
        }

        /// <summary>
        /// Set Filter checklist nodes max on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="maxnodes"></param>
        public void SetFilterChecklistNodesMax(DataGridViewColumn column, int maxnodes)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterChecklistNodesMax(maxnodes);
                }
            }
        }

        /// <summary>
        /// Set Filter checklist nodes max
        /// </summary>
        /// <param name="maxnodes"></param>
        public void SetFilterChecklistNodesMax(int maxnodes)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.SetFilterChecklistNodesMax(maxnodes);
        }

        /// <summary>
        /// Enable or disable Filter checklist nodes max on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void EnabledFilterChecklistNodesMax(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.EnabledFilterChecklistNodesMax(enabled);
                }
            }
        }

        /// <summary>
        /// Enable or disable Filter checklist nodes max
        /// </summary>
        /// <param name="enabled"></param>
        public void EnabledFilterChecklistNodesMax(bool enabled)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.EnabledFilterChecklistNodesMax(enabled);
        }

        /// <summary>
        /// Disable a Filter custom on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void DisableFilterCustom(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterCustomEnabled(false);
                }
            }
        }

        /// <summary>
        /// Enable a Filter custom on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void EnableFilterCustom(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterCustomEnabled(true);
                }
            }
        }

        /// <summary>
        /// Enabled or disable Filter custom capabilities on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetFilterCustomEnabled(DataGridViewColumn column, bool enabled)
        {
            if (enabled)
                EnableFilterCustom(column);
            else
                DisableFilterCustom(column);
        }

        /// <summary>
        /// Set nodes to enable TextChanged delay on filter checklist on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="numnodes"></param>
        public void SetFilterChecklistTextFilterTextChangedDelayNodes(DataGridViewColumn column, int numnodes)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.TextFilterTextChangedDelayNodes = numnodes;
                }
            }
        }

        /// <summary>
        /// Set nodes to enable TextChanged delay on filter checklist
        /// </summary>
        /// <param name="numnodes"></param>
        public void SetFilterChecklistTextFilterTextChangedDelayNodes(int numnodes)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.TextFilterTextChangedDelayNodes = numnodes;
        }

        /// <summary>
        /// Disable TextChanged delay on filter checklist on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void SetFilterChecklistTextFilterTextChangedDelayDisabled(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetTextFilterTextChangedDelayNodesDisabled();
                }
            }
        }

        /// <summary>
        /// Disable TextChanged delay on filter checklist
        /// </summary>
        public void SetFilterChecklistTextFilterTextChangedDelayDisabled()
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.SetTextFilterTextChangedDelayNodesDisabled();
        }

        /// <summary>
        /// Set TextChanged delay milliseconds on filter checklist on a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        public void SetFilterChecklistTextFilterTextChangedDelayMs(DataGridViewColumn column, int milliseconds)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetTextFilterTextChangedDelayMs(milliseconds);
                }
            }
        }

        /// <summary>
        /// Set TextChanged delay milliseconds on filter checklist
        /// </summary>
        public void SetFilterChecklistTextFilterTextChangedDelayMs(int milliseconds)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.SetTextFilterTextChangedDelayMs(milliseconds);
        }

        /// <summary>
        /// Load a Filter and Sort preset
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="sorting"></param>
        public void LoadFilterAndSort(string filter, string sorting)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.SetLoadedMode(true);

            _filteredColumns.Clear();

            _filterOrderList.Clear();
            _sortOrderList.Clear();

            if (filter != null)
                FilterString = filter;
            if (sorting != null)
                SortString = sorting;

            _loadedFilter = true;
        }

        /// <summary>
        /// Clean Filter and Sort
        /// </summary>
        public void CleanFilterAndSort()
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.SetLoadedMode(false);

            _filteredColumns.Clear();
            _filterOrderList.Clear();
            _sortOrderList.Clear();

            _loadedFilter = false;

            CleanFilter();
            CleanSort();
        }

        /// <summary>
        /// Set the NOTIN Logic for checkbox filter
        /// </summary>
        /// <param name="enabled"></param>
        public void SetMenuStripFilterNOTINLogic(bool enabled)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.IsMenuStripFilterNOTINLogicEnabled = enabled;
        }

        /// <summary>
        /// Get or Set Filter and Sort status
        /// </summary>
        public bool FilterAndSortEnabled
        {
            get
            {
                return _filterAndSortEnabled;
            }
            set
            {
                _filterAndSortEnabled = value;
            }
        }
        private bool _filterAndSortEnabled = true;

        /// <summary>
        /// Set the Filter Text focus OnShow
        /// </summary>
        /// <param name="enabled"></param>
        public void SetFilterTextFocusOnShow(bool enabled)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.FilterTextFocusOnShow = enabled;
        }

        #endregion


        #region public Sort methods

        /// <summary>
        /// Get the Sort string
        /// </summary>
        public string SortString
        {
            get
            {
                return (!String.IsNullOrEmpty(_sortString) ? _sortString : "");
            }
            private set
            {
                string old = value;
                if (old != _sortString)
                {
                    _sortString = value;

                    TriggerSortStringChanged();
                }
            }
        }

        /// <summary>
        /// Trigger the sort string changed method
        /// </summary>
        public void TriggerSortStringChanged()
        {
            //call event handler if one is attached
            SortEventArgs sortEventArgs = new SortEventArgs
            {
                SortString = _sortString,
                Cancel = false
            };
            //invoke SortStringChanged
            if (_sortStringChangedInvokeBeforeDatasourceUpdate)
            {
                if (SortStringChanged != null)
                    SortStringChanged.Invoke(this, sortEventArgs);
            }
            //sort datasource
            if (sortEventArgs.Cancel == false)
            {
                if (this.DataSource is BindingSource bindingsource)
                {
                    bindingsource.Sort = sortEventArgs.SortString;
                }
                else if (this.DataSource is DataView dataview)
                {
                    dataview.Sort = sortEventArgs.SortString;
                }
                else if (this.DataSource is DataTable datatable)
                {
                    if (datatable.DefaultView != null)
                        datatable.DefaultView.Sort = sortEventArgs.SortString;
                }
            }
            //invoke SortStringChanged
            if (!_sortStringChangedInvokeBeforeDatasourceUpdate)
            {
                if (SortStringChanged != null)
                    SortStringChanged.Invoke(this, sortEventArgs);
            }
        }

        /// <summary>
        /// Enabled or disable Sort capabilities for a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetSortEnabled(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetSortEnabled(enabled);
                }
            }
        }

        /// <summary>
        /// Sort ASC
        /// </summary>
        public void SortASC(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SortASC();
                }
            }
        }

        /// <summary>
        /// Sort ASC
        /// </summary>
        public void SortDESC(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SortDESC();
                }
            }
        }

        /// <summary>
        /// Clean all Sort on specific column
        /// </summary>
        /// <param name="column"></param>
        /// <param name="fireEvent"></param>
        public void CleanSort(DataGridViewColumn column, bool fireEvent)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null && FilterableCells.Contains(cell))
                {
                    cell.CleanSort();
                    //remove column from sorted list
                    _sortOrderList.Remove(column.Name);
                }
            }

            if (fireEvent)
                SortString = BuildSortString();
            else
                _sortString = BuildSortString();
        }

        /// <summary>
        /// Clean all Sort on specific column
        /// </summary>
        /// <param name="column"></param>
        public void CleanSort(DataGridViewColumn column)
        {
            CleanSort(column, true);
        }

        /// <summary>
        /// Clean all Sort on all columns
        /// </summary>
        /// <param name="fireEvent"></param>
        public void CleanSort(bool fireEvent)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
                c.CleanSort();
            _sortOrderList.Clear();

            if (fireEvent)
                SortString = null;
            else
                _sortString = null;
        }

        /// <summary>
        /// Clean all Sort on all columns
        /// </summary>
        public void CleanSort()
        {
            CleanSort(true);
        }

        #endregion


        #region public Filter methods

        /// <summary>
        /// Get the Filter string
        /// </summary>
        public string FilterString
        {
            get
            {
                return (!String.IsNullOrEmpty(_filterString) ? _filterString : "");
            }
            private set
            {
                string old = value;
                if (old != _filterString)
                {
                    _filterString = value;

                    TriggerFilterStringChanged();
                }
            }
        }

        /// <summary>
        /// Trigger the filter string changed method
        /// </summary>
        public void TriggerFilterStringChanged()
        {
            //call event handler if one is attached
            FilterEventArgs filterEventArgs = new FilterEventArgs
            {
                FilterString = _filterString,
                Cancel = false
            };
            //invoke FilterStringChanged
            if (_filterStringChangedInvokeBeforeDatasourceUpdate)
            {
                if (FilterStringChanged != null)
                    FilterStringChanged.Invoke(this, filterEventArgs);
            }
            //filter datasource
            if (filterEventArgs.Cancel == false)
            {
                if (this.DataSource is BindingSource bindingsource)
                {
                    bindingsource.Filter = filterEventArgs.FilterString;
                }
                else if (this.DataSource is DataView dataview)
                {
                    dataview.RowFilter = filterEventArgs.FilterString;
                }
                else if (this.DataSource is DataTable datatable)
                {
                    if (datatable.DefaultView != null)
                        datatable.DefaultView.RowFilter = filterEventArgs.FilterString;
                }
            }
            //invoke FilterStringChanged
            if (!_filterStringChangedInvokeBeforeDatasourceUpdate)
            {
                if (FilterStringChanged != null)
                    FilterStringChanged.Invoke(this, filterEventArgs);
            }
        }

        /// <summary>
        /// Set FilterDateAndTime status for a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetFilterDateAndTimeEnabled(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.IsFilterDateAndTimeEnabled = enabled;
                }
            }
        }

        /// <summary>
        /// Enable or disable Filter capabilities for a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetFilterEnabled(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetFilterEnabled(enabled);
                }
            }
        }

        /// <summary>
        /// Enable or disable Text filter on checklist remove node mode for a DataGridViewColumn
        /// </summary>
        /// <param name="column"></param>
        /// <param name="enabled"></param>
        public void SetChecklistTextFilterRemoveNodesOnSearchMode(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.SetChecklistTextFilterRemoveNodesOnSearchMode(enabled);
                }
            }
        }

        /// <summary>
        /// Clean Filter on specific column
        /// </summary>
        /// <param name="column"></param>
        /// <param name="fireEvent"></param>
        public void CleanFilter(DataGridViewColumn column, bool fireEvent)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    cell.CleanFilter();
                    //remove column from filtered list
                    _filterOrderList.Remove(column.Name);
                }
            }

            if (fireEvent)
                FilterString = BuildFilterString();
            else
                _filterString = BuildFilterString();
        }

        /// <summary>
        /// Clean Filter on specific column
        /// </summary>
        /// <param name="column"></param>
        public void CleanFilter(DataGridViewColumn column)
        {
            CleanFilter(column, true);
        }

        /// <summary>
        /// Clean Filter on all columns
        /// </summary>
        /// <param name="fireEvent"></param>
        public void CleanFilter(bool fireEvent)
        {
            foreach (ColumnHeaderCell c in FilterableCells)
            {
                c.CleanFilter();
            }
            _filterOrderList.Clear();

            if (fireEvent)
                FilterString = null;
            else
                _filterString = null;
        }

        /// <summary>
        /// Clean all Sort on all columns
        /// </summary>
        public void CleanFilter()
        {
            CleanFilter(true);
        }

        /// <summary>
        /// Set the text filter search nodes behaviour
        /// </summary>
        public void SetTextFilterRemoveNodesOnSearch(DataGridViewColumn column, bool enabled)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                    cell.DoesTextFilterRemoveNodesOnSearch = enabled;
            }
        }

        /// <summary>
        /// Get the text filter search nodes behaviour
        /// </summary>
        public Nullable<bool> GetTextFilterRemoveNodesOnSearch(DataGridViewColumn column)
        {
            Nullable<bool> ret = null;
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                    ret = cell.DoesTextFilterRemoveNodesOnSearch;
            }
            return ret;
        }


        /// <summary>
        /// Return the filtered strings by column name
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetColumnsFilteredStrings()
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            foreach (string filterOrder in _filterOrderList)
            {
                DataGridViewColumn Column = Columns[filterOrder];

                if (Column != null)
                {
                    ColumnHeaderCell cell = Column.HeaderCell as ColumnHeaderCell;
                    if (cell != null)
                    {
                        if (cell.FilterAndSortEnabled && cell.ActiveFilterType != MenuStrip.FilterType.None)
                        {
                            if (!ret.ContainsKey(Column.DataPropertyName))
                            {
                                ret.Add(Column.DataPropertyName, cell.FilterString);
                            }
                        }
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Set the filter builder mode
        /// </summary>
        public void SetFilterBuilderMode(FilterBuilerMode filterBuilerMode)
        {
            _filterBuilerMode = filterBuilerMode;
        }

        /// <summary>
        /// Get the filter builder mode
        /// </summary>
        public FilterBuilerMode GetFilterBuilderMode()
        {
            return _filterBuilerMode;
        }

        #endregion


        #region public Find methods

        /// <summary>
        /// Find the Cell with the given value
        /// </summary>
        /// <param name="valueToFind"></param>
        /// <param name="columnName"></param>
        /// <param name="rowIndex"></param>
        /// <param name="columnIndex"></param>
        /// <param name="isWholeWordSearch"></param>
        /// <param name="isCaseSensitive"></param>
        /// <returns></returns>
        public DataGridViewCell FindCell(string valueToFind, string columnName, int rowIndex, int columnIndex, bool isWholeWordSearch, bool isCaseSensitive)
        {
            if (valueToFind != null && RowCount > 0 && ColumnCount > 0 && (columnName == null || (Columns.Contains(columnName) && Columns[columnName].Visible)))
            {
                rowIndex = Math.Max(0, rowIndex);

                if (!isCaseSensitive)
                    valueToFind = valueToFind.ToLower();

                if (columnName != null)
                {
                    int c = Columns[columnName].Index;
                    if (columnIndex > c)
                        rowIndex++;
                    for (int r = rowIndex; r < RowCount; r++)
                    {
                        string value = Rows[r].Cells[c].FormattedValue.ToString();
                        if (!isCaseSensitive)
                            value = value.ToLower();

                        if ((!isWholeWordSearch && value.Contains(valueToFind)) || value.Equals(valueToFind))
                            return Rows[r].Cells[c];
                    }
                }
                else
                {
                    columnIndex = Math.Max(0, columnIndex);

                    for (int r = rowIndex; r < RowCount; r++)
                    {
                        for (int c = columnIndex; c < ColumnCount; c++)
                        {
                            if (!Rows[r].Cells[c].Visible)
                                continue;

                            string value = Rows[r].Cells[c].FormattedValue.ToString();
                            if (!isCaseSensitive)
                                value = value.ToLower();

                            if ((!isWholeWordSearch && value.Contains(valueToFind)) || value.Equals(valueToFind))
                                return Rows[r].Cells[c];
                        }

                        columnIndex = 0;
                    }
                }
            }

            return null;
        }

        #endregion


        #region public Cell methods

        /// <summary>
        /// Show a menu strip
        /// </summary>
        /// <param name="column"></param>
        public void ShowMenuStrip(DataGridViewColumn column)
        {
            if (Columns.Contains(column))
            {
                ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                if (cell != null)
                {
                    Cell_FilterPopup(cell, new ColumnHeaderCellEventArgs(cell.MenuStrip, column));
                }
            }
        }

        /// <summary>
        /// Get or Set the max filter button image height
        /// </summary>
        public int MaxFilterButtonImageHeight
        {
            get
            {
                return _maxFilterButtonImageHeight;
            }
            set
            {
                _maxFilterButtonImageHeight = value > ColumnHeaderCell.FilterButtonImageDefaultSize ? value : ColumnHeaderCell.FilterButtonImageDefaultSize;
            }
        }

        #endregion


        #region internal Cell methods

        /// <summary>
        /// Get or Set the max filter button image height of all cells
        /// </summary>
        internal int MaxAllCellHeight
        {
            get
            {
                return _maxAllCellHeight;
            }
            set
            {
                _maxAllCellHeight = value > ColumnHeaderCell.FilterButtonImageDefaultSize ? value : ColumnHeaderCell.FilterButtonImageDefaultSize;
            }
        }

        #endregion


        #region cells methods

        /// <summary>
        /// Get all columns
        /// </summary>
        private IEnumerable<ColumnHeaderCell> FilterableCells
        {
            get
            {
                return from DataGridViewColumn c in Columns
                       where c.HeaderCell != null && c.HeaderCell is ColumnHeaderCell
                       select (c.HeaderCell as ColumnHeaderCell);
            }
        }

        #endregion


        #region column events

        /// <summary>
        /// Overriden  OnColumnAdded event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnColumnAdded(DataGridViewColumnEventArgs e)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.Programmatic;
            ColumnHeaderCell cell = new ColumnHeaderCell(this, e.Column.HeaderCell, FilterAndSortEnabled);
            cell.SortChanged += new ColumnHeaderCellEventHandler(Cell_SortChanged);
            cell.FilterChanged += new ColumnHeaderCellEventHandler(Cell_FilterChanged);
            cell.FilterPopup += new ColumnHeaderCellEventHandler(Cell_FilterPopup);
            e.Column.MinimumWidth = cell.MinimumSize.Width;
            if (ColumnHeadersHeight < cell.MinimumSize.Height)
                ColumnHeadersHeight = cell.MinimumSize.Height;
            e.Column.HeaderCell = cell;

            base.OnColumnAdded(e);
        }

        /// <summary>
        /// Overridden OnColumnRemoved event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnColumnRemoved(DataGridViewColumnEventArgs e)
        {
            _filteredColumns.Remove(e.Column.Name);
            _filterOrderList.Remove(e.Column.Name);
            _sortOrderList.Remove(e.Column.Name);

            ColumnHeaderCell cell = e.Column.HeaderCell as ColumnHeaderCell;
            if (cell != null)
            {
                cell.SortChanged -= Cell_SortChanged;
                cell.FilterChanged -= Cell_FilterChanged;
                cell.FilterPopup -= Cell_FilterPopup;

                cell.CleanEvents();
                if (!e.Column.IsDataBound)
                    cell.MenuStrip.Dispose();
                else
                    _menuStripToDispose.Add(cell.MenuStrip);
            }
            base.OnColumnRemoved(e);
        }

        #endregion


        #region rows events

        /// <summary>
        /// Overridden OnRowsAdded event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e)
        {
            if (e.RowIndex >= 0)
                _filteredColumns.Clear();
            base.OnRowsAdded(e);
        }

        /// <summary>
        /// Overridden OnRowsRemoved event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
        {
            if (e.RowIndex >= 0)
                _filteredColumns.Clear();
            base.OnRowsRemoved(e);
        }

        #endregion


        #region cell events

        /// <summary>
        /// Overridden OnCellValueChanged event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnCellValueChanged(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                _filteredColumns.Remove(Columns[e.ColumnIndex].Name);
            base.OnCellValueChanged(e);
        }

        #endregion


        #region filter events

        /// <summary>
        /// Build the complete Filter string
        /// </summary>
        /// <returns></returns>
        private string BuildFilterString()
        {
            StringBuilder sb = new StringBuilder("");
            string appx = "";

            foreach (string filterOrder in _filterOrderList)
            {
                DataGridViewColumn Column = Columns[filterOrder];

                if (Column != null)
                {
                    ColumnHeaderCell cell = Column.HeaderCell as ColumnHeaderCell;
                    if (cell != null)
                    {
                        if (cell.FilterAndSortEnabled && cell.ActiveFilterType != MenuStrip.FilterType.None)
                        {
                            sb.AppendFormat(appx + "(" + cell.FilterString + ")", Column.DataPropertyName);
                            if (_filterBuilerMode == FilterBuilerMode.And)
                                appx = " AND ";
                            else if (_filterBuilerMode == FilterBuilerMode.Or)
                                appx = " OR ";
                        }
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// FilterPopup event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cell_FilterPopup(object sender, ColumnHeaderCellEventArgs e)
        {
            if (Columns.Contains(e.Column))
            {
                MenuStrip filterMenu = e.FilterMenu;
                DataGridViewColumn column = e.Column;

                Rectangle rect = GetCellDisplayRectangle(column.Index, -1, true);

                if (_filteredColumns.Contains(column.Name))
                    filterMenu.Show(this, rect.Left, rect.Bottom, false);
                else
                {
                    _filteredColumns.Add(column.Name);
                    if (_filterOrderList.Count() > 0 && _filterOrderList.Last() == column.Name)
                        filterMenu.Show(this, rect.Left, rect.Bottom, true);
                    else
                        filterMenu.Show(this, rect.Left, rect.Bottom, column.Name);
                }
            }
        }

        /// <summary>
        /// FilterChanged event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cell_FilterChanged(object sender, ColumnHeaderCellEventArgs e)
        {
            if (Columns.Contains(e.Column))
            {
                MenuStrip filterMenu = e.FilterMenu;
                DataGridViewColumn column = e.Column;

                _filterOrderList.Remove(column.Name);
                if (filterMenu.ActiveFilterType != MenuStrip.FilterType.None)
                    _filterOrderList.Add(column.Name);

                FilterString = BuildFilterString();

                if (_loadedFilter)
                {
                    _loadedFilter = false;
                    foreach (ColumnHeaderCell c in FilterableCells.Where(f => f.MenuStrip != filterMenu))
                        c.SetLoadedMode(false);
                }
            }
        }

        #endregion


        #region sort events

        /// <summary>
        /// Build the complete Sort string
        /// </summary>
        /// <returns></returns>
        private string BuildSortString()
        {
            StringBuilder sb = new StringBuilder("");
            string appx = "";

            foreach (string sortOrder in _sortOrderList)
            {
                DataGridViewColumn column = Columns[sortOrder];

                if (column != null)
                {
                    ColumnHeaderCell cell = column.HeaderCell as ColumnHeaderCell;
                    if (cell != null)
                    {
                        if (cell.FilterAndSortEnabled && cell.ActiveSortType != MenuStrip.SortType.None)
                        {
                            sb.AppendFormat(appx + cell.SortString, column.DataPropertyName);
                            appx = ", ";
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// SortChanged event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cell_SortChanged(object sender, ColumnHeaderCellEventArgs e)
        {
            if (Columns.Contains(e.Column))
            {
                MenuStrip filterMenu = e.FilterMenu;
                DataGridViewColumn column = e.Column;

                _sortOrderList.Remove(column.Name);
                if (filterMenu.ActiveSortType != MenuStrip.SortType.None)
                    _sortOrderList.Add(column.Name);
                SortString = BuildSortString();
            }
        }

        #endregion

        /// <summary>
        /// Selects all cells. Promotes to Virtual Selection if VirtualModeSelectionEnabled is true.
        public new void SelectAll()
        {
            if (VirtualModeSelectionEnabled)
            {
                VirtualSelectAll();
            }
            else
            {
                base.SelectAll();
            }
        }

        /// <summary>
        /// Global key handling for the control.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (VirtualModeSelectionEnabled && keyData == (Keys.Control | Keys.A))
            {
                VirtualSelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region Virtual Selection Public Methods

        /// <summary>
        /// Selects all cells in Virtual Mode without creating generic objects.
        /// </summary>
        public void VirtualSelectAll()
        {
            if (!VirtualModeSelectionEnabled) return;

            _isAllCellsSelected = true;
            _virtualSelectedRows.Clear();
            _virtualSelectedCells.Clear();
            
            OnVirtualSelectionChanged();
            Invalidate();
        }

        /// <summary>
        /// Clears all virtual selection.
        /// </summary>
        public void VirtualClearSelection(bool keepAnchor = false)
        {
            if (!VirtualModeSelectionEnabled) return;

            _isAllCellsSelected = false;
            _virtualSelectedRows.Clear();
            _virtualSelectedColumns.Clear();
            _virtualSelectedCells.Clear();
            
            if (!keepAnchor)
            {
                _anchorRowIndex = -1;
                _anchorColumnIndex = -1;
            }

            OnVirtualSelectionChanged();
            Invalidate();
        }

        /// <summary>
        /// Sets the virtual selection state of a specific row.
        /// </summary>
        public void SetVirtualRowSelected(int rowIndex, bool selected)
        {
            if (!VirtualModeSelectionEnabled) return;

            if (selected)
                _virtualSelectedRows.Add(rowIndex);
            else
                _virtualSelectedRows.Remove(rowIndex);
        }

        public bool IsVirtualSelectAll => _isAllCellsSelected;

        /// <summary>
        /// Gets the approximate count of selected cells in Virtual Mode.
        /// Efficiently calculated without enumerating all cells.
        /// </summary>
        public long VirtualSelectedCellCount
        {
            get
            {
                if (!VirtualModeSelectionEnabled) return 0;
                if (_isAllCellsSelected) return (long)RowCount * ColumnCount;

                long count = 0;
                // Entire Columns
                count += (long)_virtualSelectedColumns.Count * RowCount;

                // Entire Rows (excluding already counted via Columns)
                long remainingCols = ColumnCount - _virtualSelectedColumns.Count;
                count += (long)_virtualSelectedRows.Count * remainingCols;

                // Individual Cells
                foreach (var kvp in _virtualSelectedCells)
                {
                    int rIdx = kvp.Key;
                    if (_virtualSelectedRows.Contains(rIdx)) continue;
                    
                    foreach (var cIdx in kvp.Value)
                    {
                        if (!_virtualSelectedColumns.Contains(cIdx)) count++;
                    }
                }
                return count;
            }
        }


        public bool IsVirtualCellSelected(int rowIndex, int columnIndex)
        {
            if (!VirtualModeSelectionEnabled) return false;
            if (_isAllCellsSelected) return true;
            if (_virtualSelectedColumns.Contains(columnIndex)) return true;

            if (SelectionMode == DataGridViewSelectionMode.FullRowSelect || SelectionMode == DataGridViewSelectionMode.RowHeaderSelect)
            {
                return _virtualSelectedRows.Contains(rowIndex);
            }
            
            if (_virtualSelectedCells.TryGetValue(rowIndex, out var cols))
            {
                return cols.Contains(columnIndex);
            }

            return false;
        }

        /// <summary>
        /// Gets the indices of all selected rows in Virtual Mode.
        /// </summary>
        public IEnumerable<int> GetVirtualSelectedRowIndices()
        {
            if (!VirtualModeSelectionEnabled) yield break;

            if (_isAllCellsSelected)
            {
                for (int i = 0; i < RowCount; i++) yield return i;
            }
            else
            {
                // Unique set of indices
                var seen = new HashSet<int>(_virtualSelectedRows);
                foreach (var index in seen) yield return index;

                foreach (var kvp in _virtualSelectedCells)
                {
                    if (seen.Add(kvp.Key)) yield return kvp.Key;
                }
            }
        }

        /// <summary>
        /// Gets the indices of all selected range (points) in Virtual Mode.
        /// </summary>
        public IEnumerable<Point> GetVirtualSelectedCellIndices()
        {
            if (!VirtualModeSelectionEnabled) yield break;

            long totalCount = VirtualSelectedCellCount;
            if (totalCount > 1000000) // 1 Million safety cap for Point generation
            {
                // If selection is too large, we should NOT yield millions of Points to avoid OOM.
                // The caller should use VirtualSelectedCellCount or RowIndices instead.
                yield break;
            }

            if (_isAllCellsSelected)
            {
                for (int r = 0; r < RowCount; r++)
                {
                    for (int c = 0; c < ColumnCount; c++)
                    {
                        yield return new Point(c, r);
                    }
                }
            }
            else
            {
                // Support Column Selections
                var colIndices = _virtualSelectedColumns.ToList();

                // Support Full Rows
                var rowIndices = _virtualSelectedRows.ToList();

                for (int r = 0; r < RowCount; r++)
                {
                    bool fullRow = _virtualSelectedRows.Contains(r);
                    
                    for (int c = 0; c < ColumnCount; c++)
                    {
                        if (fullRow || _virtualSelectedColumns.Contains(c))
                        {
                            yield return new Point(c, r);
                        }
                        else if (_virtualSelectedCells.TryGetValue(r, out var cols) && cols.Contains(c))
                        {
                            yield return new Point(c, r);
                        }
                    }
                }
            }
        }

        public IEnumerable<int> GetVirtualSelectedColumnIndices() => _virtualSelectedColumns;

        protected virtual void OnVirtualSelectionChanged()
        {
            VirtualSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Virtual Selection Logic

        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            if (VirtualModeSelectionEnabled && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (IsVirtualCellSelected(e.RowIndex, e.ColumnIndex))
                {
                    // Draw custom selection background - SystemBrushes.Highlight is efficient
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, e.CellBounds);

                    // Standard HighlightText for selection
                    var savedForeColor = e.CellStyle.ForeColor;
                    e.CellStyle.ForeColor = SystemColors.HighlightText;

                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Background & ~DataGridViewPaintParts.SelectionBackground);
                    
                    e.CellStyle.ForeColor = savedForeColor;
                    e.Handled = true;
                    return;
                }
            }

            base.OnCellPainting(e);
        }

        protected override void OnCellDoubleClick(DataGridViewCellEventArgs e)
        {
            if (VirtualModeSelectionEnabled)
            {
                if (e.RowIndex == -1 && e.ColumnIndex >= 0) // Column Header
                {
                    if (Control.ModifierKeys == Keys.None) VirtualClearSelection();
                    if (!_virtualSelectedColumns.Add(e.ColumnIndex)) _virtualSelectedColumns.Remove(e.ColumnIndex);
                    OnVirtualSelectionChanged();
                    Invalidate();
                }
                else if (e.ColumnIndex == -1 && e.RowIndex >= 0) // Row Header
                {
                    if (Control.ModifierKeys == Keys.None) VirtualClearSelection();
                    if (!_virtualSelectedRows.Add(e.RowIndex)) _virtualSelectedRows.Remove(e.RowIndex);
                    OnVirtualSelectionChanged();
                    Invalidate();
                }
            }
            base.OnCellDoubleClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (VirtualModeSelectionEnabled)
            {
                var hit = HitTest(e.X, e.Y);
                if (hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.RowHeader || hit.Type == DataGridViewHitTestType.TopLeftHeader)
                {
                    if (hit.Type == DataGridViewHitTestType.TopLeftHeader)
                    {
                        Focus();
                        VirtualSelectAll();
                        return;
                    }

                    Keys modifiers = Control.ModifierKeys;
                    bool isShift = (modifiers & Keys.Shift) == Keys.Shift;
                    bool isCtrl = (modifiers & Keys.Control) == Keys.Control;
                    bool isRightClick = e.Button == MouseButtons.Right;

                    if (isRightClick)
                    {
                        // User specifically requested NO SELECTION on right-click.
                        // By NOT calling base.OnMouseDown(e), we prevent the DataGridView 
                        // from changing the selection or the CurrentCell.
                        // The ContextMenuStrip will still be triggered by the OS/WndProc.
                        return;
                    }

                    if (!isShift && !isCtrl)
                    {
                        // 2. Plain Left Click: Clear Virtual and allow Native Selection
                        VirtualClearSelection();
                        
                        base.OnMouseDown(e);
                        
                        _anchorRowIndex = hit.RowIndex;
                        _anchorColumnIndex = hit.ColumnIndex;
                        _lastMouseMoveRowIndex = hit.RowIndex;
                        _lastMouseMoveColumnIndex = hit.ColumnIndex;
                    }
                    else
                    {
                        // 3. Modifiers present: Support Shift/Ctrl by promoting native to virtual first
                        PromoteNativeToVirtual();
                        
                        if (_anchorRowIndex == -1) 
                        {
                            _anchorRowIndex = hit.RowIndex;
                            _anchorColumnIndex = hit.ColumnIndex;
                        }

                        HandleVirtualSelection(hit.RowIndex, hit.ColumnIndex, modifiers);
                        ClearSelection();
                    }
                }
                else
                {
                    base.OnMouseDown(e);
                }
            }
            else
            {
                base.OnMouseDown(e);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (VirtualModeSelectionEnabled && e.Button == MouseButtons.Left)
            {
                var hit = HitTest(e.X, e.Y);
                if ((hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.RowHeader) 
                    && (hit.RowIndex != _lastMouseMoveRowIndex || hit.ColumnIndex != _lastMouseMoveColumnIndex))
                {
                    // Viewport boundary check: If mouse goes outside visible rows, PROMOTE to virtual
                    int firstVisible = FirstDisplayedScrollingRowIndex;
                    int lastVisible = firstVisible + DisplayedRowCount(true);
                    
                    bool isEnteringVirtualRange = hit.RowIndex < firstVisible || hit.RowIndex >= lastVisible || VirtualSelectedCellCount > 0;

                    if (isEnteringVirtualRange)
                    {
                        PromoteNativeToVirtual();
                        
                        _lastMouseMoveRowIndex = hit.RowIndex;
                        _lastMouseMoveColumnIndex = hit.ColumnIndex;
                        
                        HandleVirtualSelection(hit.RowIndex, hit.ColumnIndex, Keys.Shift);
                        ClearSelection();
                    }
                    else
                    {
                        // Visible drag: Use native selection
                        _lastMouseMoveRowIndex = hit.RowIndex;
                        _lastMouseMoveColumnIndex = hit.ColumnIndex;
                        base.OnMouseMove(e);
                    }
                }
            }
            else
            {
                base.OnMouseMove(e);
            }
        }

        /// <summary>
        /// Converts the current WinForms Native Selection (Blue border) 
        /// to Virtual Selection (Blue block) for high-performance interaction.
        /// </summary>
        private void PromoteNativeToVirtual()
        {
            if (SelectedCells.Count == 0 && SelectedRows.Count == 0) return;
            if (VirtualSelectedCellCount > 0) return; // Already virtual

            // Use the anchor from mouse down if available
            // Note: SelectedCells might be in reverse order of selection
            
            foreach (DataGridViewCell cell in SelectedCells)
            {
                AddVirtualSelection(cell.RowIndex, cell.ColumnIndex);
            }

            // Also handle full rows if selected natively
            foreach (DataGridViewRow row in SelectedRows)
            {
                _virtualSelectedRows.Add(row.Index);
            }
            
            // Note: Native Column selection in DGV is rare via UI, but if present:
            // (Skipped for now as DoubleClick header already handles virtual columns)

            ClearSelection();
        }

        private void HandleVirtualSelection(int rowIndex, int colIndex, Keys modifiers)
        {
            bool isCtrl = (modifiers & Keys.Control) == Keys.Control;
            bool isShift = (modifiers & Keys.Shift) == Keys.Shift;

            if (!isCtrl && !isShift)
            {
                // New Single Selection
                VirtualClearSelection(); 
                _anchorRowIndex = rowIndex;
                _anchorColumnIndex = colIndex;
                AddVirtualSelection(rowIndex, colIndex);
            }
            else if (isCtrl)
            {
                // Toggle Selection (Anchor moves to new focus)
                _anchorRowIndex = rowIndex;
                _anchorColumnIndex = colIndex;
                ToggleVirtualSelection(rowIndex, colIndex);
            }
            else if (isShift)
            {
                // Range Selection (Anchor stays put!)
                if (_anchorRowIndex == -1) _anchorRowIndex = rowIndex;
                if (_anchorColumnIndex == -1) _anchorColumnIndex = colIndex;

                // Clear previous *temporary* range parts, but we need to know what was selected *before* the shift click starts?
                // Native behavior: The selection assumes the state from the Anchor.
                // Simplified Virtual approach: Clear everything and re-select from Anchor to Current.
                // BUT if Ctrl was used before, we might have multiple gaps.
                // Standard Shift-Click usually extends from the *last* anchor to current, ignoring gaps? 
                // Actually, Windows Explorer Shift-Click resets other selections often unless Ctrl is also held.
                // Let's implement Excel-like/Native DGV: Shift-Click selects range from Anchor to Current.
                // It usually clears other selections UNLESS Ctrl is held too?
                // For simplified high-perf: Clear and select range.
                
                // If we want to support Ctrl+Shift (Add Range), we check both.
                if (!isCtrl) VirtualClearSelection(keepAnchor: true); 

                SelectVirtualRange(_anchorRowIndex, _anchorColumnIndex, rowIndex, colIndex);
            }

            OnVirtualSelectionChanged();
            Invalidate();
        }

        private void AddVirtualSelection(int rowIndex, int colIndex)
        {
             if (SelectionMode == DataGridViewSelectionMode.FullRowSelect || SelectionMode == DataGridViewSelectionMode.RowHeaderSelect)
             {
                 _virtualSelectedRows.Add(rowIndex);
             }
             else
             {
                 if (!_virtualSelectedCells.ContainsKey(rowIndex))
                     _virtualSelectedCells[rowIndex] = new HashSet<int>();
                 
                 _virtualSelectedCells[rowIndex].Add(colIndex);
             }
        }

        private void ToggleVirtualSelection(int rowIndex, int colIndex)
        {
             if (SelectionMode == DataGridViewSelectionMode.FullRowSelect || SelectionMode == DataGridViewSelectionMode.RowHeaderSelect)
             {
                 if (_virtualSelectedRows.Contains(rowIndex))
                    _virtualSelectedRows.Remove(rowIndex);
                 else
                    _virtualSelectedRows.Add(rowIndex);
             }
             else
             {
                 if (IsVirtualCellSelected(rowIndex, colIndex))
                 {
                     if (_virtualSelectedCells.ContainsKey(rowIndex))
                         _virtualSelectedCells[rowIndex].Remove(colIndex);
                 }
                 else
                 {
                     AddVirtualSelection(rowIndex, colIndex);
                 }
             }
        }

        private void SelectVirtualRange(int r1, int c1, int r2, int c2)
        {
            int minR = Math.Min(r1, r2);
            int maxR = Math.Max(r1, r2);
            int minC = Math.Min(c1, c2);
            int maxC = Math.Max(c1, c2);

            if (SelectionMode == DataGridViewSelectionMode.FullRowSelect || SelectionMode == DataGridViewSelectionMode.RowHeaderSelect)
            {
                 for (int r = minR; r <= maxR; r++)
                 {
                     _virtualSelectedRows.Add(r);
                 }
            }
            else
            {
                 // Cell Select Mode
                 for (int r = minR; r <= maxR; r++)
                 {
                     if (!_virtualSelectedCells.ContainsKey(r))
                         _virtualSelectedCells[r] = new HashSet<int>();
                     
                     for (int c = minC; c <= maxC; c++)
                     {
                          _virtualSelectedCells[r].Add(c);
                     }
                 }
            }
        }

        #endregion

    }
}

