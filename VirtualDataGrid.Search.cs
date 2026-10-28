using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace VirtualADGV.WPF;

public partial class VirtualDataGrid
{
    private TextBlock? _searchStatus;
    private DispatcherOperation? _searchNavigation;
    internal string SearchStatus => _searchStatus?.Text ?? "";

    private void DetachSearchParts()
    {
        if (_searchButton != null) _searchButton.Click -= OnSearchNext;
        if (_searchResetButton != null) _searchResetButton.Click -= OnSearchRestart;
        if (_searchTextBox != null)
        {
            _searchTextBox.KeyDown -= OnSearchKey;
            _searchTextBox.TextChanged -= OnSearchTextChanged;
        }
    }

    private void OnSearchNext(object sender, RoutedEventArgs e) => TriggerSearch(true);
    private void OnSearchRestart(object sender, RoutedEventArgs e) => TriggerSearch(false);
    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { TriggerSearch(true); e.Handled = true; }
    }
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ClearSearchResults();
    private static void OnSearchVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) ((VirtualDataGrid)d).ClearSearchResults();
    }
    private void ClearSearchResults()
    {
        _searchNavigation?.Abort();
        _searchNavigation = null;
        SetMinimapSearchHits(null);
        if (_searchStatus != null) _searchStatus.Text = "";
    }

    private void RunBuiltInSearch(SearchEventArgs args)
    {
        _searchNavigation?.Abort();
        _searchNavigation = null;
        if (!GridSearch.IsInMemory(this)) return;
        int start = args.SearchNext && SelectedIndex >= 0 ? SelectedIndex + 1 : 0;
        var hits = GridSearch.FindAll(this, args.SearchText ?? "", args.ColumnName, args.IsCaseSensitive);
        SetMinimapSearchHits(hits.Select(i => (long)i).ToArray());
        int index = hits.FindIndex(i => i >= start);
        if (index < 0)
        {
            if (_searchStatus != null) _searchStatus.Text = start > 0 ? Strings.SearchEndReached : Strings.SearchNoMatch;
            return;
        }
        object item = Items[hits[index]];
        SelectedItem = item;
        if (_searchStatus != null) _searchStatus.Text = string.Format(CultureInfo.CurrentCulture, Strings.SearchMatchCountFormat, hits.Count);
        _searchNavigation?.Abort();
        _searchNavigation = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _searchNavigation = null;
            ScrollIntoView(item);
            Focus();
        }));
    }
}
