namespace VirtualADGV.Data;

/// <summary>A successfully loaded page, addressed by its source ordinals.</summary>
public sealed class VirtualPageLoadedEventArgs : EventArgs
{
    /// <summary>Creates a page notification.</summary>
    public VirtualPageLoadedEventArgs(long start, int count)
    {
        Start = start;
        Count = count;
    }

    /// <summary>Zero-based ordinal of the first row.</summary>
    public long Start { get; }

    /// <summary>Number of rows returned by the source.</summary>
    public int Count { get; }
}
