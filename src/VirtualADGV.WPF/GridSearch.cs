using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using VirtualADGV.Grid;

namespace VirtualADGV.WPF;

/// <summary>Bellek içi tabloda görünen sıra üzerinden metin araması.</summary>
public static class GridSearch
{
    // Anahtar küçük bir yapıdır: değer çifti (ValueTuple) .NET Framework 4.6.2'de ek paket ister, Tuple her satırda tahsis eder.
    private readonly struct PropertyKey : IEquatable<PropertyKey>
    {
        public PropertyKey(Type type, string path) { Type = type; Path = path; }
        public Type Type { get; }
        public string Path { get; }
        public bool Equals(PropertyKey other) => Type == other.Type && string.Equals(Path, other.Path, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is PropertyKey other && Equals(other);
        public override int GetHashCode() => unchecked(Type.GetHashCode() * 397 ^ StringComparer.Ordinal.GetHashCode(Path));
    }
    private static readonly ConcurrentDictionary<PropertyKey, PropertyInfo?> PropertyCache = new();

    // Bilinmeyen IList bir sayfalı kaynak olabilir; boyutu küçükken de taranmaz.
    internal static bool IsInMemory(DataGrid grid)
    {
        if (grid.Items.Count > RowSearch.InMemoryRowLimit) return false;
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
        => string.IsNullOrEmpty(text) ? -1 : CreateSearch(grid, columnHeader, caseSensitive)?.FindNext(text, startIndex) ?? -1;

    /// <summary>Tüm eşleşmelerin görsel sıralarını tek geçişte verir.</summary>
    public static List<int> FindAll(DataGrid grid, string text, string? columnHeader, bool caseSensitive)
        => string.IsNullOrEmpty(text) ? new List<int>() : CreateSearch(grid, columnHeader, caseSensitive)?.FindAll(text) ?? new List<int>();

    private static RowSearch? CreateSearch(DataGrid grid, string? columnHeader, bool caseSensitive)
    {
        if (grid == null) return null;
        var columns = new List<RowSearchColumn>();
        foreach (var col in grid.Columns.OfType<DataGridBoundColumn>())
        {
            if (!string.IsNullOrEmpty(columnHeader) && HeaderKey(col.Header?.ToString()) != HeaderKey(columnHeader)) continue;
            if (col.Binding is Binding binding && !string.IsNullOrEmpty(binding.Path?.Path))
            {
                string path = binding.Path!.Path!;
                columns.Add(new RowSearchColumn(item => ReadValue(item, path), binding.StringFormat));
            }
        }
        if (columns.Count == 0) return null;
        var culture = grid.Language?.GetSpecificCulture() ?? CultureInfo.CurrentCulture;
        return new RowSearch(grid.Items.Count, i => grid.Items[i], columns, culture, caseSensitive);
    }

    private static object? ReadValue(object item, string path)
    {
        if (item is DataRowView row)
            return row.Row.Table.Columns.Contains(path) ? row[path] : null;
        return PropertyCache.GetOrAdd(new PropertyKey(item.GetType(), path), key => key.Type.GetProperty(key.Path))?.GetValue(item);
    }
    private static string HeaderKey(string? header) => (header ?? "").TrimEnd(' ', '⏷');
}
