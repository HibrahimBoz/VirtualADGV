using System.Globalization;

namespace VirtualADGV.Grid;

/// <summary>A viewport expressed as fractions of the minimap height.</summary>
public readonly struct MinimapViewport
{
    /// <summary>Creates a normalized viewport.</summary>
    public MinimapViewport(double topRatio, double heightRatio)
    {
        TopRatio = topRatio;
        HeightRatio = heightRatio;
    }
    /// <summary>Top edge, in the range zero to one.</summary>
    public double TopRatio { get; }
    /// <summary>Height, in the range zero to one.</summary>
    public double HeightRatio { get; }
}

/// <summary>Minimap buckets, viewport ratios and deferred drag state. Owns no UI objects or timers.</summary>
public sealed class MinimapModel
{
    private IReadOnlyList<long> _marks = Array.Empty<long>(), _selection = Array.Empty<long>(), _searchHits = Array.Empty<long>();
    private long[] _markBuckets = Array.Empty<long>(), _selectionBuckets = Array.Empty<long>(), _searchBuckets = Array.Empty<long>();
    private bool _bucketsDirty = true;
    private int _bucketCount;

    /// <summary>Marked source ordinals. Call InvalidateBuckets if the supplied list changes in place.</summary>
    public IReadOnlyList<long> Marks
    {
        get => _marks;
        set { _marks = value ?? throw new ArgumentNullException(nameof(value)); InvalidateBuckets(); }
    }
    /// <summary>Selected source ordinals.</summary>
    public IReadOnlyList<long> Selection
    {
        get => _selection;
        set { _selection = value ?? throw new ArgumentNullException(nameof(value)); InvalidateBuckets(); }
    }
    /// <summary>Search result ordinals.</summary>
    public IReadOnlyList<long> SearchHits
    {
        get => _searchHits;
        set { _searchHits = value ?? throw new ArgumentNullException(nameof(value)); InvalidateBuckets(); }
    }
    /// <summary>Total source row count.</summary>
    public long TotalRows { get; private set; }
    /// <summary>Top of the map in the adapter's coordinate system.</summary>
    public double MapTop { get; private set; }
    /// <summary>Map height; zero disables targeting.</summary>
    public double MapHeight { get; private set; }
    /// <summary>True between BeginDrag and EndDrag or CancelDrag.</summary>
    public bool IsDragging { get; private set; }
    /// <summary>Last drag target, as a fraction of all rows.</summary>
    public double TargetRatio { get; private set; }
    /// <summary>The adapter should restart its deferred-scroll timer after a drag update.</summary>
    public bool NeedsDeferredScroll { get; private set; }
    /// <summary>First marked ordinal per bucket, or -1 for an empty bucket.</summary>
    public IReadOnlyList<long> MarkBuckets { get { EnsureBuckets(); return _markBuckets; } }
    /// <summary>First selected ordinal per bucket.</summary>
    public IReadOnlyList<long> SelectionBuckets { get { EnsureBuckets(); return _selectionBuckets; } }
    /// <summary>First search hit per bucket.</summary>
    public IReadOnlyList<long> SearchBuckets { get { EnsureBuckets(); return _searchBuckets; } }

    /// <summary>Updates source size and map coordinates; no platform geometry types are needed.</summary>
    public void SetGeometry(long totalRows, double mapTop, double mapHeight)
    {
        if (totalRows < 0) throw new ArgumentOutOfRangeException(nameof(totalRows));
        if (double.IsNaN(mapTop) || double.IsInfinity(mapTop)) throw new ArgumentOutOfRangeException(nameof(mapTop));
        if (double.IsNaN(mapHeight) || double.IsInfinity(mapHeight) || mapHeight < 0 || mapHeight > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(mapHeight));
        int height = mapHeight == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(mapHeight));
        if (TotalRows != totalRows || _bucketCount != height) InvalidateBuckets();
        TotalRows = totalRows;
        MapTop = mapTop;
        MapHeight = mapHeight;
        _bucketCount = height;
    }

    /// <summary>Invalidates cached buckets without reading any row data.</summary>
    public void InvalidateBuckets() => _bucketsDirty = true;

    /// <summary>Maps valid ordinals to height buckets. Each bucket keeps its smallest ordinal, or -1.</summary>
    public static long[] Bucketize(IReadOnlyList<long> ordinals, long total, int height)
    {
        if (ordinals == null) throw new ArgumentNullException(nameof(ordinals));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        var buckets = new long[height];
        Compat.Fill(buckets, -1L);
        if (total <= 0 || height == 0) return buckets;
        for (int i = 0; i < ordinals.Count; i++)
        {
            long ordinal = ordinals[i];
            if (ordinal < 0 || ordinal >= total) continue;
            // Clamp before converting: even long.MaxValue can round to the last boundary.
            int y = (int)Math.Min(height - 1, ordinal * (double)height / total);
            if (buckets[y] < 0 || ordinal < buckets[y]) buckets[y] = ordinal;
        }
        return buckets;
    }

    private void EnsureBuckets()
    {
        if (!_bucketsDirty) return;
        _markBuckets = Bucketize(_marks, TotalRows, _bucketCount);
        _selectionBuckets = Bucketize(_selection, TotalRows, _bucketCount);
        _searchBuckets = Bucketize(_searchHits, TotalRows, _bucketCount);
        _bucketsDirty = false;
    }

    /// <summary>Pointer ratio, optionally snapping to a marked bucket at most two buckets away; upper ties win.</summary>
    public double RatioAt(double y, bool snap)
    {
        if (MapHeight <= 0) return 0;
        EnsureBuckets();
        double ratio = Compat.Clamp((y - MapTop) / MapHeight, 0, 1);
        if (snap)
        {
            int bucket = Compat.Clamp((int)(ratio * _bucketCount), 0, Math.Max(0, _bucketCount - 1));
            for (int distance = 0; distance <= 2; distance++)
                foreach (int index in distance == 0 ? new[] { bucket } : new[] { bucket - distance, bucket + distance })
                    if (index >= 0 && index < _markBuckets.Length && _markBuckets[index] >= 0)
                        return _markBuckets[index] / (double)Math.Max(1L, TotalRows);
        }
        return ratio;
    }

    /// <summary>Calculates the viewport ratios; minimumHeightRatio is supplied by the drawing adapter.</summary>
    public static MinimapViewport CalculateViewport(double offset, double extent, double viewport, double minimumHeightRatio = 0)
    {
        if (extent <= 0) return default;
        double height = Math.Min(1, Math.Max(minimumHeightRatio, viewport / extent));
        double top = Compat.Clamp(offset / extent, 0, 1 - height);
        return new MinimapViewport(top, height);
    }

    /// <summary>Scroll offset that centers the target ratio, clamped to the scrollable extent.</summary>
    public static double TargetOffset(double ratio, double extent, double viewport)
        => Compat.Clamp(ratio * extent - viewport / 2, 0, Math.Max(0, extent - viewport));

    /// <summary>Starts dragging and snaps the initial target to nearby marks.</summary>
    public void BeginDrag(double y) { IsDragging = true; UpdateDrag(y, true); }
    /// <summary>Moves the drag target without snapping.</summary>
    public void MoveDrag(double y) => UpdateDrag(y, false);

    private void UpdateDrag(double y, bool snap)
    {
        if (!IsDragging || MapHeight <= 0) return;
        TargetRatio = RatioAt(y, snap);
        NeedsDeferredScroll = true;
    }

    /// <summary>Consumes a pending timer scroll. The adapter scrolls to TargetRatio when this returns true.</summary>
    public bool ConsumeDeferredScroll()
    {
        bool pending = IsDragging && NeedsDeferredScroll;
        NeedsDeferredScroll = false;
        return pending;
    }

    /// <summary>Ends the drag. Returns true when the adapter should apply the final target.</summary>
    public bool EndDrag()
    {
        bool wasDragging = IsDragging;
        IsDragging = false;
        NeedsDeferredScroll = false;
        return wasDragging;
    }

    /// <summary>Cancels without requesting a final scroll.</summary>
    public void CancelDrag() { IsDragging = false; NeedsDeferredScroll = false; }

    /// <summary>Formats the current one-based target row using the adapter's format and culture.</summary>
    public string FormatRowHint(string format, CultureInfo culture)
    {
        double ordinal = TargetRatio * TotalRows;
        long row = TotalRows <= 0 ? 1 : ordinal >= TotalRows - 1 ? TotalRows : (long)ordinal + 1;
        return string.Format(culture, format, row);
    }
}
