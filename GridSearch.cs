using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;

namespace VirtualADGV.WPF;

/// <summary>Bellek içi tabloda görünen sıra üzerinden metin araması.</summary>
public static class GridSearch
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache = new();

    // Bilinmeyen IList bir sayfalı kaynak olabilir; boyutu küçükken de taranmaz.
    internal static bool IsInMemory(DataGrid grid)
    {
        if (grid.Items.Count > 200_000) return false;
        object? source = grid.ItemsSource;
        while (source is ICollectionView view && !ReferenceEquals(source, view.SourceCollection))
            source = view.SourceCollection;
        if (source == null || source is DataView or Array or ArrayList) return true;
        for (Type? type = source.GetType(); type != null; type = type.BaseType)
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(System.Collections.ObjectModel.Collection<>))) return true;
        return false;
    }

    /// <summary>Başlangıçtan sona ilk eşleşmenin görsel sırasını verir; sarma yapmaz.</summary>
    public static int FindNext(DataGrid grid, string text, string? columnHeader, bool caseSensitive, int startIndex)
        => Matches(grid, text, columnHeader, caseSensitive, startIndex).FirstOrDefault(-1);

    /// <summary>Tüm eşleşmelerin görsel sıralarını tek geçişte verir.</summary>
    public static List<int> FindAll(DataGrid grid, string text, string? columnHeader, bool caseSensitive)
        => Matches(grid, text, columnHeader, caseSensitive, 0).ToList();

    private static IEnumerable<int> Matches(DataGrid grid, string text, string? columnHeader, bool caseSensitive, int startIndex)
    {
        if (grid == null || string.IsNullOrEmpty(text)) yield break;
        var columns = new List<(Binding Binding, string Path)>();
        foreach (var col in grid.Columns.OfType<DataGridBoundColumn>())
        {
            if (!string.IsNullOrEmpty(columnHeader) && HeaderKey(col.Header?.ToString()) != HeaderKey(columnHeader)) continue;
            if (col.Binding is Binding b && !string.IsNullOrEmpty(b.Path?.Path)) columns.Add((b, b.Path.Path));
        }
        if (columns.Count == 0) yield break;
        var culture = grid.Language?.GetSpecificCulture() ?? CultureInfo.CurrentCulture;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.InvariantCultureIgnoreCase;
        for (int i = Math.Max(0, startIndex); i < grid.Items.Count; i++)
        {
            object item = grid.Items[i];
            if (item == null) continue;
            foreach (var (binding, path) in columns)
            {
                object? val = null;
                if (item is DataRowView row)
                {
                    if (row.Row.Table.Columns.Contains(path)) val = row[path];
                }
                else val = PropertyCache.GetOrAdd((item.GetType(), path), key => key.Item1.GetProperty(key.Item2))?.GetValue(item);
                if (val == DBNull.Value) val = null;
                string display;
                if (val == null) display = "";
                else if (!string.IsNullOrEmpty(binding.StringFormat))
                {
                    string fmt = binding.StringFormat;
                    try { display = string.Format(culture, fmt.Contains('{') ? fmt : "{0:" + fmt + "}", val); }
                    catch (FormatException) { display = Convert.ToString(val, culture) ?? ""; }
                }
                else display = Convert.ToString(val, culture) ?? "";
                string? raw = val is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : null;
                if ((display.Length > 0 && display.Contains(text, comparison)) || (raw != null && raw.Contains(text, comparison)))
                {
                    yield return i;
                    break;
                }
            }
        }
    }

    private static string HeaderKey(string? header) => (header ?? "").TrimEnd(' ', '⏷');
}
