namespace VirtualADGV.Grid;

/// <summary>A set of nonnegative 64-bit ordinals, stored as inclusive ranges.</summary>
public sealed class OrdinalSelection
{
    private readonly List<Range> _ranges = new();
    private readonly struct Range
    {
        internal Range(long start, long end) { Start = start; End = end; }
        internal long Start { get; }
        internal long End { get; }
    }

    /// <summary>Adds one ordinal; returns false if already selected.</summary>
    public bool Add(long ordinal)
    {
        if (Contains(ordinal)) return false;
        AddRange(ordinal, ordinal);
        return true;
    }

    /// <summary>Removes one ordinal; returns false if absent.</summary>
    public bool Remove(long ordinal)
    {
        int index = Find(ordinal);
        if (index < 0) return false;
        var range = _ranges[index];
        if (range.Start == range.End) _ranges.RemoveAt(index);
        else if (ordinal == range.Start) _ranges[index] = new Range(ordinal + 1, range.End);
        else if (ordinal == range.End) _ranges[index] = new Range(range.Start, ordinal - 1);
        else
        {
            _ranges[index] = new Range(range.Start, ordinal - 1);
            _ranges.Insert(index + 1, new Range(ordinal + 1, range.End));
        }
        return true;
    }

    /// <summary>Tests membership.</summary>
    public bool Contains(long ordinal) => Find(ordinal) >= 0;

    private int Find(long ordinal)
    {
        int low = 0, high = _ranges.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            var range = _ranges[middle];
            if (ordinal < range.Start) high = middle - 1;
            else if (ordinal > range.End) low = middle + 1;
            else return middle;
        }
        return -1;
    }

    /// <summary>Clears all selected ordinals.</summary>
    public void Clear() => _ranges.Clear();

    /// <summary>Adds both endpoints and every ordinal between them. Either endpoint order is accepted.</summary>
    public void AddRange(long start, long end)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (end < 0) throw new ArgumentOutOfRangeException(nameof(end));
        if (start > end) { long swap = start; start = end; end = swap; }
        int first = 0;
        while (first < _ranges.Count && _ranges[first].End < start && _ranges[first].End != start - 1) first++;
        int after = first;
        while (after < _ranges.Count && (_ranges[after].Start <= end || (end < long.MaxValue && _ranges[after].Start == end + 1)))
        {
            start = Math.Min(start, _ranges[after].Start);
            end = Math.Max(end, _ranges[after].End);
            after++;
        }
        if (after > first) _ranges.RemoveRange(first, after - first);
        _ranges.Insert(first, new Range(start, end));
    }

    /// <summary>Returns at most limit selected ordinals in ascending order.</summary>
    public long[] ToSortedArray(int limit = int.MaxValue)
    {
        if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var result = new List<long>(Math.Min(limit, 1024));
        foreach (var range in _ranges)
            for (long ordinal = range.Start; result.Count < limit; ordinal++)
            {
                result.Add(ordinal);
                if (ordinal == range.End) break;
            }
        return result.ToArray();
    }
}
