using System.Globalization;

namespace VirtualADGV.Grid;

/// <summary>A UI-independent column value accessor and optional display format.</summary>
public sealed class RowSearchColumn
{
    /// <summary>Creates an accessor; format may be a composite format or a single value format.</summary>
    public RowSearchColumn(Func<object, object?> value, string? format = null)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Format = format;
    }

    /// <summary>Reads a cell from a row.</summary>
    public Func<object, object?> Value { get; }
    /// <summary>Optional display format.</summary>
    public string? Format { get; }
}

/// <summary>Text search over rows in their current visual order, without UI dependencies.</summary>
public sealed class RowSearch
{
    /// <summary>Maximum source size for automatic in-memory search. Adapters apply this guard before scanning.</summary>
    public const int InMemoryRowLimit = 200_000;

    private readonly int _rowCount;
    private readonly Func<int, object?> _rowAt;
    private readonly IReadOnlyList<RowSearchColumn> _columns;
    private readonly CultureInfo _culture;
    private readonly StringComparison _comparison;

    /// <summary>Creates a search over a fixed row count and the supplied column accessors.</summary>
    public RowSearch(int rowCount, Func<int, object?> rowAt, IReadOnlyList<RowSearchColumn> columns,
        CultureInfo culture, bool caseSensitive)
    {
        if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        _rowCount = rowCount;
        _rowAt = rowAt ?? throw new ArgumentNullException(nameof(rowAt));
        _columns = columns ?? throw new ArgumentNullException(nameof(columns));
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.InvariantCultureIgnoreCase;
    }

    /// <summary>First matching row at or after startIndex; negative starts become zero. Does not wrap.</summary>
    public int FindNext(string text, int startIndex)
    {
        foreach (int index in Matches(text, startIndex)) return index;
        return -1;
    }

    /// <summary>All matching row ordinals, in ascending order, with each row included once.</summary>
    public List<int> FindAll(string text) => Matches(text, 0).ToList();

    private IEnumerable<int> Matches(string text, int startIndex)
    {
        if (string.IsNullOrEmpty(text) || _columns.Count == 0) yield break;
        for (int i = Math.Max(0, startIndex); i < _rowCount; i++)
        {
            object? item = _rowAt(i);
            if (item == null) continue;
            foreach (var column in _columns)
            {
                object? val = column.Value(item);
                if (val == DBNull.Value) val = null;
                string display;
                if (val == null) display = "";
                else if (!string.IsNullOrEmpty(column.Format))
                {
                    string fmt = column.Format!;
                    try { display = string.Format(_culture, fmt.IndexOf('{') >= 0 ? fmt : "{0:" + fmt + "}", val); }
                    catch (FormatException) { display = Convert.ToString(val, _culture) ?? ""; }
                }
                else display = Convert.ToString(val, _culture) ?? "";
                string? raw = val is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : null;
                if ((display.Length > 0 && display.IndexOf(text, _comparison) >= 0) ||
                    (raw != null && raw.IndexOf(text, _comparison) >= 0))
                {
                    yield return i;
                    break;
                }
            }
        }
    }
}
